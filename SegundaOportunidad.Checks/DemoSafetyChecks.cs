using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class DemoSafetyChecks
{
    public static async Task Run()
    {
        foreach(var protectedPath in new[]{"SegundaOportunidad/app.db","SegundaOportunidad/APP.DB","outside-demo.db"})
        {
            var rejected=false;
            try {DemoSetupChecks.ValidateDemoPath(protectedPath);}catch(InvalidOperationException){rejected=true;}
            Ensure(rejected,"CLI rechaza destino antes de migrar: "+protectedPath);
        }
        foreach(var scenario in new[]{"isolated","real-user","real-post","corpus-failure","Production"})
        {
            var root=Path.Combine(Path.GetTempPath(),"segunda-demo-safety-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root,"App_Data"));
            var corpus=JsonNode.Parse(await File.ReadAllTextAsync("ml/demo-corpus.json"))!;
            if(scenario=="corpus-failure")corpus.AsArray()[47]!["mode"]="InvalidModeForRollbackCheck";
            await File.WriteAllTextAsync(Path.Combine(root,"App_Data","demo-corpus.json"),corpus.ToJsonString());
            var builder=WebApplication.CreateBuilder(new WebApplicationOptions {ContentRootPath=root,EnvironmentName=scenario=="Production"?"Production":"Demo"});
            builder.Logging.ClearProviders();
            await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
            builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(connection));
            builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddScoped<DemoCatalogSeeder>();
            await using var app=builder.Build();using var scope=app.Services.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();await db.Database.EnsureCreatedAsync();
            if(scenario=="isolated")
            {
                // Render necesita conservar la cuenta de bootstrap, sin convertirla en DEMO.
                var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                Ensure((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded,"rol de bootstrap temporal");
                var bootstrap=new ApplicationUser {Id="bootstrap",UserName="bootstrap@example.invalid",Email="bootstrap@example.invalid"};
                Ensure((await users.CreateAsync(bootstrap,"Temporary!"+Guid.NewGuid().ToString("N"))).Succeeded,"bootstrap temporal");
                Ensure((await users.AddToRoleAsync(bootstrap,"Admin")).Succeeded,"rol del bootstrap");
            }
            if(scenario=="real-user") {db.Users.Add(new ApplicationUser {Id="existing-real",UserName="preserve"});await db.SaveChangesAsync();}
            if(scenario=="real-post")
            {
                db.Users.Add(new ApplicationUser {Id="demo-ml-user-01",UserName="preserve"});
                db.Categorias.Add(new Categoria {Id=1,Name="Existing"});
                db.Articulos.Add(new Articulo {Id="existing-post",UserId="demo-ml-user-01",CategoriaId=1,Title="Preserve original"});await db.SaveChangesAsync();
            }
            var beforeUsers=await db.Users.CountAsync();var beforePosts=await db.Articulos.IgnoreQueryFilters().CountAsync();var beforeCategories=await db.Categorias.CountAsync();
            var seed=scope.ServiceProvider.GetRequiredService<DemoCatalogSeeder>();
            if(scenario=="isolated")
            {
                var first=await seed.SeedAsync();var again=await seed.SeedAsync();
                Ensure(first.NuevasCuentas==10&&first.NuevasPublicaciones==48&&first.NuevosIntercambios==6,"conjunto completo en base separada");
                Ensure(again.NuevasCuentas==0&&again.NuevasPublicaciones==0&&again.NuevosIntercambios==0,"repetición idempotente");
                Ensure(await db.Users.AnyAsync(u=>u.Id=="bootstrap"&&u.Email=="bootstrap@example.invalid"),"bootstrap conservado");
            }
            else
            {
                bool rejected=false;
                try {await seed.SeedAsync();}
                catch(DemoSeedSafetyException) when(scenario is "real-user" or "real-post"){rejected=true;}
                catch(ArgumentException) when(scenario=="corpus-failure"){rejected=true;}
                catch(InvalidOperationException) when(scenario=="Production"){rejected=true;}
                Ensure(rejected,"rechazo esperado: "+scenario);
                db.ChangeTracker.Clear();
                Ensure(await db.Users.CountAsync()==beforeUsers&&await db.Articulos.IgnoreQueryFilters().CountAsync()==beforePosts
                    &&await db.Categorias.CountAsync()==beforeCategories&&await db.Intercambios.CountAsync()==0,"sin escrituras parciales: "+scenario);
            }
        }
    }
    private static void Ensure(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("OK DEMO SAFETY: "+message);}
}
