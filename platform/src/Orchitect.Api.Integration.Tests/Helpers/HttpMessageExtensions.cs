using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orchitect.Api.Integration.Tests.Helpers;

public static class HttpMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T?> ReadFromJsonAsync<T>(this HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}