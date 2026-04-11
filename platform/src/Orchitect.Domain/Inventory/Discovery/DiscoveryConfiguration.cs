using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Discovery;

public sealed record DiscoveryConfiguration
{
    public required DiscoveryConfigurationId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required CredentialId CredentialId { get; init; }
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
        DiscoveryPlatform platform,
        bool isEnabled = true,
        Dictionary<string, string>? platformConfig = null)
    {
        return new DiscoveryConfiguration
        {
            Id = new DiscoveryConfigurationId(),
            OrganisationId = organisationId,
            CredentialId = credentialId,
            Platform = platform,
            IsEnabled = isEnabled,
            PlatformConfig = platformConfig ?? [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public DiscoveryConfiguration Update(
        bool isEnabled,
        Dictionary<string, string>? platformConfig = null)
    {
        return this with
        {
            IsEnabled = isEnabled,
            PlatformConfig = platformConfig ?? PlatformConfig,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
