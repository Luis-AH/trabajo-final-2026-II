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
            var apiKey = Setting("Gemini:ApiKey", "GEMINI_API_KEY", "GOOGLE_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Gemini no está configurado. Defina Gemini:ApiKey o la variable GEMINI_API_KEY en el entorno.");
                return "El asistente no está configurado. Contacta al administrador de la plataforma.";
            }

            var model = NormalizeModel(Setting("Gemini:Model", "GEMINI_MODEL") ?? "gemini-3.1-flash-lite");
            var fallbackModel = NormalizeModel(Setting("Gemini:FallbackModel", "GEMINI_FALLBACK_MODEL") ?? model);

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = userMessage } } } },
                generationConfig = new { maxOutputTokens = 2048, temperature = 0.5 }
            };
            var serializedRequest = JsonSerializer.Serialize(requestBody);

            try
            {
                var response = await SendWithRetriesAsync(apiKey, model, serializedRequest, cancellationToken);
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable &&
                    !string.Equals(model, fallbackModel, StringComparison.OrdinalIgnoreCase))
                {
                    var initialError = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Gemini model {Model} no disponible (503). Reintentando con {FallbackModel}. Código: {ErrorCode}", model, fallbackModel, ErrorCode(initialError));
                    response.Dispose();
                    response = await SendWithRetriesAsync(apiKey, fallbackModel, serializedRequest, cancellationToken);
                }

                using (response)
                {
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Gemini POST {Endpoint} devolvió HTTP {StatusCode}. Cuerpo seguro: {ErrorBody}", response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Path), (int)response.StatusCode, SafeErrorBody(errorBody));
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

                _logger.LogWarning("Gemini respondió sin texto utilizable (posible bloqueo o límite de generación).");
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

        // Los valores vacíos no deben ocultar otra variable válida.
        private string? Setting(params string[] names) => names.Select(name => _configuration[name])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

        private static string ErrorCode(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("status", out var status))
                    return status.GetString() ?? "UNKNOWN";
            }
            catch (JsonException) { }
            return "UNKNOWN";
        }

        private static string NormalizeModel(string model) => model.StartsWith("models/", StringComparison.Ordinal)
            ? model["models/".Length..] : model;

        // Proyección permitida del cuerpo: nunca registrar message/details originales,
        // porque Google puede incluir valores de la solicitud o identificadores privados.
        internal static string SafeErrorBody(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var error = doc.RootElement.GetProperty("error");
                var code = error.TryGetProperty("code", out var c) && c.TryGetInt32(out var n) ? n : 0;
                var status = ErrorCode(body);
                status = status is "NOT_FOUND" or "INVALID_ARGUMENT" or "PERMISSION_DENIED" or "UNAUTHENTICATED"
                    or "RESOURCE_EXHAUSTED" or "UNAVAILABLE" or "INTERNAL" ? status : "UNKNOWN";
                var message = error.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                var reason = message.Contains("no longer available to new users", StringComparison.OrdinalIgnoreCase)
                    ? "MODEL_UNAVAILABLE_TO_NEW_USERS"
                    : "MESSAGE_OMITTED_FOR_PRIVACY";
                return JsonSerializer.Serialize(new { error = new { code, status, message = reason } });
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                return "{\"error\":\"UNREADABLE_BODY_OMITTED\"}";
            }
        }

        private async Task<HttpResponseMessage> SendWithRetriesAsync(string apiKey, string model, string body, CancellationToken cancellationToken)
        {
            // Tres intentos por modelo, con esperas de 1 y 2 segundos; nunca reintentar
            // errores de configuración/autenticación ni cuotas agotadas.
            for (var attempt = 1; ; attempt++)
            {
                var response = await SendRequestAsync(apiKey, model, body, cancellationToken);
                if (response.StatusCode != HttpStatusCode.ServiceUnavailable || attempt == 3)
                    return response;
                response.Dispose();
                _logger.LogWarning("Gemini modelo {Model}: HTTP 503, intento {Attempt}/3; reintentando", model, attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
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

