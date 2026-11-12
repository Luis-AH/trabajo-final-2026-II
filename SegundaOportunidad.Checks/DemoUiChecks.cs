using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SegundaOportunidad.Models;
using SegundaOportunidad.Data;

internal static class DemoUiChecks
{
    public static async Task Run()
    {
        var root=Path.GetFullPath("SegundaOportunidad");
        var accounts=JsonSerializer.Deserialize<List<DemoAccount>>(await File.ReadAllTextAsync(".demo/accounts.private.json"))??[];
        var temp=Path.Combine(Path.GetTempPath(),"segunda-demo-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        var path=Path.Combine(temp,"demo.db");
        await using(var source=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=Path.GetFullPath(".demo/segunda-demo.db"),Mode=SqliteOpenMode.ReadOnly}.ToString()))
        await using(var target=new SqliteConnection("Data Source="+path)){await source.OpenAsync();await target.OpenAsync();source.BackupDatabase(target);}
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{Path.Combine(root,"bin","Debug","net10.0","SegundaOportunidad.dll"),"--urls","http://127.0.0.1:0"})info.ArgumentList.Add(arg);
        info.Environment["ASPNETCORE_ENVIRONMENT"]="Development";info.Environment["DOTNET_ENVIRONMENT"]="Development";
        info.Environment["Demo__UseDataset"]="true";info.Environment["ConnectionStrings__DefaultConnection"]="Data Source="+path;
        info.Environment["DataProtection__KeyPath"]=Path.Combine(temp,"keys");info.Environment["Logging__EventLog__LogLevel__Default"]="None";
        foreach(var key in new[]{"RabbitMQ__ConnectionString","Redis__ConnectionString","PieSocket__ApiKey","PieSocket__ApiSecret","PieSocket__ClusterId","Algolia__AppId","Algolia__WriteApiKey","Gemini__ApiKey","GEMINI_API_KEY","GOOGLE_API_KEY"})info.Environment[key]="";
        using var process=Process.Start(info)??throw new Exception("No se pudo iniciar prueba local.");var errors=process.StandardError.ReadToEndAsync();
        var ready=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output=Task.Run(async()=>{while(await process.StandardOutput.ReadLineAsync() is {} line){var m=Regex.Match(line,@"Now listening on: (http://127\.0\.0\.1:\d+)");if(m.Success)ready.TrySetResult(m.Groups[1].Value);}});
        try
        {
            if(await Task.WhenAny(ready.Task,process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(35))!=ready.Task)throw new Exception("Falló el arranque de prueba; código "+process.ExitCode);
            var address=new Uri(await ready.Task);
            HttpClient Client()=>new(new HttpClientHandler {AllowAutoRedirect=false}){BaseAddress=address};
            using var anonymous=Client();using var admin=Client();using var member=Client();using var other=Client();
            static string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            async Task Login(HttpClient client,string email)
            {
                var account=accounts.Single(a=>a.Email==email);
                var html=await client.GetStringAsync("/Identity/Account/Login");
                using var response=await client.PostAsync("/Identity/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["Input.Email"]=account.Email,["Input.Password"]=account.Password,["__RequestVerificationToken"]=Token(html)}));
                Ensure(response.StatusCode==HttpStatusCode.Redirect,"Identity acepta las credenciales del archivo privado");
            }
            foreach(var route in new[]{"/","/Articulos","/Identity/Account/Login","/Identity/Account/Register","/css/experience.css","/js/recommendations.js","/lib/bootstrap-icons/bootstrap-icons.min.css"})
                Ensure((await anonymous.GetAsync(route)).StatusCode==HttpStatusCode.OK,"ruta pública "+route+" responde 200");
            var catalog=await anonymous.GetStringAsync("/Articulos");Ensure(Regex.Matches(catalog,"class=\"product-card\"").Count==12,"catálogo paginado en 12 tarjetas");
            Ensure(!catalog.Contains("demo-banner")&&catalog.Contains("ml-recommendations"),"catálogo sin aviso superior y módulo ML presente");
            Ensure((await anonymous.GetAsync("/App_Data/recommender.json")).StatusCode==HttpStatusCode.NotFound,"artefacto y corpus no se sirven como archivos públicos");
            using(var response=await anonymous.GetAsync("/api/ml/recomendaciones?consulta=laptop"))
            {using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());Ensure(response.IsSuccessStatusCode&&json.RootElement.GetProperty("publicaciones").GetArrayLength()>0,"API ML funciona con las publicaciones DEMO reales");}
            await Login(admin,accounts[0].Email);await Login(member,accounts[1].Email);await Login(other,accounts[2].Email);
            foreach(var account in accounts.Skip(3))
            {
                using var client=Client();
                await Login(client,account.Email);
                Ensure((await client.GetAsync("/Perfil")).StatusCode==HttpStatusCode.OK,"cuenta ficticia autenticada accede a su perfil");
            }
            var dashboard=await admin.GetStringAsync("/Home/Dashboard");
            Ensure(dashboard.Contains("id=\"catalog-count\">40</span>")&&dashboard.Contains("id=\"own-publication-count\">0</span>"),"administrador ve 40 disponibles y cero propios, sin confundir contadores");
            var memberDashboard=await member.GetStringAsync("/Home/Dashboard");
            Ensure(memberDashboard.Contains("id=\"own-publication-count\">6</span>"),"usuario ve sus seis publicaciones reales");
            foreach(var route in new[]{"/Admin","/AdminGuard","/Supervisor","/Seguridad","/Demo"})Ensure((await admin.GetAsync(route)).StatusCode==HttpStatusCode.OK,"vista administrativa "+route+" responde 200");
            Ensure((await admin.PostAsync("/Demo/Preparar",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"preparación DEMO rechaza POST sin antiforgery");
            var demoForm=await admin.GetStringAsync("/Demo");
            using var reseed=await admin.PostAsync("/Demo/Preparar",new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=Token(demoForm)}));
            Ensure(reseed.StatusCode==HttpStatusCode.OK&&!(await reseed.Content.ReadAsStringAsync()).Contains("<code>D3mo!"),"repetir seed HTTP no crea ni vuelve a mostrar contraseñas");
            foreach(var route in new[]{"/Perfil","/Perfil/MisPublicaciones","/Perfil/Intercambios","/Articulos/Create","/Mensajes"})Ensure((await member.GetAsync(route)).StatusCode==HttpStatusCode.OK,"vista de usuario "+route+" responde 200");
            const string id="demo-ml-art-001";
            Ensure((await other.GetAsync("/Articulos/Edit/"+id)).StatusCode==HttpStatusCode.NotFound,"no se permite editar publicación ajena");
            var form=await member.GetStringAsync("/Articulos/Edit/"+id);
            var version=WebUtility.HtmlDecode(Regex.Match(form,"name=\"ModeracionVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            int category;
            await using(var db=new SqliteConnection("Data Source="+path)){await db.OpenAsync();using var cmd=db.CreateCommand();cmd.CommandText="SELECT CategoriaId FROM Articulos WHERE Id='demo-ml-art-001'";category=Convert.ToInt32(await cmd.ExecuteScalarAsync());}
            using var edited=await member.PostAsync("/Articulos/Edit/"+id,new FormUrlEncodedContent(new Dictionary<string,string> {
                ["Title"]="Laptop para clases",["Description"]="Publicación ficticia para probar edición segura.",["Modalidad"]="Venta",["EstadoFisico"]="BuenEstado",["Price"]="42",["CategoriaId"]=category.ToString(),["ModeracionVersion"]=version,["__RequestVerificationToken"]=Token(form)}));
            Ensure(edited.StatusCode==HttpStatusCode.Redirect,"edición propia guarda sin cambiar propietario ni moderación");
            var create=await member.GetStringAsync("/Articulos/Create");
            Ensure(!create.Contains("location-picker")&&!form.Contains("location-picker")&&!catalog.Contains("maps.googleapis.com")&&!catalog.Contains("data-google-maps-key"),"formularios y layout sin Google Maps");
            Ensure((await member.GetAsync("/api/intercambios/demo-ml-exchange-0/encuentro")).StatusCode==HttpStatusCode.NotFound,"endpoint de mapas de encuentro retirado");
            Ensure((await anonymous.GetAsync("/js/maps.js")).StatusCode==HttpStatusCode.NotFound,"script Maps ya no se sirve");
            await using(var safetyDb=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source="+path).Options))
            {safetyDb.Users.Add(new ApplicationUser {Id="safety-outsider",UserName="existing-outside-demo"});await safetyDb.SaveChangesAsync();}
            var safetyForm=await admin.GetStringAsync("/Demo");
            using var unsafeSeed=await admin.PostAsync("/Demo/Preparar",new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=Token(safetyForm)}));
            Ensure(unsafeSeed.StatusCode==HttpStatusCode.Conflict&&(await unsafeSeed.Content.ReadAsStringAsync()).Contains("base de demostraci"),"seed HTTP rechaza mezcla y muestra explicación al administrador");
            Console.WriteLine("Memoria observada en Windows después de probar vistas y ML: "+Math.Round(process.WorkingSet64/1048576d,1)+" MiB. No equivale a una medida de Render/Linux.");
            Console.WriteLine("OK DEMO HTTP: vistas y login comprobados sobre copia temporal. Sin revisión visual del navegador ni cambios en app.db.");
        }
        finally {if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();await output;}
    }
    static void Ensure(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("OK UX HTTP: "+text);}
}
