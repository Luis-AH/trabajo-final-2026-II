using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class LegacyTokensChecks
{
    public static async Task Run()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        var previous = db.Database.GetMigrations().Single(m => m.EndsWith("GestionUsuariosSanciones"));
        await db.GetService<IMigrator>().MigrateAsync(previous);
        db.Users.Add(new ApplicationUser { Id = "legacy", SanctionVersion = "" });
        db.Categorias.Add(new Categoria { Id = 1, Name = "Prueba" });
        db.Articulos.AddRange(new Articulo { Id = "old1", UserId = "legacy", CategoriaId = 1, ModeracionVersion = "" }, new Articulo { Id = "old2", UserId = "legacy", CategoriaId = 1, ModeracionVersion = "" });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var tokens = await db.Articulos.Select(a => a.ModeracionVersion).ToListAsync();
        if (tokens.Any(string.IsNullOrWhiteSpace) || tokens.Distinct().Count() != 2 || string.IsNullOrWhiteSpace((await db.Users.SingleAsync()).SanctionVersion))
            throw new Exception("Migración no inicializó versiones antiguas");
        Console.WriteLine("OK: Migración inicializa versiones únicas de publicaciones y usuarios antiguos sin borrar filas");
    }
}
