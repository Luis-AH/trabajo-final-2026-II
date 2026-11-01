using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SegundaOportunidad.Models;
using SegundaOportunidad.Services;

internal static class SecurityChecks
{
    public sealed class Mailbox : IRecoveryEmailService
    {
        public bool Configurado => true;
        public string BaseUrl => "https://example.invalid";
        public string? Link;
        public int Sent;
        public Task EnviarAsync(string email,string url) { Link=url; Sent++; return Task.CompletedTask; }
    }
    static void Check(bool condition,string label) { if(!condition) throw new Exception(label); Console.WriteLine("OK SEGURIDAD: "+label); }
    public static async Task Run(HttpClient admin,HttpClient member,IServiceProvider services)
    {
        async Task<string> Token(HttpClient client,string path)
        {
            var r=await client.GetAsync(path); Check(r.IsSuccessStatusCode,"Formulario "+path.Split('?')[0]);
            return WebUtility.HtmlDecode(Regex.Match(await r.Content.ReadAsStringAsync(),"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        }
        Check((await admin.GetAsync("/Seguridad")).IsSuccessStatusCode,"Admin consulta roles y permisos");
        Check((await member.GetAsync("/Seguridad")).StatusCode==HttpStatusCode.Redirect,"Rol común no accede a seguridad administrativa");
        var token=await Token(admin,"/Seguridad/Cambiar");
        Check((await admin.PostAsync("/Seguridad/Cambiar",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"Cambio requiere antiforgery");
        async Task<HttpResponseMessage> Change(string current) => await admin.PostAsync("/Seguridad/Cambiar",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["Actual"]=current,["Nueva"]="NuevaChecks456!",["Confirmacion"]="NuevaChecks456!",["__RequestVerificationToken"]=token }));
        var wrong=await Change("ContraseñaEquivocada!");
        Check(wrong.IsSuccessStatusCode && !(await wrong.Content.ReadAsStringAsync()).Contains("value=\"ContraseñaEquivocada!\""),"Contraseña incorrecta rechazada sin devolverla al HTML");
        Check((await Change("Checks123!")).StatusCode==HttpStatusCode.Redirect,"Cambio de contraseña propia correcto");
        using(var scope=services.CreateScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Check(await users.CheckPasswordAsync((await users.FindByIdAsync("admin"))!,"NuevaChecks456!") && await users.CheckPasswordAsync((await users.FindByIdAsync("member"))!,"Checks123!"),"Solo cambia la contraseña del administrador actual");
        }
        Check((await admin.GetAsync("/Seguridad")).IsSuccessStatusCode,"Cookie propia renovada tras cambio");
        using var anonymous=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,CookieContainer=new CookieContainer() }) { BaseAddress=admin.BaseAddress };
        var recoverToken=await Token(anonymous,"/Seguridad/Recuperar");
        async Task<HttpResponseMessage> RequestReset(string email) => await anonymous.PostAsync("/Seguridad/Recuperar",new FormUrlEncodedContent(new Dictionary<string,string> { ["Email"]=email,["__RequestVerificationToken"]=recoverToken }));
        var mailbox=(Mailbox)services.GetRequiredService<IRecoveryEmailService>();
        var requested=await RequestReset("member@checks.local");
        Check(requested.StatusCode==HttpStatusCode.Redirect && mailbox.Sent==1,"Recuperación entrega enlace a buzón simulado");
        var unknown=await RequestReset("nobody@checks.local");
        Check(unknown.StatusCode==requested.StatusCode && unknown.Headers.Location==requested.Headers.Location && mailbox.Sent==1,"Respuesta uniforme para correo inexistente");
        var link=new Uri(mailbox.Link!);
        var query=QueryHelpers.ParseQuery(link.Query);
        var code=query["codigo"].ToString();
        var form="/Seguridad/Restablecer?"+link.Query.TrimStart('?');
        var resetToken=await Token(anonymous,form);
        async Task<HttpResponseMessage> Reset(string email) => await anonymous.PostAsync("/Seguridad/Restablecer",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["Email"]=email,["Codigo"]=code,["Nueva"]="Restablecida789!",["Confirmacion"]="Restablecida789!",["__RequestVerificationToken"]=resetToken }));
        Check((await Reset("admin@checks.local")).StatusCode==HttpStatusCode.OK,"Token no sirve para otra cuenta");
        Check((await Reset("member@checks.local")).StatusCode==HttpStatusCode.Redirect,"Restablecimiento HTTP con token Identity válido");
        Check((await Reset("member@checks.local")).StatusCode==HttpStatusCode.OK,"Token consumido no se reutiliza");
        Check((await member.GetAsync("/Home/Dashboard")).StatusCode==HttpStatusCode.Redirect,"Reset invalida la sesión anterior");
        Check((await RequestReset("member@checks.local")).StatusCode==HttpStatusCode.TooManyRequests,"Recuperación limitada por IP");
        using(var scope=services.CreateScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u=(await users.FindByIdAsync("member"))!;
            Check(await users.CheckPasswordAsync(u,"Restablecida789!"),"Nueva contraseña persistida por Identity");
            Check(!await users.VerifyUserTokenAsync(u,TokenOptions.DefaultProvider,"ResetPassword","token-manipulado"),"Token manipulado rechazado");
            Check(scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan==TimeSpan.FromHours(1),"Caducidad configurada en una hora");
            var expired=await users.GeneratePasswordResetTokenAsync(u);
            var tokenOptions=scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
            var lifespan=tokenOptions.TokenLifespan;
            try {
                tokenOptions.TokenLifespan=TimeSpan.FromSeconds(-1);
                Check(!await users.VerifyUserTokenAsync(u,TokenOptions.DefaultProvider,"ResetPassword",expired),"Token caducado rechazado por el proveedor Identity");
            } finally { tokenOptions.TokenLifespan=lifespan; }
        }
        Check((await anonymous.GetAsync("/Identity/Account/ForgotPassword")).StatusCode==HttpStatusCode.Redirect,"Ruta Identity antigua usa recuperación unificada");
    }
}
