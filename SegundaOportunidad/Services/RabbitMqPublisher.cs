using RabbitMQ.Client;
using System.Text.Json;
using System.Text;

namespace SegundaOportunidad.Services;

public sealed class MessagingUnavailableException : Exception
{
    public MessagingUnavailableException():base("La mensajería no está disponible temporalmente.") { }
}
public class RabbitMqPublisher(IConfiguration configuration)
{
    public async Task PublishMessageAsync<T>(string queueName,T message)
    {
        var uri=configuration["RabbitMQ:ConnectionString"];
        if(string.IsNullOrWhiteSpace(uri)) throw new MessagingUnavailableException();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var factory=new ConnectionFactory {Uri=new Uri(uri),RequestedConnectionTimeout=TimeSpan.FromSeconds(5)};
            await using var connection=await factory.CreateConnectionAsync(timeout.Token);
            var options=new CreateChannelOptions(publisherConfirmationsEnabled:true,publisherConfirmationTrackingEnabled:true);
            await using var channel=await connection.CreateChannelAsync(options,timeout.Token);
            await channel.QueueDeclareAsync(queue:queueName,durable:true,exclusive:false,autoDelete:false,arguments:null,cancellationToken:timeout.Token);
            var body=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message,new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
            await channel.BasicPublishAsync(exchange:"",routingKey:queueName,mandatory:true,basicProperties:new BasicProperties {Persistent=true},body:body,cancellationToken:timeout.Token);
        }
        catch(Exception ex) when(ex is not MessagingUnavailableException)
        {
            // No propagar excepción del proveedor: puede contener host/URI o credenciales.
            throw new MessagingUnavailableException();
        }
    }
}
