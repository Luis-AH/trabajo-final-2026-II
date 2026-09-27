using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Algolia.Search.Exceptions;
using SegundaOportunidad.Models;

namespace SegundaOportunidad.Services
{
    public class AlgoliaService
    {
        private readonly SearchClient _client;
        private readonly SearchIndex _index;

        public AlgoliaService(IConfiguration configuration)
        {
            // App ID and Write API Key for syncing
            _client = new SearchClient(
                configuration["Algolia:AppId"] ?? "PLHOKYL1K3", 
                configuration["Algolia:WriteApiKey"] ?? "b8e668ea9a836b86fd7a78cba373cbf7"
            );
            _index = _client.InitIndex("articulos");
        }

        public async Task IndexArticuloAsync(Articulo articulo)
        {
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
                Console.WriteLine($"[Algolia Error] {ex.Message}");
            }
        }

        public async Task<List<string>> SearchArticulosIdsAsync(string query)
        {
            try
            {
                var search = new Query(query)
                {
                    HitsPerPage = 50
                };
                
                var result = await _index.SearchAsync<AlgoliaArticuloRecord>(search);
                return result.Hits.Select(h => h.ObjectID).ToList();
            }
            catch (AlgoliaApiException ex) when (ex.HttpErrorCode == 404)
            {
                // El índice no existe todavía, retornar lista vacía
                return new List<string>();
            }
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
