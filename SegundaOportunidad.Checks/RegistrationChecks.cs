using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SegundaOportunidad.Data;

internal static class RegistrationChecks
{
    public sealed class TestEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email,string subject,string htmlMessage) => Task.CompletedTask;
    }
    static void Check(bool value,string label) { if(!value) throw new Exception(label); Console.WriteLine("OK CUENTA: "+label); }
    public static async Task Run(IServiceProvider services,Uri address)
    {
        using var browser=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,CookieContainer=new CookieContainer() }) { BaseAddress=address };
        string Extract(string html) => WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var form=await browser.GetStringAsync("/Identity/Account/Register");
        Check(form.Contains("Input.FullName") && form.Contains("autocomplete=\"name\""),"Registro solicita nombres y apellidos");
        var token=Extract(form);
        async Task<HttpResponseMessage> Register(string? name)
        {
            var input=new Dictionary<string,string> { ["Input.Email"]="new-fullname@checks.local",["Input.Password"]="Checks123!",["Input.ConfirmPassword"]="Checks123!",["__RequestVerificationToken"]=token };
            if(name!=null) input["Input.FullName"]=name;
            return await browser.PostAsync("/Identity/Account/Register",new FormUrlEncodedContent(input));
        }
        Check((await Register(null)).IsSuccessStatusCode,"Sin nombre completo se mantiene formulario con error");
        using(var scope=services.CreateScope()) Check(!await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AnyAsync(u => u.Email=="new-fullname@checks.local"),"No se crea cuenta sin nombre");
        Check((await Register("Ana")).IsSuccessStatusCode,"Nombre sin apellidos no se acepta");
        Check((await Register("  Ana   María Torres López  ")).StatusCode==HttpStatusCode.Redirect,"Registro completo inicia sesión");
        using(var scope=services.CreateScope())
        {
            var user=await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(u => u.Email=="new-fullname@checks.local");
            Check(user.FullName=="Ana María Torres López" && !user.IsVerified,"Nombre normalizado guardado sin aprobar automáticamente");
        }
        var account=await browser.GetStringAsync("/Verificacion");
        Check(account.Contains("dashboard-account-card") && account.Contains("Cerrar sesión") && account.Contains("/Identity/Account/Logout"),"Cierre de sesión visible sin depender de dropdown");
        var logout=await browser.PostAsync("/Identity/Account/Logout?returnUrl=%2F",new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"]=Extract(account) }));
        Check(logout.StatusCode==HttpStatusCode.Redirect,"Usuario sin verificar puede cerrar sesión por POST");
        Check((await browser.GetAsync("/Home/Dashboard")).StatusCode==HttpStatusCode.Redirect,"Cookie de sesión cerrada ya no accede al dashboard");
    }
}
