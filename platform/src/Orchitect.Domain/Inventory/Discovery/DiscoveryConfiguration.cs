using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Discovery;

public sealed record DiscoveryConfiguration : IEntity
{
    public const int NameMaxLength = 200;

    public required DiscoveryConfigurationId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required CredentialId CredentialId { get; init; }
    public required string Name { get; init; }
    public required DiscoveryPlatform Platform { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string? Schedule { get; init; }
    public Dictionary<string, string> PlatformConfig { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    private DiscoveryConfiguration() { }

    public static DiscoveryConfiguration Create(
        OrganisationId organisationId,
        CredentialId credentialId,
        string name,
        DiscoveryPlatform platform,
        bool isEnabled = true,
        Dictionary<string, string>? platformConfig = null)
    {
        ValidateName(name);

        return new DiscoveryConfiguration
        {
            Id = new DiscoveryConfigurationId(),
            OrganisationId = organisationId,
            CredentialId = credentialId,
            Name = name,
            Platform = platform,
            IsEnabled = isEnabled,
            PlatformConfig = platformConfig ?? [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public DiscoveryConfiguration Update(
        string name,
        bool isEnabled,
        Dictionary<string, string>? platformConfig = null)
    {
        ValidateName(name);

        return this with
        {
            Name = name,
            IsEnabled = isEnabled,
            PlatformConfig = platformConfig ?? PlatformConfig,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));
    }
}
