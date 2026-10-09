using System.Net.Http.Json;

namespace CatalogWebUI
{
    public class CatalogHttpService
    {
        private readonly HttpClient _httpClient;

        public CatalogHttpService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<CatalogItem>> GetItemsAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<CatalogItem>>("api/catalog/items")
                   ?? new List<CatalogItem>();
        }

        public async Task<CatalogItem?> GetItemByIdAsync(int id)
        {
            return await _httpClient.GetFromJsonAsync<CatalogItem>($"api/catalog/items/{id}");
        }

        public async Task<bool> CreateItemAsync(CatalogItem item)
        {
            var response = await _httpClient.PostAsJsonAsync("api/catalog/items", item);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateItemAsync(CatalogItem item)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/catalog/items/{item.Id}", item);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteItemAsync(int id)
        {
            var response = await _httpClient.DeleteAsync($"api/catalog/items/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}

