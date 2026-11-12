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
        await db.Database.ExecuteSqlRawAsync("INSERT INTO AspNetUsers (Id,FullName,IsVerified,Co2Saved,ObjectsReused,FamiliesHelped,Rating,EmailConfirmed,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount,SanctionVersion) VALUES ('legacy','',0,'0',0,0,'0',0,0,0,0,0,'');");
        db.Categorias.Add(new Categoria { Id = 1, Name = "Prueba" });

        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Articulos (Id,Title,Description,Modalidad,EstadoFisico,IsActive,CreatedAt,CategoriaId,UserId,Moderacion,ModeracionVersion) VALUES ('old1','Legacy 1','',0,0,1,'2026-01-01',1,'legacy',0,''),('old2','Legacy 2','',0,0,1,'2026-01-01',1,'legacy',0,'');");
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var tokens = await db.Articulos.Select(a => a.ModeracionVersion).ToListAsync();
        if (tokens.Any(string.IsNullOrWhiteSpace) || tokens.Distinct().Count() != 2 || string.IsNullOrWhiteSpace((await db.Users.SingleAsync()).SanctionVersion))
            throw new Exception("Migración no inicializó versiones antiguas");
        Console.WriteLine("OK: Migración inicializa versiones únicas de publicaciones y usuarios antiguos sin borrar filas");
    }
}
