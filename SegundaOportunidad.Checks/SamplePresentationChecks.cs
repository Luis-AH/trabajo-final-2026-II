using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class SamplePresentationChecks
{
    public static async Task Run(string path)
    {
        var full=DemoSetupChecks.ValidateDemoPath(path);
        if(!File.Exists(full))throw new InvalidOperationException("La base de simulación debe existir.");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Demo",ContentRootPath=Path.GetFullPath("SegundaOportunidad")});
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder {DataSource=full,Mode=SqliteOpenMode.ReadWrite}.ToString()));
        builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var app=builder.Build();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var privatePath=Path.Combine(Path.GetDirectoryName(full)!,"accounts.private.json");
        var credentials=JsonSerializer.Deserialize<List<DemoAccount>>(await File.ReadAllTextAsync(privatePath))??throw new InvalidOperationException("Faltan credenciales locales.");
        using var corpus=JsonDocument.Parse(await File.ReadAllTextAsync("ml/demo-corpus.json"));
        var rows=corpus.RootElement.EnumerateArray().ToList();
        if(rows.Count!=48||rows.Any(r=>!r.GetProperty("id").GetString()!.StartsWith("demo-ml-art-")))throw new InvalidOperationException("Corpus no reconocido.");
        var backup=Path.Combine(Path.GetDirectoryName(full)!,"before-presentation-"+Guid.NewGuid().ToString("N")+".db");
        await using(var source=new SqliteConnection("Data Source="+full+";Mode=ReadOnly"))
        await using(var target=new SqliteConnection("Data Source="+backup)){await source.OpenAsync();await target.OpenAsync();source.BackupDatabase(target);}
        await using var transaction=await db.Database.BeginTransactionAsync();var updated=new List<DemoAccount>();
        for(var number=1;number<=10;number++)
        {
            var profile=SampleProfiles.Accounts[number-1];var user=await users.FindByIdAsync($"demo-ml-user-{number:00}");
            if(user==null||(!user.FullName.StartsWith("[DEMO]")&&user.FullName!=profile.Name))throw new InvalidOperationException("Cuenta ajena al conjunto ficticio.");
            var credential=credentials.Single(c=>c.Email==user.Email);
            if(!await users.CheckPasswordAsync(user,credential.Password))throw new InvalidOperationException("Archivo privado no coincide con Identity.");
            user.FullName=profile.Name;user.UserName=profile.Email;user.Email=profile.Email;
            if(!(await users.UpdateAsync(user)).Succeeded)throw new InvalidOperationException("No se pudo actualizar el perfil ficticio.");
            updated.Add(new(profile.Email,credential.Password));
        }
        foreach(var row in rows)
        {
            var id=row.GetProperty("id").GetString();
            var article=await db.Articulos.IgnoreQueryFilters().SingleAsync(a=>a.Id==id);
            if(!article.UserId.StartsWith("demo-ml-user-"))throw new InvalidOperationException("No se modifica una publicación ajena.");
            article.Title=row.GetProperty("title").GetString()!;article.Description=row.GetProperty("description").GetString()!;
            article.ImageUrl=row.GetProperty("image").GetString();
            article.ExchangeDetails=article.Modalidad==ModalidadArticulo.Intercambio?"Busco artículos relacionados con estudio, hogar o deporte.":null;
            article.ModeracionVersion=Guid.NewGuid().ToString();
        }
        await db.SaveChangesAsync();
        var pending=Path.Combine(Path.GetDirectoryName(full)!,"accounts.private.pending.json");
        await File.WriteAllTextAsync(pending,JsonSerializer.Serialize(updated,new JsonSerializerOptions {WriteIndented=true}));
        await transaction.CommitAsync();File.Move(pending,privatePath,overwrite:true);
        Console.WriteLine("OK: diez perfiles renombrados, contraseñas conservadas y 48 publicaciones con fotos locales. Respaldo privado conservado; app.db intacta.");
    }
}
