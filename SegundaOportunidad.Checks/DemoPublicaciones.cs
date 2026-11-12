using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class DemoPublicaciones
{
    public static async Task Run(string path)
    {
        var fullPath=Path.GetFullPath(path);
        if(!File.Exists(fullPath)) throw new FileNotFoundException("No se encontró la base local.");
        await using var connection=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=fullPath, Mode=SqliteOpenMode.ReadWrite }.ToString());
        await connection.OpenAsync();
        await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        if((await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Hay migraciones pendientes; no se añadirán datos.");
        var admin=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where r.Name=="Admin" orderby ur.UserId select ur.UserId).FirstAsync();
        var categories=await db.Categorias.OrderBy(c=>c.Id).ToListAsync();
        if(categories.Count==0) throw new Exception("No existen categorías.");
        var samples=new[] {
            new { Id="demo-chat-bicicleta",Title="[PRUEBA] Bicicleta urbana",Category="Deportes",Description="Bicicleta urbana de ejemplo, aro 26, frenos operativos. Publicación ficticia para pruebas; no realizar pagos ni coordinar entregas.", Mode=ModalidadArticulo.Intercambio, Price=(decimal?)null, Exchange="Busco una mochila o artículos deportivos.",Photo="photo-1485965120184-e220f721d03e" },
            new { Id="demo-chat-mochila",Title="[PRUEBA] Mochila universitaria",Category="Ropa",Description="Mochila de ejemplo con espacio para laptop de 15 pulgadas. Publicación ficticia para pruebas; no realizar pagos ni coordinar entregas.",Mode=ModalidadArticulo.Venta,Price=(decimal?)45,Exchange="",Photo="photo-1553062407-98eeb64c6a62" },
            new { Id="demo-chat-libros",Title="[PRUEBA] Libros para estudiar",Category="Libros",Description="Lote de libros de ejemplo para estudiantes. Publicación ficticia para pruebas; no realizar pagos ni coordinar entregas.",Mode=ModalidadArticulo.Donacion,Price=(decimal?)null,Exchange="",Photo="photo-1495446815901-a7297e633e8d" },
            new { Id="demo-chat-laptop",Title="[PRUEBA] Laptop para clases",Category="Tecnología",Description="Laptop de ejemplo: 8 GB de RAM y SSD de 256 GB, para clases y tareas. Publicación ficticia para pruebas; no realizar pagos ni coordinar entregas.",Mode=ModalidadArticulo.Venta,Price=(decimal?)850,Exchange="",Photo="photo-1496181133206-80ce9b88a853" },
            new { Id="demo-chat-silla",Title="[PRUEBA] Silla para escritorio",Category="Muebles",Description="Silla de escritorio de ejemplo para estudiar en casa. Publicación ficticia para pruebas; no realizar pagos ni coordinar entregas.",Mode=ModalidadArticulo.Intercambio,Price=(decimal?)null,Exchange="Busco una lámpara de escritorio.",Photo="photo-1503602642458-232111445657" }
        };
        using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(25)};
        var pending=new List<(Articulo Article,FotoArticulo Photo)>();
        foreach(var sample in samples)
        {
            if(await db.Articulos.IgnoreQueryFilters().AnyAsync(a=>a.Id==sample.Id)){Console.WriteLine("Ya existe: "+sample.Title);continue;}
            var category=categories.FirstOrDefault(c=>c.Name.Contains(sample.Category,StringComparison.OrdinalIgnoreCase)) ?? categories.FirstOrDefault(c=>c.Name.StartsWith("Otros")) ?? categories[0];
            using var response=await http.GetAsync($"https://images.unsplash.com/{sample.Photo}?w=480&q=70&fit=crop");
            response.EnsureSuccessStatusCode();
            var bytes=await response.Content.ReadAsByteArrayAsync();
            if(bytes.Length>4*1024*1024 || bytes.Length<3 || bytes[0]!=0xff || bytes[1]!=0xd8) throw new Exception("Imagen de prueba no válida.");
            var article=new Articulo {Id=sample.Id,Title=sample.Title,Description=sample.Description+" Fotografía ilustrativa de Unsplash.",UserId=admin,CategoriaId=category.Id,Modalidad=sample.Mode,Price=sample.Price,ExchangeDetails=sample.Exchange,EstadoFisico=EstadoFisico.BuenEstado,ImageUrl="/Articulos/Imagen/"+sample.Id};
            pending.Add((article,new FotoArticulo {ArticuloId=sample.Id,Contenido=bytes,Mime="image/jpeg"}));
        }
        if(pending.Count==0){Console.WriteLine("No se duplicaron publicaciones.");return;}
        var backupPath=fullPath+".backup-demo-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+".sqlite";
        await using(var backup=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=backupPath}.ToString())) {await backup.OpenAsync();connection.BackupDatabase(backup);}
        await using var tx=await db.Database.BeginTransactionAsync();
        foreach(var item in pending){db.Articulos.Add(item.Article);db.FotosArticulos.Add(item.Photo);}
        await db.SaveChangesAsync();
        if(await db.Articulos.CountAsync(a=>a.Id.StartsWith("demo-chat-"))!=5 || await db.FotosArticulos.CountAsync(f=>f.ArticuloId.StartsWith("demo-chat-"))!=5) throw new Exception("No se verificaron los cinco ejemplos con fotos.");
        await tx.CommitAsync();
        Console.WriteLine("Backup: "+backupPath);
        foreach(var item in pending) Console.WriteLine("Creada: "+item.Article.Title+" | "+item.Article.Modalidad+" | /Articulos/Details/"+item.Article.Id);
        Console.WriteLine("No se modificaron cuentas ni publicaciones existentes; sin sincronización externa con Algolia.");
    }
}
