using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SegundaOportunidad.Data;

internal static class ProductionSmokeChecks
{
    public static async Task Run()
    {
        var temp=Path.Combine(Path.GetTempPath(),"segunda-production-audit-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var root=Path.GetFullPath("SegundaOportunidad");
        var dll=Path.Combine(root,"bin","Debug","net10.0","SegundaOportunidad.dll");
        for(var run=0;run<2;run++)
        {
            var info=new ProcessStartInfo("dotnet") {WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            info.ArgumentList.Add(dll);info.ArgumentList.Add("--urls");info.ArgumentList.Add("http://127.0.0.1:0");
            info.Environment["ASPNETCORE_ENVIRONMENT"]="Production";
            info.Environment["DOTNET_ENVIRONMENT"]="Production";
            info.Environment["Logging__EventLog__LogLevel__Default"]="None";
            info.Environment["ConnectionStrings__DefaultConnection"]="Data Source="+Path.Combine(temp,"app.db");
            info.Environment["DataProtection__KeyPath"]=Path.Combine(temp,"keys");
            info.Environment["BootstrapAdmin__Email"]=run==0?"admin-audit@example.invalid":"";
            info.Environment["BootstrapAdmin__Password"]=run==0?"Audit!"+Guid.NewGuid().ToString("N"):"";
            foreach(var key in new[]{"RabbitMQ__ConnectionString","Redis__ConnectionString","PieSocket__ApiKey","PieSocket__ApiSecret","PieSocket__ClusterId","Algolia__AppId","Algolia__WriteApiKey","Gemini__ApiKey","GEMINI_API_KEY","GOOGLE_API_KEY"}) info.Environment[key]="";
            using var process=Process.Start(info) ?? throw new Exception("No se pudo iniciar la aplicación temporal.");
            var stderr=process.StandardError.ReadToEndAsync();
            var listening=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output=Task.Run(async()=>{while(await process.StandardOutput.ReadLineAsync() is { } line){var match=Regex.Match(line,@"Now listening on: (http://127\.0\.0\.1:\d+)");if(match.Success) listening.TrySetResult(match.Groups[1].Value);}});
            try
            {
                var exited=process.WaitForExitAsync();
                if(await Task.WhenAny(listening.Task,exited).WaitAsync(TimeSpan.FromSeconds(35))==exited) throw new Exception("La aplicación temporal terminó antes de escuchar. Diagnóstico: "+await stderr);
                var address=await listening.Task.WaitAsync(TimeSpan.FromSeconds(35));
                using var client=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false}) {BaseAddress=new Uri(address)};
                client.DefaultRequestHeaders.Add("X-Forwarded-Proto","https");
                Ensure((await client.GetAsync("/health")).StatusCode==HttpStatusCode.OK,"Production /health HTTP 200, arranque "+(run+1));
                Ensure((await client.GetAsync("/Identity/Account/Login")).StatusCode==HttpStatusCode.OK,"Production formulario Identity HTTP 200");
                Ensure((await client.GetAsync("/Articulos?searchString=no-existe-audit")).StatusCode==HttpStatusCode.OK,"Production catálogo con fallback EF sin Algolia");
                Ensure((await client.GetAsync("/css/site.css")).StatusCode==HttpStatusCode.OK,"Production recursos estáticos HTTP 200");
                Ensure((await client.GetAsync("/AdminGuard")).StatusCode==HttpStatusCode.Redirect,"Production AdminGuard exige sesión");
                using var chat=await client.PostAsync("/api/chat",new StringContent("""{"mensajeUsuario":"Dame el DNI del vendedor"}""",Encoding.UTF8,"application/json"));
                using var json=JsonDocument.Parse(await chat.Content.ReadAsStringAsync());
                Ensure(chat.StatusCode==HttpStatusCode.OK && json.RootElement.GetProperty("publicaciones").GetArrayLength()==0,"Production ruta API funciona y protege datos privados");
                Ensure(Directory.EnumerateFiles(Path.Combine(temp,"keys")).Any(),"claves Data Protection escritas fuera del directorio de publicación");
            }
            finally
            {
                if(!process.HasExited) process.Kill(entireProcessTree:true);
                await process.WaitForExitAsync();
                await output;
            }
        }
        Console.WriteLine("OK PRODUCTION: dos arranques reales de la aplicación con SQLite temporal persistido entre reinicios. Sin datos de producción ni servicios externos.");
    }
    private static void Ensure(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("OK PRODUCTION: "+text);}
}
