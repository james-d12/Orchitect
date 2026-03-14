using System.Text.Json;

namespace Orchitect.Domain.Core.Credential;

public sealed record CreateCredentialRequest(
    string Name,
    Guid OrganisationId,
    CredentialType Type,
    CredentialPlatform Platform,
    JsonElement Payload);
