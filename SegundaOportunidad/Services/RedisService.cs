using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SegundaOportunidad.Services;

// Redis es una optimización: SQLite sigue siendo la fuente de datos.
public sealed class RedisService : IDisposable
{
    private readonly Lazy<Task<ConnectionMultiplexer?>> _connection;
    private readonly ILogger<RedisService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisService(IConfiguration configuration, ILogger<RedisService> logger)
    {
        _logger = logger;
        var configured = configuration["Redis:ConnectionString"];
        _connection = new Lazy<Task<ConnectionMultiplexer?>>(async () =>
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                _logger.LogInformation("Redis no configurado; se consultará la base de datos.");
                return null;
            }
            try
            {
                var options = ConfigurationOptions.Parse(configured);
                options.AbortOnConnectFail = false;
                options.BacklogPolicy = BacklogPolicy.FailFast;
                // Un servicio de caché inaccesible no debe retrasar el acceso durante segundos.
                if (!configured.Contains("connectTimeout=", StringComparison.OrdinalIgnoreCase)) options.ConnectTimeout = 1000;
                return await ConnectionMultiplexer.ConnectAsync(options);
            }
            catch (Exception ex) when (ex is RedisException or ArgumentException)
            {
                _logger.LogWarning("No se pudo inicializar Redis ({Tipo}); se consultará la base de datos.", ex.GetType().Name);
                return null;
            }
        });
    }

    private async Task<IDatabase?> DatabaseAsync()
    {
        var connection = await _connection.Value;
        return connection?.IsConnected == true ? connection.GetDatabase() : null;
    }

    public async Task<T?> GetCacheAsync<T>(string key)
    {
        try
        {
            var db = await DatabaseAsync();
            if (db == null) return default;
            var json = await db.StringGetAsync(key);
            return json.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(json.ToString(), JsonOptions);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            _logger.LogWarning("Lectura de caché no disponible ({Tipo}); se consultará la base de datos.", ex.GetType().Name);
            return default;
        }
    }

    public async Task SetCacheAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        try
        {
            var db = await DatabaseAsync();
            if (db != null) await db.StringSetAsync(key, json, expiry ?? TimeSpan.FromMinutes(30));
        }
        catch (RedisException ex) { _logger.LogWarning("No se pudo escribir en caché ({Tipo}).", ex.GetType().Name); }
    }

    public async Task RemoveCacheAsync(string key)
    {
        try
        {
            var db = await DatabaseAsync();
            if (db != null) await db.KeyDeleteAsync(key);
        }
        catch (RedisException ex) { _logger.LogWarning("No se pudo invalidar caché ({Tipo}).", ex.GetType().Name); }
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated && _connection.Value.IsCompletedSuccessfully) _connection.Value.Result?.Dispose();
    }
}
