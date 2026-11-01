using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;

internal static class DatabaseVerification
{
    public static async Task Run(string path)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), Mode = SqliteOpenMode.ReadOnly }.ToString());
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        if ((string?)await cmd.ExecuteScalarAsync() != "ok") throw new Exception("Integridad SQLite incorrecta");
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var reader = await cmd.ExecuteReaderAsync()) if (await reader.ReadAsync()) throw new Exception("Relaciones SQLite inconsistentes");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        if ((await db.Database.GetPendingMigrationsAsync()).Any() || db.Database.HasPendingModelChanges()) throw new Exception("Esquema o migraciones pendientes");
        var admins = await (from role in db.Roles join userRole in db.UserRoles on role.Id equals userRole.RoleId where role.Name == "Admin" select userRole.UserId).CountAsync();
        if (admins == 0) throw new Exception("No hay administradores con rol asignado");
        Console.WriteLine($"OK BASE LOCAL (solo lectura): integridad, relaciones, modelo y migraciones. Usuarios={await db.Users.CountAsync()}, administradores={admins}, artículos={await db.Articulos.IgnoreQueryFilters().CountAsync()}, intercambios={await db.Intercambios.CountAsync()}");
    }
}
