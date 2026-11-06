using Microsoft.Extensions.Configuration;
using SegundaOportunidad.Services;
using SegundaOportunidad.Data;
using RabbitMQ.Client;
using StackExchange.Redis;
using System.Net.Http.Json;

internal static class IntegrationReadChecks
{
    public static async Task Run()
    {
        var config=new ConfigurationBuilder().AddUserSecrets(typeof(ApplicationDbContext).Assembly,optional:true).AddEnvironmentVariables().Build();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var algolia=new AlgoliaService(config);
        if(!algolia.Configurado) Console.WriteLine("ALGOLIA: pendiente, sin configuración.");
        else try {var ids=await algolia.SearchChatIdsAsync("prueba",timeout.Token);Console.WriteLine("ALGOLIA: consulta real satisfactoria, coincidencias="+ids.Count+". Sin indexar ni modificar registros.");}
        catch(Exception ex){Console.WriteLine("ALGOLIA: fallo de consulta real ("+ex.GetType().Name+").");}
        var rabbit=config["RabbitMQ:ConnectionString"];
        if(string.IsNullOrWhiteSpace(rabbit)) Console.WriteLine("RABBITMQ: pendiente, sin configuración.");
        else try {var factory=new ConnectionFactory {Uri=new Uri(rabbit),RequestedConnectionTimeout=TimeSpan.FromSeconds(5)};await using var connection=await factory.CreateConnectionAsync(timeout.Token);Console.WriteLine("RABBITMQ: conexión y autenticación reales satisfactorias. No se crearon colas, enviaron ni consumieron mensajes.");}
        catch(Exception ex){Console.WriteLine("RABBITMQ: fallo de conexión real ("+ex.GetType().Name+").");}
        var redis=config["Redis:ConnectionString"];
        if(string.IsNullOrWhiteSpace(redis)) Console.WriteLine("REDIS: opcional sin configurar; fallback a SQLite.");
        else try {var options=ConfigurationOptions.Parse(redis);options.ConnectTimeout=3000;options.AbortOnConnectFail=true;using var connection=await ConnectionMultiplexer.ConnectAsync(options);await connection.GetDatabase().PingAsync();Console.WriteLine("REDIS: PING real satisfactorio; sin escrituras.");}
        catch(Exception ex){Console.WriteLine("REDIS: fallo de PING real ("+ex.GetType().Name+").");}
        var cluster=config["PieSocket:ClusterId"];
        if(string.IsNullOrWhiteSpace(cluster)) Console.WriteLine("PIESOCKET: pendiente, sin configuración.");
        else try
        {
            using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(10)};
            using var health=await http.GetAsync($"https://{cluster}.piesocket.com/api/health");
            Console.WriteLine("PIESOCKET: health HTTP "+(int)health.StatusCode+" (no certifica publicación).");
            using var auth=await http.PostAsJsonAsync($"https://{cluster}.piesocket.com/api/members",new {key=config["PieSocket:ApiKey"],secret=config["PieSocket:ApiSecret"],channel="audit_no_subscribers"});
            Console.WriteLine("PIESOCKET: consulta members HTTP "+(int)auth.StatusCode+"; cuerpo omitido. No se publicaron mensajes.");
        }
        catch(Exception ex){Console.WriteLine("PIESOCKET: fallo de diagnóstico real ("+ex.GetType().Name+").");}
    }
}
