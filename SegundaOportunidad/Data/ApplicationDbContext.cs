using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Articulo> Articulos { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Mensaje> Mensajes { get; set; }
    public DbSet<Intercambio> Intercambios { get; set; }
    public DbSet<ModeracionPublicacion> ModeracionesPublicaciones { get; set; }
    public DbSet<SancionUsuario> SancionesUsuarios { get; set; }
    public DbSet<Reporte> Reportes { get; set; }
    public DbSet<DecisionReporte> DecisionesReportes { get; set; }
    public DbSet<AnalisisAdminGuard> AnalisisAdminGuard { get; set; }
    public DbSet<EstadoSistema> EstadosSistema { get; set; }
    public DbSet<FotoArticulo> FotosArticulos { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<FotoArticulo>().HasKey(f => f.ArticuloId);
        builder.Entity<FotoArticulo>().HasOne(f => f.Articulo).WithOne().HasForeignKey<FotoArticulo>(f => f.ArticuloId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ApplicationUser>().HasOne<ApplicationUser>().WithMany().HasForeignKey(u => u.VerifiedByAdminId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AnalisisAdminGuard>().HasOne(a => a.Administrador).WithMany().HasForeignKey(a => a.AdministradorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AnalisisAdminGuard>().HasIndex(a => new { a.AdministradorId, a.CreadoUtc });
        builder.Entity<AnalisisAdminGuard>().HasIndex(a => a.ImagenSha256);
        builder.Entity<Reporte>().HasOne(r => r.Denunciante).WithMany().HasForeignKey(r => r.DenuncianteId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Reporte>().HasOne(r => r.Denunciado).WithMany().HasForeignKey(r => r.DenunciadoId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Reporte>().HasOne(r => r.Articulo).WithMany().HasForeignKey(r => r.ArticuloId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Reporte>().HasIndex(r => r.ClaveAbierta).IsUnique();
        builder.Entity<Reporte>().HasIndex(r => new { r.Estado, r.CreadoUtc });
        builder.Entity<Reporte>().HasIndex(r => r.DenunciadoId);
        builder.Entity<DecisionReporte>().HasOne(d => d.Reporte).WithMany(r => r.Decisiones).HasForeignKey(d => d.ReporteId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DecisionReporte>().HasOne(d => d.Administrador).WithMany().HasForeignKey(d => d.AdministradorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ApplicationUser>().Property(u => u.SanctionVersion).IsConcurrencyToken();
        builder.Entity<SancionUsuario>().HasOne(s => s.Usuario).WithMany().HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SancionUsuario>().HasOne(s => s.Administrador).WithMany().HasForeignKey(s => s.AdministradorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SancionUsuario>().HasIndex(s => new { s.UsuarioId, s.FechaUtc });
        builder.Entity<Articulo>().HasQueryFilter(a => a.Moderacion == EstadoModeracion.Visible);
        builder.Entity<ModeracionPublicacion>().HasOne(m => m.Articulo).WithMany().HasForeignKey(m => m.ArticuloId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ModeracionPublicacion>().HasOne(m => m.Administrador).WithMany().HasForeignKey(m => m.AdministradorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ModeracionPublicacion>().HasIndex(m => new { m.ArticuloId, m.FechaUtc });
        
        // Configuraciones adicionales
        builder.Entity<Mensaje>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Mensaje>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Intercambio>()
            .HasOne(i => i.Proposer)
            .WithMany()
            .HasForeignKey(i => i.ProposerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Intercambio>()
            .HasOne(i => i.Receiver)
            .WithMany()
            .HasForeignKey(i => i.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<Intercambio>()
            .HasOne(i => i.OfferedArticle)
            .WithMany()
            .HasForeignKey(i => i.OfferedArticleId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<Intercambio>()
            .HasOne(i => i.RequestedArticle)
            .WithMany()
            .HasForeignKey(i => i.RequestedArticleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
