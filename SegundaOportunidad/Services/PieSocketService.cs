using System.Text;
using System.Text.Json;

namespace SegundaOportunidad.Services
{
    public class PieSocketService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<PieSocketService> _logger;

        public PieSocketService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<PieSocketService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        public async Task PublishMessageAsync<T>(string channelId, string eventName, T data)
        {
            var clusterId = _configuration["PieSocket:ClusterId"];
            var apiKey = _configuration["PieSocket:ApiKey"];
            var apiSecret = _configuration["PieSocket:ApiSecret"];

            if (string.IsNullOrEmpty(clusterId) || clusterId == "free")
            {
                _logger.LogWarning("PieSocket no está configurado correctamente.");
                return;
            }

            var url = $"https://{clusterId}.piesocket.com/api/publish";

            var payload = new
            {
                key = apiKey,
                secret = apiSecret,
                channelId = channelId,
                message = new
                {
                    @event = eventName,
                    data = data
                }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error al publicar en PieSocket: {StatusCode} - {ErrorBody}", response.StatusCode, errorBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción publicando en PieSocket.");
            }
        }
    }
}
