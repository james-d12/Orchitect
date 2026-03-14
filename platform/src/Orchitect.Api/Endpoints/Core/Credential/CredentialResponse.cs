using Orchitect.Domain.Core.Credential;

namespace Orchitect.Api.Endpoints.Core.Credential;

public sealed record CredentialResponse(
    Guid Id,
    Guid OrganisationId,
    string Name,
    CredentialType Type,
    CredentialPlatform Platform,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CredentialResponse From(Domain.Core.Credential.Credential credential) =>
        new(
            credential.Id.Value,
            credential.OrganisationId.Value,
            credential.Name,
            credential.Type,
            credential.Platform,
            credential.CreatedAt,
            credential.UpdatedAt);
}