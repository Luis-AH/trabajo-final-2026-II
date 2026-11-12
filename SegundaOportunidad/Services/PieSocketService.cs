using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SegundaOportunidad.Services;

public class PieSocketService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PieSocketService> _logger;
    public PieSocketService(IConfiguration configuration,IHttpClientFactory factory,ILogger<PieSocketService> logger)
    {
        _configuration=configuration;
        _httpClient=factory.CreateClient(nameof(PieSocketService));
        _logger=logger;
    }
    public async Task PublishMessageAsync<T>(string channelId,string eventName,T data)
    {
        var cluster=_configuration["PieSocket:ClusterId"];
        var key=_configuration["PieSocket:ApiKey"];
        var secret=_configuration["PieSocket:ApiSecret"];
        if(string.IsNullOrWhiteSpace(cluster) || !Regex.IsMatch(cluster,@"^[a-zA-Z0-9.-]+$") || cluster=="free" || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
        { _logger.LogWarning("PieSocket sin configuración completa; los mensajes siguen disponibles por consulta autenticada.");return; }
        // API v3 utiliza channel, no channelId. El contenido privado no se difunde por canales públicos.
        var payload=new {key,secret,channel=channelId,message=new {@event=eventName,data=new {refresh=true}}};
        using var content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json");
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response=await _httpClient.PostAsync($"https://{cluster}.piesocket.com/api/publish",content,timeout.Token);
            if(!response.IsSuccessStatusCode) _logger.LogWarning("PieSocket devolvió HTTP {StatusCode}; no se registra el cuerpo para proteger credenciales y mensajes.",(int)response.StatusCode);
        }
        catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException)
        { _logger.LogWarning("PieSocket no disponible ({Type}); usar consulta autenticada.",ex.GetType().Name); }
    }
}
