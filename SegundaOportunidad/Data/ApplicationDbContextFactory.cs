using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SegundaOportunidad.Data;

// Las herramientas EF no deben arrancar RabbitMQ, Redis ni sembrar datos.
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=app.db").Options);
}
