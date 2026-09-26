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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
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
