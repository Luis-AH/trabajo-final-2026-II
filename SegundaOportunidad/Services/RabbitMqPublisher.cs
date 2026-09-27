using RabbitMQ.Client;
using System.Text.Json;
using System.Text;

namespace SegundaOportunidad.Services
{
    public class RabbitMqPublisher
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public RabbitMqPublisher(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration["RabbitMQ:ConnectionString"] ?? throw new ArgumentNullException("RabbitMQ:ConnectionString");
        }

        public async Task PublishMessageAsync<T>(string queueName, T message)
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_connectionString)
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var body = Encoding.UTF8.GetBytes(messageJson);

            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body);
        }
    }
}
