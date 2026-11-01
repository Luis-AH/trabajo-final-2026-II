using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class ResetContent
{
    public static async Task Run(string path)
    {
        await DatabaseUpgrade.Run(path); // Respaldo SQLite completo antes de migrar o limpiar.
        await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource=Path.GetFullPath(path) }.ToString()).Options);
        await using var transaction=await db.Database.BeginTransactionAsync();
        var admin=await (from role in db.Roles join assignment in db.UserRoles on role.Id equals assignment.RoleId where role.Name=="Admin" select assignment.UserId).FirstAsync();
        var now=DateTime.UtcNow;
        var articles=await db.Articulos.IgnoreQueryFilters().ToListAsync();
        foreach(var article in articles)
        {
            if(article.Moderacion!=EstadoModeracion.Eliminada)
                db.ModeracionesPublicaciones.Add(new ModeracionPublicacion { ArticuloId=article.Id,AdministradorId=admin,Anterior=article.Moderacion,Nuevo=EstadoModeracion.Eliminada,FechaUtc=now,Motivo="Limpieza local solicitada por el propietario del proyecto al activar aprobación obligatoria." });
            article.Moderacion=EstadoModeracion.Eliminada;
            article.IsActive=false;
            article.ModeracionVersion=Guid.NewGuid().ToString();
        }
        var exchanges=await db.Intercambios.IgnoreQueryFilters().ExecuteDeleteAsync();
        var messages=await db.Mensajes.ExecuteDeleteAsync();
        var state=await db.EstadosSistema.FindAsync("chat-reset");
        if(state==null) db.EstadosSistema.Add(new EstadoSistema { Id="chat-reset",FechaUtc=now }); else state.FechaUtc=now;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine($"Limpieza local completada: publicaciones retiradas lógicamente={articles.Count}, intercambios eliminados={exchanges}, mensajes eliminados={messages}. Usuarios, categorías, reportes y auditorías conservados.");
        Console.WriteLine("Se guardó el corte temporal para no reinsertar mensajes anteriores desde RabbitMQ. No se purgaron servicios compartidos.");
    }
}
