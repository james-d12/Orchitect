namespace Orchitect.Inventory.Api.Settings;

public sealed record CorsSettings
{
    public required Dictionary<string, string> Policies { get; init; } = [];
}