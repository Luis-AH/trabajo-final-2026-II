using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class DemoPasswordChecks
{
    // Solo bajo solicitud explícita: no cambia políticas de Identity ni cuentas reales.
    public static async Task Run(string path)
    {
        var full=DemoSetupChecks.ValidateDemoPath(path);
        if(!File.Exists(full))throw new InvalidOperationException("La base DEMO debe existir; no se crea ni migra ninguna base.");
        var privateFile=Path.Combine(Path.GetDirectoryName(full)!,"accounts.private.json");
        var passwords=SampleProfiles.ReadPasswords(await File.ReadAllTextAsync(privateFile))
            ??throw new InvalidOperationException("Configura primero el archivo privado con las diez cuentas ficticias.");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Demo",ContentRootPath=Path.GetFullPath("SegundaOportunidad")});
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder {DataSource=full,Mode=SqliteOpenMode.ReadWrite}.ToString()));
        builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var app=builder.Build();using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var accounts=new List<ApplicationUser>();
        for(var number=1;number<=10;number++)
        {
            var user=await users.FindByIdAsync($"demo-ml-user-{number:00}");
            var profile=SampleProfiles.Accounts[number-1];
            if(user==null||(user.Email!=$"demo{number:00}@example.invalid"&&user.Email!=profile.Email)||(!user.FullName.StartsWith("[DEMO]")&&user.FullName!=profile.Name))
                throw new InvalidOperationException("No coincide el conjunto de diez cuentas ficticias; no se modificó ninguna contraseña.");
            accounts.Add(user);
        }
        var backup=Path.Combine(Path.GetDirectoryName(full)!,"before-short-passwords-"+Guid.NewGuid().ToString("N")+".db");
        await using(var source=new SqliteConnection("Data Source="+full+";Mode=ReadOnly"))
        await using(var target=new SqliteConnection("Data Source="+backup))
        {await source.OpenAsync();await target.OpenAsync();source.BackupDatabase(target);}
        var credentials=new List<DemoAccount>();
        await using var transaction=await db.Database.BeginTransactionAsync();
        foreach(var user in accounts)
        {
            var password=passwords[user.Email!];
            if(await users.HasPasswordAsync(user)&&!(await users.RemovePasswordAsync(user)).Succeeded)throw new Exception("No se pudo actualizar la cuenta ficticia.");
            if(!(await users.AddPasswordAsync(user,password)).Succeeded||!await users.CheckPasswordAsync(user,password))throw new Exception("Identity rechazó la contraseña DEMO.");
            credentials.Add(new(user.Email!,password));
        }
        var pendingFile=Path.Combine(Path.GetDirectoryName(full)!,"accounts.private.pending.json");
        await File.WriteAllTextAsync(pendingFile,JsonSerializer.Serialize(credentials,new JsonSerializerOptions {WriteIndented=true}));
        await transaction.CommitAsync();
        File.Move(pendingFile,privateFile,overwrite:true);
        Console.WriteLine("OK: diez contraseñas del archivo privado sincronizadas y verificadas por Identity. Sesiones previas invalidadas.");
        Console.WriteLine("Credenciales actualizadas en archivo privado local; respaldo SQLite conservado. Sin cambios en app.db ni cuentas reales.");
    }
}
