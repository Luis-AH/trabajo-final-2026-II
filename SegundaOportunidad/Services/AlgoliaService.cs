using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
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
            var record = new
            {
                objectID = articulo.Id,
                title = articulo.Title,
                description = articulo.Description,
                imageUrl = articulo.ImageUrl,
                modalidad = articulo.Modalidad.ToString(),
                price = articulo.Price,
                categoriaId = articulo.CategoriaId,
                isActive = articulo.IsActive
            };

            await _index.SaveObjectAsync(record);
        }

        public async Task<List<string>> SearchArticulosIdsAsync(string query)
        {
            var search = new Query(query)
            {
                HitsPerPage = 50
            };
            
            var result = await _index.SearchAsync<AlgoliaArticuloRecord>(search);
            return result.Hits.Select(h => h.ObjectID).ToList();
        }
        
        private class AlgoliaArticuloRecord
        {
            public string ObjectID { get; set; } = string.Empty;
        }
    }
}
