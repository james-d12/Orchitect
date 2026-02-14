using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Core.Domain.Credential;

public sealed record Credential
{
    public required CredentialId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; } = string.Empty;
    public required CredentialType Type { get; init; }
    public required CredentialPlatform Platform { get; init; }
    public required string EncryptedPayload { get; init; } = string.Empty;
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private Credential()
    {
    }

    public static Credential Create(
        OrganisationId organisationId,
        string name,
        CredentialType type,
        CredentialPlatform platform,
        string encryptedPayload)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(encryptedPayload);

        return new Credential
        {
            Id = new CredentialId(),
            OrganisationId = organisationId,
            Name = name,
            Type = type,
            Platform = platform,
            EncryptedPayload = encryptedPayload,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Credential Update(string name, CredentialType type, CredentialPlatform platform, string encryptedPayload)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(encryptedPayload);

        return this with
        {
            Name = name,
            Type = type,
            Platform = platform,
            EncryptedPayload = encryptedPayload,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
