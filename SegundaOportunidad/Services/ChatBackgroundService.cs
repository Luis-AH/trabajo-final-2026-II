using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using Microsoft.EntityFrameworkCore;

namespace SegundaOportunidad.Services;

public class ChatBackgroundService(IConfiguration configuration,IServiceProvider services,PieSocketService pieSocket,ILogger<ChatBackgroundService> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uri=configuration["RabbitMQ:ConnectionString"];
        if(string.IsNullOrWhiteSpace(uri)) {logger.LogWarning("RabbitMQ no configurado; mensajería pendiente de configuración.");return;}
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory=new ConnectionFactory {Uri=new Uri(uri),RequestedConnectionTimeout=TimeSpan.FromSeconds(5),AutomaticRecoveryEnabled=false};
                await using var connection=await factory.CreateConnectionAsync(stoppingToken);
                await using var channel=await connection.CreateChannelAsync(cancellationToken:stoppingToken);
                await channel.QueueDeclareAsync("chat_messages",durable:true,exclusive:false,autoDelete:false,arguments:null,cancellationToken:stoppingToken);
                await channel.BasicQosAsync(0,10,false,stoppingToken);
                var consumer=new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync+=async (_,ea)=> {
                    try
                    {
                        var message=JsonSerializer.Deserialize<MessageDto>(ea.Body.Span,new JsonSerializerOptions {PropertyNameCaseInsensitive=true});
                        if(message==null || string.IsNullOrWhiteSpace(message.RabbitMqMessageId) || string.IsNullOrWhiteSpace(message.Content))
                            throw new JsonException("Mensaje no válido.");
                        using var scope=services.CreateScope();
                        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        if(await db.ChatAllowedAsync(message.SenderId,message.ReceiverId,message.SentAt,stoppingToken))
                        {
                            if(!await db.Mensajes.AnyAsync(m=>m.RabbitMqMessageId==message.RabbitMqMessageId,stoppingToken))
                            {
                                db.Mensajes.Add(new Mensaje {RabbitMqMessageId=message.RabbitMqMessageId,Content=message.Content,SenderId=message.SenderId,ReceiverId=message.ReceiverId,SentAt=message.SentAt});
                                await db.SaveChangesAsync(stoppingToken);
                                await pieSocket.PublishMessageAsync("chat_"+message.ReceiverId,"new_message",new {refresh=true});
                            }
                        }
                        await channel.BasicAckAsync(ea.DeliveryTag,false,stoppingToken);
                    }
                    catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { }
                    catch(JsonException)
                    {
                        logger.LogWarning("RabbitMQ recibió un mensaje inválido; se rechaza sin registrar contenido.");
                        await channel.BasicNackAsync(ea.DeliveryTag,false,false,stoppingToken);
                    }
                    catch(Exception ex)
                    {
                        logger.LogWarning("No se pudo persistir mensaje ({Type}); se reencolará.",ex.GetType().Name);
                        await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken);
                        await channel.BasicNackAsync(ea.DeliveryTag,false,true,stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync("chat_messages",false,consumer,stoppingToken);
                logger.LogInformation("RabbitMQ conectado; consumidor de chat activo.");
                while(connection.IsOpen && channel.IsOpen && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(1),stoppingToken);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {break;}
            catch(Exception ex) {logger.LogWarning("RabbitMQ no disponible ({Type}); próximo intento en 10 segundos.",ex.GetType().Name);}
            try {await Task.Delay(TimeSpan.FromSeconds(10),stoppingToken);}
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {break;}
        }
    }
    private sealed class MessageDto
    {
        public string RabbitMqMessageId {get;set;}="";
        public string Content {get;set;}="";
        public string SenderId {get;set;}="";
        public string ReceiverId {get;set;}="";
        public DateTime SentAt {get;set;}
    }
}
