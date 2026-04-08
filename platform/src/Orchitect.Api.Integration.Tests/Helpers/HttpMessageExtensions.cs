using System.Net.Http.Json;
using System.Text.Json;

namespace Orchitect.Api.Integration.Tests.Helpers;

public static class HttpMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<T?> ReadFromJsonAsync<T>(this HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}