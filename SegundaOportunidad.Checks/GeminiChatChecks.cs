using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SegundaOportunidad.Services;

static class GeminiChatChecks
{
    public static async Task Run()
    {
        var calls = 0;
        var config = new Dictionary<string,string?>();
        var status = HttpStatusCode.OK;
        var body = """{"candidates":[{"content":{"parts":[{"text":"Hola "},{"text":"usuario"}]}}]}""";
        using var client = new HttpClient(new Handler(async request =>
        {
            calls++;
            Ensure(request.RequestUri!.Host == "generativelanguage.googleapis.com", "host Google");
            Ensure(request.RequestUri.AbsolutePath == "/v1beta/models/gemini-2.5-flash:generateContent", "URL exacta sin models duplicado");
            Ensure(request.Headers.GetValues("x-goog-api-key").Single() == "test-key", "clave en cabecera");
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Ensure(json.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString() == "Hola", "mensaje JSON");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }));
        AiSearchService Service() => new(new ConfigurationBuilder().AddInMemoryCollection(config).Build(), client, NullLogger<AiSearchService>.Instance);
        Ensure((await Service().GetCompletionAsync("Sistema", "Hola")).Contains("no está configurado") && calls == 0, "sin clave no llama Google");
        config["Gemini:Model"] = "models/gemini-2.5-flash";
        config["Gemini:ApiKey"] = " ";
        config["GEMINI_API_KEY"] = " test-key ";
        Ensure(await Service().GetCompletionAsync("Sistema", "Hola") == "Hola usuario", "alias con valor principal vacío y respuesta multipartes");
        foreach (var code in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable })
        {
            var before = calls;
            status = code;
            body = """{"error":{"status":"TEST_ERROR"}}""";
            Ensure(!string.IsNullOrWhiteSpace(await Service().GetCompletionAsync("Sistema", "Hola")), $"error HTTP {(int)code} controlado");
            Ensure(calls - before == (code == HttpStatusCode.ServiceUnavailable ? 3 : 1), "reintentos acotados solo para 503");
        }
        status = HttpStatusCode.OK;
        body = "invalid json";
        Ensure((await Service().GetCompletionAsync("Sistema", "Hola")).Contains("inválida"), "JSON inválido controlado");
        body = "{}";
        Ensure((await Service().GetCompletionAsync("Sistema", "Hola")).Contains("No pude"), "respuesta sin candidatos");
        var sanitize = typeof(AiSearchService).GetMethod("SafeErrorBody", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var safe = (string)sanitize.Invoke(null, new object[] { """{"error":{"code":404,"status":"NOT_FOUND","message":"This model is no longer available to new users test-key persona@example.org","details":["private"]}}""" })!;
        Ensure(safe.Contains("MODEL_UNAVAILABLE_TO_NEW_USERS") && !safe.Contains("test-key") && !safe.Contains("example.org") && !safe.Contains("private"), "cuerpo de error sin clave ni datos personales");
        Console.WriteLine("Pruebas Gemini completadas sin claves reales ni servicios externos.");
    }
    static void Ensure(bool condition, string message)
    {
        if (!condition) throw new Exception("Gemini: " + message);
        Console.WriteLine("OK Gemini: " + message);
    }
    sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
