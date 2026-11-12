using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;

internal static class RenderCatalogChecks
{
    public static async Task Run()
    {
        var root=Path.GetFullPath("SegundaOportunidad");
        var temp=Path.Combine(Path.GetTempPath(),"segunda-render-catalog-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        var database=Path.Combine(temp,"catalog.db");
        var credentials=SampleProfiles.Accounts.Select(p=>new DemoAccount(p.Email,"Check!"+Guid.NewGuid().ToString("N"))).ToList();
        for(var run=0;run<2;run++)
        {
            var info=new ProcessStartInfo("dotnet") {WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{Path.Combine(root,"bin","Debug","net10.0","SegundaOportunidad.dll"),"--urls","http://127.0.0.1:0"})info.ArgumentList.Add(arg);
            info.Environment["ASPNETCORE_ENVIRONMENT"]="Demo";info.Environment["DOTNET_ENVIRONMENT"]="Demo";
            // Reproduce el despliegue que recibía una ruta relativa y ninguna ruta de claves.
            info.Environment["ConnectionStrings__DefaultConnection"]="Data Source="+Path.GetRelativePath(root,database);
            info.Environment["DataProtection__KeyPath"]="";
            info.Environment["SampleData__InitializeOnStartup"]="true";
            info.Environment["SampleData__CredentialsJson"]=run==0?"":JsonSerializer.Serialize(credentials);
            info.Environment["Logging__EventLog__LogLevel__Default"]="None";
            foreach(var key in new[]{"BootstrapAdmin__Email","BootstrapAdmin__Password","RabbitMQ__ConnectionString","Redis__ConnectionString","PieSocket__ApiKey","PieSocket__ApiSecret","PieSocket__ClusterId","Algolia__AppId","Algolia__WriteApiKey","Gemini__ApiKey","GEMINI_API_KEY","GOOGLE_API_KEY"})info.Environment[key]="";
            using var process=Process.Start(info)??throw new Exception("No se pudo iniciar la app de ensayo.");
            var error=process.StandardError.ReadToEndAsync();var ready=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output=Task.Run(async()=>{while(await process.StandardOutput.ReadLineAsync() is {} line){var match=Regex.Match(line,@"Now listening on: (http://127\.0\.0\.1:\d+)");if(match.Success)ready.TrySetResult(match.Groups[1].Value);}});
            try
            {
                if(await Task.WhenAny(ready.Task,process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(35))!=ready.Task)
                    throw new Exception("Falló el arranque del catálogo de ensayo; diagnóstico: "+await error);
                using var http=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false}) {BaseAddress=new Uri(await ready.Task)};
                http.DefaultRequestHeaders.Add("X-Forwarded-Proto","https");
                Ensure((await http.GetAsync("/health")).StatusCode==HttpStatusCode.OK,run==0?"arranque público sin credenciales ni APIs externas":"arranque con credenciales privadas configuradas");
                var catalog=await http.GetStringAsync("/Articulos");
                Ensure(catalog.Contains((run==0?"40":"41")+" publicaciones disponibles"),"total público correcto tras arranque "+(run+1));
                Ensure(!catalog.Contains("demo-banner")&&!catalog.Contains("TF-IDF /"),"textos solicitados retirados");
                Ensure((await http.GetAsync("/images/catalog/libros.jpg")).StatusCode==HttpStatusCode.OK,"fotografía empaquetada accesible");
                using var recommendations=JsonDocument.Parse(await http.GetStringAsync("/api/ml/recomendaciones?consulta=libros&cantidad=4"));
                Ensure(recommendations.RootElement.GetProperty("publicaciones").GetArrayLength()>0,"Machine Learning entrega publicaciones reales sin depender de APIs externas");
                await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source="+database).Options);
                Ensure(await db.Users.CountAsync(u=>u.Id.StartsWith("demo-ml-user-"))==10&&await db.Articulos.IgnoreQueryFilters().CountAsync(a=>a.Id.StartsWith("demo-ml-art-"))==48,"diez cuentas y 48 artículos sin duplicación");
                var user=await db.Users.SingleAsync(u=>u.Id=="demo-ml-user-01");
                if(run==0)
                {
                    Ensure(!string.IsNullOrEmpty(user.PasswordHash),"cuentas protegidas con claves aleatorias cuando falta el secreto");
                    db.Users.Add(new ApplicationUser {Id="new-after-seed",UserName="new-after-seed"});
                    db.Articulos.Add(new Articulo {Id="new-after-seed",Title="Nueva publicación de ensayo",UserId=user.Id,CategoriaId=await db.Categorias.Select(c=>c.Id).FirstAsync()});
                    await db.SaveChangesAsync();
                    Ensure((await http.GetStringAsync("/Articulos")).Contains("41 publicaciones disponibles"),"contador público refleja una publicación nueva");
                    continue;
                }
                Ensure(new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user,user.PasswordHash!,credentials[0].Password)!=PasswordVerificationResult.Failed,run==0?"la clave privada configurada coincide con Identity":"reiniciar sincroniza la clave nueva del secreto con la cuenta existente");
                // Secure cookies se transportan manualmente en el ensayo HTTP local del proxy HTTPS.
                using var loginPage=await http.GetAsync("/Identity/Account/Login");
                var html=await loginPage.Content.ReadAsStringAsync();
                var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
                var cookies=loginPage.Headers.GetValues("Set-Cookie").Select(v=>v.Split(';')[0]).ToList();
                using var loginRequest=new HttpRequestMessage(HttpMethod.Post,"/Identity/Account/Login");
                loginRequest.Headers.Add("Cookie",string.Join("; ",cookies));
                loginRequest.Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"Input.Email",credentials[0].Email},{"Input.Password",credentials[0].Password},{"__RequestVerificationToken",token}});
                using var login=await http.SendAsync(loginRequest);
                Ensure(login.StatusCode==HttpStatusCode.Redirect&&login.Headers.TryGetValues("Set-Cookie",out var authCookies),"login real con secreto externo funciona en Demo");
                cookies.AddRange(login.Headers.GetValues("Set-Cookie").Select(v=>v.Split(';')[0]));
                async Task<string> Dashboard()
                {using var request=new HttpRequestMessage(HttpMethod.Get,"/Home/Dashboard");request.Headers.Add("Cookie",string.Join("; ",cookies));using var response=await http.SendAsync(request);Ensure(response.StatusCode==HttpStatusCode.OK,"dashboard autenticado responde 200");return await response.Content.ReadAsStringAsync();}
                var dashboard=await Dashboard();
                Ensure(dashboard.Contains("id=\"catalog-count\">"+(run==0?40:41)+"</span>"),"contador del inicio coincide con el catálogo");
                Ensure(await db.Users.AnyAsync(u=>u.Id=="new-after-seed")&&await db.Articulos.AnyAsync(a=>a.Id=="new-after-seed"),"reinicio conserva nuevas cuentas y publicaciones sin reseed");
            }
            finally {if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();await output;}
        }
        var blocked=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Production"});
        blocked.Configuration["SampleData:InitializeOnStartup"]="true";
        await using var blockedApp=blocked.Build();var denied=false;
        try {await SampleStartupInitializer.InitializeAsync(blockedApp.Services,blocked.Environment,blocked.Configuration);}catch(InvalidOperationException ex) when(ex.Message.Contains("Production")){denied=true;}
        Ensure(denied,"autoinicialización prohibida en Production");
    }
    static void Ensure(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("OK RENDER CATALOGO: "+message);}
}
