using Orchitect.Core.Domain.Credential;

namespace Orchitect.Core.Api.Endpoints.Credential;

public sealed record CredentialResponse(
    Guid Id,
    Guid OrganisationId,
    string Name,
    CredentialType Type,
    CredentialPlatform Platform,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CredentialResponse From(Domain.Credential.Credential credential) =>
        new(
            credential.Id.Value,
            credential.OrganisationId.Value,
            credential.Name,
            credential.Type,
            credential.Platform,
            credential.CreatedAt,
            credential.UpdatedAt);
}
