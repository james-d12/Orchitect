using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orchitect.Engine.Contracts.Runner.Api;

public static class RunnerContract
{
    public const string HeaderName = "Orchitect-Runner-Contract";
    public const int Version = 1;

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
