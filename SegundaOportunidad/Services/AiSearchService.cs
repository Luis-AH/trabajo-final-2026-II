using System.Net;
using System.Text;
using System.Text.Json;

namespace SegundaOportunidad.Services
{
    public class AiSearchService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiSearchService> _logger;

        public AiSearchService(IConfiguration configuration, HttpClient httpClient, ILogger<AiSearchService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GetCompletionAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
        {
            var apiKey = _configuration["Gemini:ApiKey"]
                ?? _configuration["GEMINI_API_KEY"]
                ?? _configuration["GOOGLE_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Gemini no está configurado. Defina Gemini:ApiKey o la variable GEMINI_API_KEY en el entorno.");
                return "El asistente no está configurado. Contacta al administrador de la plataforma.";
            }

            var model = _configuration["Gemini:Model"] ?? "gemini-3.8-flash";
            var fallbackModel = _configuration["Gemini:FallbackModel"] ?? "gemini-3.7-flash";

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = userMessage } } } },
                generationConfig = new { maxOutputTokens = 256, temperature = 0.5 }
            };
            var serializedRequest = JsonSerializer.Serialize(requestBody);

            try
            {
                var response = await SendRequestAsync(apiKey, model, serializedRequest, cancellationToken);
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable &&
                    !string.Equals(model, fallbackModel, StringComparison.OrdinalIgnoreCase))
                {
                    var initialError = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Gemini model {Model} está temporalmente no disponible (503). Se reintentará con {FallbackModel}. Detalle: {ErrorBody}", model, fallbackModel, initialError);
                    response.Dispose();
                    response = await SendRequestAsync(apiKey, fallbackModel, serializedRequest, cancellationToken);
                }

                using (response)
                {
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Gemini devolvió HTTP {StatusCode}: {ErrorBody}", (int)response.StatusCode, errorBody);
                    return response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "El asistente no pudo autenticarse con Gemini. Contacta al administrador.",
                        HttpStatusCode.NotFound => "El modelo de Gemini configurado no está disponible. Contacta al administrador.",
                        HttpStatusCode.BadRequest => "Gemini rechazó la solicitud (HTTP 400). Revisa en Render los logs de Gemini para ver si la clave, el modelo o los parámetros son incorrectos.",
                        HttpStatusCode.TooManyRequests => "El asistente está ocupado por el momento. Inténtalo de nuevo en unos minutos.",
                        HttpStatusCode.ServiceUnavailable => "Gemini está temporalmente saturado. Inténtalo de nuevo en un momento.",
                        _ => $"Gemini respondió con HTTP {(int)response.StatusCode}. Revisa los logs del servicio en Render."
                    };
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
                var root = doc.RootElement;
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                {
                    var text = string.Join("", parts.EnumerateArray()
                        .Where(part => part.TryGetProperty("text", out _))
                        .Select(part => part.GetProperty("text").GetString()));
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }

                _logger.LogWarning("Gemini respondió correctamente sin texto utilizable. Respuesta: {Response}", root.ToString());
                return "No pude generar una respuesta para esa consulta. Inténtalo de otra forma.";
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("La solicitud a Gemini agotó el tiempo de espera.");
                return "El asistente tardó demasiado en responder. Inténtalo de nuevo.";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "No se pudo conectar con Gemini.");
                return "No se pudo conectar con el servicio del asistente. Inténtalo más tarde.";
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Gemini devolvió una respuesta JSON inválida.");
                return "El servicio del asistente devolvió una respuesta inválida. Inténtalo más tarde.";
            }
        }

        private Task<HttpResponseMessage> SendRequestAsync(string apiKey, string model, string serializedRequest, CancellationToken cancellationToken)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = new StringContent(serializedRequest, Encoding.UTF8, "application/json");
            return SendAndDisposeRequestAsync(request, cancellationToken);
        }

        private async Task<HttpResponseMessage> SendAndDisposeRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (request)
                return await _httpClient.SendAsync(request, cancellationToken);
        }
    }
}

