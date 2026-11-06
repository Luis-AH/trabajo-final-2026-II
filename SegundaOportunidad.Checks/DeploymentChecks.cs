using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SegundaOportunidad.Services;

internal static class DeploymentChecks
{
    public static async Task Run()
    {
        var temp=Path.Combine(Path.GetTempPath(),"segunda-audit-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Production"});
        builder.Logging.ClearProviders();
        builder.Configuration["DataProtection:KeyPath"]=Path.Combine(temp,"keys");
        builder.Configuration["ReverseProxy:KnownProxies:0"]="192.0.2.10";
        builder.AddDeploymentConfiguration("Data Source="+Path.Combine(temp,"app.db"));
        using var provider=builder.Services.BuildServiceProvider();
        var protector=provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("audit");
        var token=protector.Protect("temporary-test");
        var second=DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(temp,"keys")),o=>o.SetApplicationName("SegundaOportunidad")).CreateProtector("audit");
        Ensure(second.Unprotect(token)=="temporary-test","Data Protection conserva tokens entre proveedores/reinicios");
        var options=provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var middleware=new ForwardedHeadersMiddleware(_=>Task.CompletedTask,provider.GetRequiredService<ILoggerFactory>(),options);
        var trusted=new DefaultHttpContext();trusted.Connection.RemoteIpAddress=IPAddress.Parse("192.0.2.10");trusted.Request.Scheme="http";trusted.Request.Headers["X-Forwarded-Proto"]="https";
        await middleware.Invoke(trusted);
        Ensure(trusted.Request.Scheme=="https","proxy autorizado transmite HTTPS");
        var forged=new DefaultHttpContext();forged.Connection.RemoteIpAddress=IPAddress.Parse("198.51.100.30");forged.Request.Scheme="http";forged.Request.Headers["X-Forwarded-Proto"]="https";
        await middleware.Invoke(forged);
        Ensure(forged.Request.Scheme=="http","no se aceptan cabeceras de proxy desconocido");
        var captured="";
        var handler=new Handler(async request=>{captured=await request.Content!.ReadAsStringAsync();return new HttpResponseMessage(HttpStatusCode.OK);});
        using var http=new HttpClient(handler);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["PieSocket:ClusterId"]="test.cluster",["PieSocket:ApiKey"]="public-test",["PieSocket:ApiSecret"]="private-test" }).Build();
        var factory=new Factory(http);
        await new PieSocketService(config,factory,Microsoft.Extensions.Logging.Abstractions.NullLogger<PieSocketService>.Instance)
            .PublishMessageAsync("chat_test","new_message",new {content="PRIVATE CHAT",senderId="private-user"});
        using var json=JsonDocument.Parse(captured);
        Ensure(json.RootElement.GetProperty("channel").GetString()=="chat_test" && !json.RootElement.TryGetProperty("channelId",out _),"PieSocket usa channel según API v3");
        Ensure(!captured.Contains("PRIVATE CHAT") && !captured.Contains("private-user") && json.RootElement.GetProperty("message").GetProperty("data").GetProperty("refresh").GetBoolean(),"PieSocket no difunde conversaciones privadas");
        try {await new RabbitMqPublisher(new ConfigurationBuilder().Build()).PublishMessageAsync("test",new {text="test"});throw new Exception("Aceptó envío sin broker");}
        catch(MessagingUnavailableException){Console.WriteLine("OK RENDER: ausencia de broker produce error controlado, sin falso envío exitoso");}
        var brokerConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["RabbitMQ:ConnectionString"]="amqp://guest:guest@127.0.0.1:1/" }).Build();
        using(var worker=new ChatBackgroundService(brokerConfig,provider,new PieSocketService(config,factory,Microsoft.Extensions.Logging.Abstractions.NullLogger<PieSocketService>.Instance),Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatBackgroundService>.Instance))
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            await worker.StartAsync(CancellationToken.None);
            Ensure(watch.Elapsed<TimeSpan.FromSeconds(2),"broker caído no bloquea el arranque web");
            await worker.StopAsync(CancellationToken.None);
        }        Console.WriteLine("Pruebas de despliegue aprobadas, archivos temporales fuera del repositorio; sin servicios externos.");
    }
    static void Ensure(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("OK RENDER: "+message);}
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request);}
    private sealed class Factory(HttpClient http):IHttpClientFactory {public HttpClient CreateClient(string name)=>http;}
}
