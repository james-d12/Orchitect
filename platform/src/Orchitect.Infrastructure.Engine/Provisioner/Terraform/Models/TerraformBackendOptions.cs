namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public enum TerraformBackendMode
{
    Local,
    Remote
}

public sealed record TerraformBackendOptions
{
    public const string SectionName = "TerraformBackend";

    public TerraformBackendMode Mode { get; init; } = TerraformBackendMode.Local;

    public string? Type { get; init; }

    public Dictionary<string, string> Config { get; init; } = [];

    public bool IsRemote => Mode == TerraformBackendMode.Remote;

    public string? GetValidationError() => Mode switch
    {
        TerraformBackendMode.Remote when string.IsNullOrWhiteSpace(Type) =>
            $"{SectionName}:Type is required when {SectionName}:Mode is {TerraformBackendMode.Remote}.",
        TerraformBackendMode.Remote when Config.Count == 0 =>
            $"{SectionName}:Config is required when {SectionName}:Mode is {TerraformBackendMode.Remote}.",
        TerraformBackendMode.Local when !string.IsNullOrWhiteSpace(Type) || Config.Count > 0 =>
            $"{SectionName}:Type and {SectionName}:Config are only used when {SectionName}:Mode is " +
            $"{TerraformBackendMode.Remote}. Set Mode to {TerraformBackendMode.Remote} or remove them.",
        TerraformBackendMode.Local or TerraformBackendMode.Remote => null,
        _ => $"{SectionName}:Mode '{Mode}' is not supported."
    };
}
