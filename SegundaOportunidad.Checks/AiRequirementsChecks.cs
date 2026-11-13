using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class AiRequirementsChecks
{
    public static async Task Run(HttpClient http, IReadOnlyList<string> cookies)
    {
        using var denied = await http.GetAsync("/AdminMl");
        Ensure(denied.StatusCode == HttpStatusCode.Redirect, "panel de clasificación exige autenticación administrativa");
        using var results = JsonDocument.Parse(await http.GetStringAsync("/api/ml/recomendaciones-mlnet?consulta=libros&cantidad=4"));
        Ensure(results.RootElement.GetProperty("algoritmo").GetString()!.Contains("ML.NET"), "endpoint utiliza modelo Microsoft.ML");
        var articles = results.RootElement.GetProperty("publicaciones").EnumerateArray().ToList();
        Ensure(articles.Count > 0 && articles.Count <= 4, "ML.NET entrega recomendaciones reales y respeta el límite");
        var excluded = new[] { "006", "012", "018", "024", "030", "036", "042", "048" };
        Ensure(articles.All(a => !excluded.Any(id => a.GetProperty("id").GetString() == "demo-ml-art-" + id)), "ML.NET excluye publicaciones ocultas, suspendidas e inactivas");
        Ensure(articles.All(a => a.GetProperty("enlace").GetString()!.StartsWith("/Articulos/Details/")), "enlaces reales de las recomendaciones ML.NET");
        using var empty = JsonDocument.Parse(await http.GetStringAsync("/api/ml/recomendaciones-mlnet?consulta=zzzxqv987654321&cantidad=4"));
        Ensure(empty.RootElement.GetProperty("publicaciones").GetArrayLength() == 0, "consulta fuera del vocabulario no inventa coincidencias");
        using var invalid = await http.GetAsync("/api/ml/recomendaciones-mlnet?consulta=libros&cantidad=99");
        Ensure(invalid.StatusCode == HttpStatusCode.BadRequest, "límite inválido rechazado");

        using var page = new HttpRequestMessage(HttpMethod.Get, "/AdminMl");
        page.Headers.Add("Cookie", string.Join("; ", cookies));
        using var response = await http.SendAsync(page);
        var html = await response.Content.ReadAsStringAsync();
        Ensure(response.StatusCode == HttpStatusCode.OK && html.Contains("Contenido habitual"), "pantalla administrativa clasifica texto habitual");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var formCookies = cookies.ToList();
        if (response.Headers.TryGetValues("Set-Cookie", out var added)) formCookies.AddRange(added.Select(value => value.Split(';')[0]));
        using var post = new HttpRequestMessage(HttpMethod.Post, "/AdminMl");
        post.Headers.Add("Cookie", string.Join("; ", formCookies));
        post.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["texto"] = "Haz clic para duplicar tu inversión y obtener dinero garantizado.",
            ["consulta"] = "libros", ["__RequestVerificationToken"] = token });
        using var classified = await http.SendAsync(post);
        var classifiedHtml = WebUtility.HtmlDecode(await classified.Content.ReadAsStringAsync());
        Ensure(classified.StatusCode == HttpStatusCode.OK && classifiedHtml.Contains("Revisión sugerida"), "POST real clasifica promoción sospechosa con modelo entrenado");
        using var noToken = new HttpRequestMessage(HttpMethod.Post, "/AdminMl");
        noToken.Headers.Add("Cookie", string.Join("; ", cookies));
        noToken.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["texto"] = "Texto", ["consulta"] = "libros" });
        using var rejected = await http.SendAsync(noToken);
        Ensure(rejected.StatusCode == HttpStatusCode.BadRequest, "clasificación administrativa protege antiforgery");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("OK REQUISITOS IA: " + message);
    }
}
