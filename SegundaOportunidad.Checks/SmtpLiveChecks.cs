using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class SmtpLiveChecks
{
    public static async Task Run()
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName=typeof(ApplicationDbContext).Assembly.GetName().Name,
            EnvironmentName="Development",ContentRootPath=Path.GetFullPath("SegundaOportunidad") });
        builder.Logging.ClearProviders();
        await using var connection=new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddDefaultIdentity<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddAccountSecurity(builder.Configuration);
        await using var app=builder.Build();
        using var scope=app.Services.CreateScope();
        var options=scope.ServiceProvider.GetRequiredService<IOptions<RecoveryEmailOptions>>().Value;
        if(!string.Equals(options.Host,"sandbox.smtp.mailtrap.io",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Esta prueba automática solo envía a Mailtrap Sandbox; no se envió ningún correo.");
        var email=scope.ServiceProvider.GetRequiredService<IRecoveryEmailService>();
        if(!email.Configurado) throw new InvalidOperationException("Configuración SMTP incompleta o URL pública no válida.");
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user=new ApplicationUser { UserName="prueba-recuperacion@example.test",Email="prueba-recuperacion@example.test",EmailConfirmed=true };
        if(!(await users.CreateAsync(user,"PruebaTemporal123!")).Succeeded) throw new Exception("No se pudo crear la cuenta temporal de prueba.");
        var token=await users.GeneratePasswordResetTokenAsync(user);
        var url=QueryHelpers.AddQueryString(email.BaseUrl+"/Seguridad/Restablecer",new Dictionary<string,string?> {
            ["email"]=user.Email,["codigo"]=WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)) });
        await email.EnviarAsync(user.Email!,url);
        Console.WriteLine("OK SMTP REAL: Mailtrap Sandbox aceptó el mensaje de recuperación por SMTP con TLS y autenticación.");
        Console.WriteLine("Destinatario de prueba: prueba-recuperacion@example.test. Busca el asunto Recuperar acceso a SegundaOportunidad en tu Sandbox.");
        if(!(await users.ResetPasswordAsync(user,token,"NuevaTemporal456!")).Succeeded) throw new Exception("Token de prueba no válido.");
        if((await users.ResetPasswordAsync(user,token,"OtraTemporal789!")).Succeeded) throw new Exception("Token reutilizado inesperadamente.");
        Console.WriteLine("OK IDENTITY: token de la cuenta temporal válido y de un solo uso. No se modificó app.db ni ninguna contraseña real.");
        Console.WriteLine("El enlace enviado pertenece exclusivamente a esta base temporal y no restablece cuentas de la página real.");
    }
}
