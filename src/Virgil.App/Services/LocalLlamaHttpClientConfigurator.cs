using System.Net.Http;
using System.Net.Http.Headers;

namespace Virgil.App.Services;

internal static class LocalLlamaHttpClientConfigurator
{
    internal static void ConfigureAuthHeaders(HttpClient client, string? apiKey)
    {
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Remove("X-API-Key");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }
}
