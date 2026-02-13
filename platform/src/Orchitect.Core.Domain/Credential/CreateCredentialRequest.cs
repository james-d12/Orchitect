namespace Orchitect.Core.Domain.Credential;

public sealed record CreateCredentialRequest(
    string Name,
    Guid OrganisationId,
    CredentialType Type,
    CredentialPlatform Platform,
    string Payload);
