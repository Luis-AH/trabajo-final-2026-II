using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SegundaOportunidad.Services
{
    public class RedisService
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public RedisService(IConfiguration configuration)
        {
            var connectionString = configuration["Redis:ConnectionString"] ?? "humor-wave-quicksand-84304.db.redis.io:11953,password=zh2fiNSYBgRgO5UhfOQeEOhl6pMaQIDE,abortConnect=false";
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase();
        }

        public async Task SetCacheAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(value, options);
            await _db.StringSetAsync(key, json, expiry ?? TimeSpan.FromMinutes(30));
        }

        public async Task<T?> GetCacheAsync<T>(string key)
        {
            var json = await _db.StringGetAsync(key);
            if (json.IsNullOrEmpty)
            {
                return default;
            }

            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Deserialize<T>(json.ToString(), options);
        }
    }
}
