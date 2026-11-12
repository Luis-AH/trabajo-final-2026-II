using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Algolia.Search.Exceptions;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services
{
    public class AlgoliaService
    {
        private readonly SearchClient? _client;
        private readonly SearchIndex? _index;
        public bool Configurado => _index != null;

        public AlgoliaService(IConfiguration configuration)
        {
            var appId = configuration["Algolia:AppId"];
            var key = configuration["Algolia:WriteApiKey"];
            if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(key))
            {
                _client = new SearchClient(appId, key);
                _index = _client.InitIndex("articulos");
            }
        }
        public virtual async Task IndexArticuloAsync(Articulo articulo)
        {
            if (_index == null) return;
            try
            {
                var record = new AlgoliaArticuloRecord
                {
                    ObjectID = articulo.Id,
                    Title = articulo.Title,
                    Description = articulo.Description,
                    ImageUrl = articulo.ImageUrl,
                    Modalidad = articulo.Modalidad.ToString(),
                    Price = articulo.Price,
                    CategoriaId = articulo.CategoriaId,
                    IsActive = articulo.IsActive
                };

                await _index.SaveObjectAsync(record);
                Console.WriteLine($"[Algolia] Indexed {articulo.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Algolia] Indexación no disponible ({ex.GetType().Name}).");
            }
        }

        // Consulta acotada para el asistente; conserva las operaciones existentes del catálogo.
        public virtual async Task<List<string>> SearchChatIdsAsync(string query, CancellationToken ct)
        {
            if (_index == null) return [];
            var result = await _index.SearchAsync<AlgoliaArticuloRecord>(new Query(query) { HitsPerPage = 50 }).WaitAsync(ct);
            return result.Hits.Select(hit => hit.ObjectID).ToList();
        }

        public async Task<List<string>> SearchArticulosIdsAsync(string query)
        {
            if (_index == null) return [];
            try
            {
                var search = new Query(query)
                {
                    HitsPerPage = 50
                };
                
                var result = await _index.SearchAsync<AlgoliaArticuloRecord>(search);
                return result.Hits.Select(h => h.ObjectID).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Algolia] Búsqueda no disponible ({ex.GetType().Name}); catálogo usará EF.");
                return new List<string>();
            }
        }
        
        public async Task<List<string>> SearchAllArticulosIdsAsync(string query, CancellationToken ct)
        {
            if (_index == null) return [];
            var ids = new HashSet<string>();
            var page = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (_index == null) return [];
            var result = await _index.SearchAsync<AlgoliaArticuloRecord>(new Query(query) { HitsPerPage = 100, Page = page });
                foreach (var hit in result.Hits) ids.Add(hit.ObjectID);
                page++;
                if (page >= result.NbPages) break;
            }
            return ids.ToList();
        }

        private class AlgoliaArticuloRecord
        {
            [System.Text.Json.Serialization.JsonPropertyName("objectID")]
            [Newtonsoft.Json.JsonProperty("objectID")]
            public string ObjectID { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? ImageUrl { get; set; }
            public string Modalidad { get; set; } = string.Empty;
            public decimal? Price { get; set; }
            public int CategoriaId { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
