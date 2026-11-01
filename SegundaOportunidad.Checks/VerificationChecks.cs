using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class VerificationChecks
{
    static void Check(bool value,string label) { if(!value) throw new Exception(label); Console.WriteLine("OK VERIFICACIÓN: "+label); }
    public static async Task Run(HttpClient admin,IServiceProvider services)
    {
        using(var scope=services.CreateScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Check((await users.CreateAsync(new ApplicationUser { Id="pending",UserName="pending@checks.local",Email="pending@checks.local",EmailConfirmed=true },"Checks123!")).Succeeded,"Cuenta nueva sin verificación");
        }
        using var pending=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,CookieContainer=new CookieContainer() }) { BaseAddress=admin.BaseAddress };
        async Task<string> Token(HttpClient client,string path)
        {
            var page=await client.GetAsync(path);
            Check(page.IsSuccessStatusCode,"Formulario "+path);
            return WebUtility.HtmlDecode(Regex.Match(await page.Content.ReadAsStringAsync(),"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        }
        var loginToken=await Token(pending,"/Identity/Account/Login");
        Check((await pending.PostAsync("/Identity/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string> { ["Input.Email"]="pending@checks.local",["Input.Password"]="Checks123!",["__RequestVerificationToken"]=loginToken }))).StatusCode==HttpStatusCode.Redirect,"Cuenta no verificada puede iniciar sesión");
        Check((await pending.GetAsync("/Articulos")).IsSuccessStatusCode && (await pending.GetAsync("/Articulos/Details/art")).IsSuccessStatusCode,"Puede explorar catálogo y detalle");
        foreach(var path in new[] {"/Articulos/Create","/Mensajes","/Reportes/Crear?usuarioId=admin","/Perfil/Intercambios"})
        {
            var response=await pending.GetAsync(path);
            Check(response.StatusCode==HttpStatusCode.Redirect && response.Headers.Location!.ToString().Contains("Verificacion"),"Acceso directo bloqueado: "+path.Split('?')[0]);
        }
        var token=await Token(pending,"/Verificacion");
        foreach(var path in new[] {"/Articulos/Create","/Mensajes/SendMessage","/Intercambios/Proponer","/Intercambios/Aceptar","/Intercambios/Rechazar","/Intercambios/Completar","/Reportes/Crear"})
        {
            var response=await pending.PostAsync(path,new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"]=token }));
            Check(response.StatusCode==HttpStatusCode.Forbidden,"POST de negocio bloqueado: "+path);
        }
        await pending.PostAsync("/Verificacion/Solicitar",new FormUrlEncodedContent(new Dictionary<string,string> { ["dni"]="abc",["__RequestVerificationToken"]=token }));
        using(var scope=services.CreateScope()) Check((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(u => u.Id=="pending")).Dni==null,"DNI inválido no se guarda");
        await pending.PostAsync("/Verificacion/Solicitar",new FormUrlEncodedContent(new Dictionary<string,string> { ["dni"]="12345678",["nombreCompleto"]="Usuario Pendiente Prueba",["__RequestVerificationToken"]=token }));
        var supervisorToken=await Token(admin,"/Supervisor");
        Check((await admin.PostAsync("/Supervisor/Approve",new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]="pending",["__RequestVerificationToken"]=supervisorToken }))).StatusCode==HttpStatusCode.Redirect,"Administrador aprueba solicitud real");
        Check((await pending.GetAsync("/Articulos/Create")).IsSuccessStatusCode,"Sesión existente puede publicar después de aprobación");
        Check((await pending.GetAsync("/Reportes/Crear?usuarioId=admin")).IsSuccessStatusCode,"Aprobación habilita reportes");
        using(var scope=services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u=await db.Users.SingleAsync(u => u.Id=="pending");
            Check(u.IsVerified && u.VerifiedAtUtc!=null && u.VerifiedByAdminId=="admin","Fecha y administrador de aprobación persistidos");
            var cutoff=DateTime.UtcNow;
            db.EstadosSistema.Add(new EstadoSistema { Id="chat-reset",FechaUtc=cutoff });
            await db.SaveChangesAsync();
            Check(!await db.ChatAllowedAsync("admin","pending",cutoff.AddMinutes(-1)),"Mensaje anterior a limpieza no reaparece");
            Check(await db.ChatAllowedAsync("admin","pending",cutoff.AddMinutes(1)),"Conversación nueva permitida entre aprobados");
        }
        await admin.PostAsync("/Supervisor/Reject",new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]="pending",["__RequestVerificationToken"]=supervisorToken }));
        Check((await pending.GetAsync("/Articulos/Create")).StatusCode==HttpStatusCode.Redirect,"Retirar aprobación bloquea sesión existente");
        using(var scope=services.CreateScope()) Check(!await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ChatAllowedAsync("admin","pending",DateTime.UtcNow.AddMinutes(2)),"Conversación con destinatario sin aprobar se rechaza");
    }
}
