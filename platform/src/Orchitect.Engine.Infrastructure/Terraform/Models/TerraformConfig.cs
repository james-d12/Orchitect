using System.Text.Json.Serialization;

namespace Orchitect.Engine.Infrastructure.Terraform.Models;

public sealed record TerraformConfig
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("variables")]
    public Dictionary<string, Variable> Variables { get; init; } = [];

    [JsonPropertyName("outputs")]
    public Dictionary<string, Output> Outputs { get; init; } = [];

    [JsonPropertyName("required_providers")]
    public Dictionary<string, RequiredProvider> RequiredProviders { get; init; } = [];

    public sealed record Variable
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("default")]
        public object? Default { get; set; }

        [JsonPropertyName("required")]
        public bool Required { get; set; }
    }

    public sealed record Output
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public sealed record RequiredProvider
    {
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("version_constraints")]
        public List<string> VersionConstraints { get; set; } = [];
    }
}