using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Data;
public static class CategoryCatalog
{
    public static readonly string[] Names=["Ropa","Muebles","Electrónica","Libros","Deportes","Hogar y decoración","Electrodomésticos","Computación y accesorios","Celulares y accesorios","Herramientas","Juguetes y juegos","Bebés y niños","Música e instrumentos","Arte y manualidades","Jardín y exteriores","Salud y cuidado personal","Calzado y accesorios","Mascotas","Oficina y útiles","Videojuegos y consolas","Otros..."];
    public static async Task EnsureAsync(ApplicationDbContext db,CancellationToken ct=default)
    {
        var existing=(await db.Categorias.Select(c => c.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var name in Names.Where(n => !existing.Contains(n))) db.Categorias.Add(new Categoria { Name=name });
        await db.SaveChangesAsync(ct);
    }
}
