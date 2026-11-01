using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SegundaOportunidad.Data;

internal static class DatabaseUpgrade
{
    public static async Task Run(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Base no encontrada", path);
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        await source.OpenAsync();
        var backupPath = path + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".sqlite";
        await using (var backup = new SqliteConnection("Data Source=" + backupPath)) { await backup.OpenAsync(); source.BackupDatabase(backup); }
        Console.WriteLine("Respaldo: " + backupPath);
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(source).Options);
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        if (applied.Count == 0)
        {
            await using var baselineConnection = new SqliteConnection("Data Source=:memory:");
            await baselineConnection.OpenAsync();
            await using var baseline = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(baselineConnection).Options);
            await baseline.GetService<IMigrator>().MigrateAsync("20260926224210_InitialCreate");
            var expected = await Schema(baselineConnection);
            var actual = await Schema(source);
            foreach (var entry in expected.Where(x => !actual.TryGetValue(x.Key, out var value) || value != x.Value))
                Console.WriteLine($"Diferencia {entry.Key}: esperado {entry.Value}; actual {actual.GetValueOrDefault(entry.Key)}");
            if (expected.Count != actual.Count || expected.Any(x => !actual.TryGetValue(x.Key, out var value) || value != x.Value))
                throw new InvalidOperationException("Esquema distinto de las migraciones iniciales. No se modifica el historial; revisar manualmente.");
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);");
            foreach (var id in new[] { "00000000000000_CreateIdentitySchema", "20260926224210_InitialCreate" })
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({id}, {"10.0.12"});");
        }
        await db.Database.MigrateAsync();
        Console.WriteLine("Migraciones aplicadas. Se conservaron los datos existentes.");
    }

    private static async Task<Dictionary<string, string>> Schema(SqliteConnection connection)
    {
        var schema = new Dictionary<string, string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, sql FROM sqlite_master WHERE type IN ('table','index') AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%' ORDER BY name";
        var tables = new List<string>();
        using (var reader = await command.ExecuteReaderAsync())
        while (await reader.ReadAsync())
        {
            var sql = reader.IsDBNull(1) ? "" : reader.GetString(1);
            schema[reader.GetString(0)] = string.Concat(sql.Where(c => !char.IsWhiteSpace(c) && c != '"' && c != ';')).ToUpperInvariant();
            if (sql.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase)) tables.Add(reader.GetString(0));
        }
        foreach (var table in tables)
        {
            var columns = new List<string>();
            using var info = connection.CreateCommand();
            info.CommandText = "PRAGMA table_info(\"" + table.Replace("\"", "\"\"") + "\")";
            using (var reader = await info.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add($"{reader.GetString(1)}:{reader.GetString(2)}:{reader.GetInt32(3)}:{reader.GetInt32(5)}");
            info.CommandText = "PRAGMA foreign_key_list(\"" + table.Replace("\"", "\"\"") + "\")";
            using (var reader = await info.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add($"FK:{reader.GetString(2)}:{reader.GetString(3)}:{reader.GetString(4)}:{reader.GetString(5)}:{reader.GetString(6)}");
            schema[table] = string.Join("|", columns.OrderBy(x => x, StringComparer.Ordinal));
        }
        return schema;
    }
}
