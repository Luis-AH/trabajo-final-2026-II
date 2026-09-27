using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using SegundaOportunidad.Data;
using SegundaOportunidad.Models;
using Microsoft.EntityFrameworkCore;

namespace SegundaOportunidad.Services
{
    public class ChatBackgroundService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly PieSocketService _pieSocketService;
        private readonly ILogger<ChatBackgroundService> _logger;
        private IConnection? _connection;
        private IChannel? _channel;
        private const string QueueName = "chat_messages";

        public ChatBackgroundService(IConfiguration configuration, IServiceProvider serviceProvider, PieSocketService pieSocketService, ILogger<ChatBackgroundService> logger)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _pieSocketService = pieSocketService;
            _logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var connectionString = _configuration["RabbitMQ:ConnectionString"];
            if (string.IsNullOrEmpty(connectionString)) return;

            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(connectionString)
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await _channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("RabbitMQ conectado y cola declarada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error conectando a RabbitMQ en ChatBackgroundService.");
            }

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null) return;

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);
                
                try
                {
                    var msgDto = JsonSerializer.Deserialize<MessageDto>(messageJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (msgDto != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Idempotency check
                        var exists = await context.Mensajes.AnyAsync(m => m.RabbitMqMessageId == msgDto.RabbitMqMessageId, stoppingToken);
                        
                        if (!exists)
                        {
                            var newMsg = new Mensaje
                            {
                                RabbitMqMessageId = msgDto.RabbitMqMessageId,
                                Content = msgDto.Content,
                                SenderId = msgDto.SenderId,
                                ReceiverId = msgDto.ReceiverId,
                                SentAt = msgDto.SentAt,
                                IsRead = false
                            };
                            
                            context.Mensajes.Add(newMsg);
                            await context.SaveChangesAsync(stoppingToken);
                            
                            _logger.LogInformation("Mensaje guardado exitosamente: {MsgId}", newMsg.Id);
                            
                            // Broadcast to PieSocket
                            var channelId = $"chat_{msgDto.ReceiverId}";
                            await _pieSocketService.PublishMessageAsync(channelId, "new_message", msgDto);
                        }
                        else
                        {
                            _logger.LogInformation("Mensaje duplicado detectado (idempotencia), se omite: {RabbitMsgId}", msgDto.RabbitMqMessageId);
                        }
                    }

                    // Acknowledge the message
                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando mensaje. Nack.");
                    // In real world, maybe nack or send to DLQ. 
                    // For now, we ack to avoid infinite loop of poison messages, or nack with requeue=false.
                    await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null)
            {
                await _channel.CloseAsync(cancellationToken: cancellationToken);
                await _channel.DisposeAsync();
            }
            if (_connection != null)
            {
                await _connection.CloseAsync(cancellationToken: cancellationToken);
                await _connection.DisposeAsync();
            }
            await base.StopAsync(cancellationToken);
        }

        private class MessageDto
        {
            public string RabbitMqMessageId { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string SenderId { get; set; } = string.Empty;
            public string ReceiverId { get; set; } = string.Empty;
            public DateTime SentAt { get; set; }
        }
    }
}
