# Credentials Feature - Implementation Plan

## Context

Organisations need to store authorised credentials (PATs, OAuth tokens, service principals, etc.) for connecting to third-party platforms (GitHub, Azure DevOps, GitLab, Azure). These credentials are stored in Core and consumed by Engine and Inventory bounded contexts. Sensitive credential data must be encrypted at rest using AES-256.

**Design decisions:**
- Single `Credential` entity with a `CredentialType` enum and AES-256-encrypted JSON blob for type-specific fields
- Full CRUD API with secrets masked/omitted in all GET responses
- `IEncryptionService` interface in Domain, `AesEncryptionService` implementation in Persistence
- Credential has its own repository (not managed via Organisation aggregate root) since credentials are queried independently

---

## New Files (16)

### Phase 1: Domain (`src/Orchitect.Core.Domain/Credential/`)

| # | File | Description |
|---|------|-------------|
| 1 | `CredentialId.cs` | `readonly record struct CredentialId(Guid Value)` - strongly-typed ID |
| 2 | `CredentialType.cs` | Enum: `PersonalAccessToken`, `OAuth`, `ServicePrincipal`, `BasicAuth` |
| 3 | `CredentialPlatform.cs` | Enum: `GitHub`, `AzureDevOps`, `GitLab`, `Azure`, `Custom` |
| 4 | `IEncryptionService.cs` | Interface with `Encrypt(string)` and `Decrypt(string)` methods |
| 5 | `Credential.cs` | Sealed record entity with `Id`, `OrganisationId`, `Name`, `Type`, `Platform`, `EncryptedPayload`, `CreatedAt`, `UpdatedAt`. Factory `Create()` and `Update()` methods |
| 6 | `ICredentialRepository.cs` | Extends `IRepository<Credential, CredentialId>`, adds `GetAllByOrganisationId`, `UpdateAsync`, `DeleteAsync` |
| 7 | `CreateCredentialRequest.cs` | Request record with `Name`, `OrganisationId`, `Type`, `Platform`, `Payload` (plaintext JSON) |

### Phase 2: Persistence (`src/Orchitect.Core.Persistence/`)

| # | File | Description |
|---|------|-------------|
| 8 | `Configurations/CredentialConfiguration.cs` | EF config: unique index on `(Name, OrganisationId)`, FK to Organisation with cascade delete, enums stored as strings, ID value conversions |
| 9 | `Repositories/CredentialRepository.cs` | Repository implementation following `OrganisationRepository` pattern |
| 10 | `Services/EncryptionOptions.cs` | Options record: `required string Key` (base64-encoded 256-bit key) |
| 11 | `Services/AesEncryptionService.cs` | AES-256-CBC implementation. Random IV prepended to ciphertext. Uses `System.Security.Cryptography` (no extra NuGet needed) |

### Phase 3: API (`src/Orchitect.Core.Api/Endpoints/Credential/`)

| # | File | Description |
|---|------|-------------|
| 12 | `CreateCredentialEndpoint.cs` | POST `/credentials` - encrypts payload, creates credential, returns metadata only |
| 13 | `GetCredentialEndpoint.cs` | GET `/credentials/{id}` - returns metadata only (no secrets) |
| 14 | `GetAllCredentialsEndpoint.cs` | GET `/credentials?organisationId={id}` - lists credentials for an org (metadata only) |
| 15 | `UpdateCredentialEndpoint.cs` | PUT `/credentials/{id}` - re-encrypts new payload, returns metadata only |
| 16 | `DeleteCredentialEndpoint.cs` | DELETE `/credentials/{id}` - deletes credential |

---

## Modified Files (5)

| # | File | Change |
|---|------|--------|
| 1 | `src/Orchitect.Core.Persistence/CoreDbContext.cs` | Add `DbSet<Credential> Credentials` |
| 2 | `src/Orchitect.Core.Persistence/CorePersistenceExtensions.cs` | Register `ICredentialRepository` (scoped) and `IEncryptionService` (singleton) |
| 3 | `src/Orchitect.Core.Api/Endpoints/Endpoints.cs` | Add `MapCredentialEndpoints()` with private group (authenticated) |
| 4 | `src/Orchitect.Core.Api/Program.cs` | Add `EncryptionOptions` binding from config section |
| 5 | `src/Orchitect.Core.Api/appsettings.json` | Add `"EncryptionOptions": { "Key": "" }` section |

---

## Security Details

### AES-256-CBC Encryption

- Random 16-byte IV generated per encryption call (different ciphertext each time)
- IV prepended to ciphertext, stored together as base64 string
- Key provided via `EncryptionOptions:Key` config (base64-encoded 32-byte key)
- Key should be set via user secrets / environment variables, never committed

### API Security

- All credential endpoints behind `RequireAuthorization()` (JWT Bearer)
- No endpoint ever returns `EncryptedPayload` - responses include only: `Id`, `OrganisationId`, `Name`, `Type`, `Platform`, `CreatedAt`, `UpdatedAt`
- Plaintext `Payload` only exists in create/update request bodies (HTTPS protects in transit)

### Payload JSON Schemas by Type

**PersonalAccessToken:**
```json
{ "token": "ghp_xxxxxxxxxxxx" }
```

**OAuth:**
```json
{
    "clientId": "...",
    "clientSecret": "...",
    "refreshToken": "...",
    "tokenUrl": "https://..."
}
```

**ServicePrincipal:**
```json
{
    "tenantId": "...",
    "clientId": "...",
    "clientSecret": "..."
}
```

**BasicAuth:**
```json
{
    "username": "...",
    "password": "..."
}
```

---

## Implementation Order

1. Domain types (Phase 1) - no dependencies
2. Persistence layer (Phase 2) - depends on domain
3. API layer (Phase 3) - depends on domain + persistence
4. EF migration: `cd src/Orchitect.Core.Persistence && dotnet ef migrations add AddCredentials`
5. Generate dev encryption key: `openssl rand -base64 32`, set via user secrets

---

## Verification

1. `dotnet build` - confirm zero warnings (TreatWarningsAsErrors enforced)
2. `dotnet run --project src/Orchitect.AppHost` - confirm migrations apply, service starts
3. Test via Swagger UI or Bruno:
   - Register + login to get JWT token
   - POST `/credentials` with payload - verify 200 with metadata only
   - GET `/credentials/{id}` - verify metadata returned, no secrets
   - GET `/credentials?organisationId={id}` - verify list returns
   - PUT `/credentials/{id}` - verify update works
   - DELETE `/credentials/{id}` - verify 204
   - Verify cascade: delete organisation, confirm its credentials are also deleted
4. `dotnet test` - confirm existing tests still pass

---

## Credential Payload Design: Typed Models & Usage Examples

### Typed Payload Records

Each `CredentialType` maps to a strongly-typed record that defines the shape of the JSON payload. These records live in `src/Orchitect.Core.Domain/Credential/Payloads/` and are used by consuming services to deserialize the encrypted blob.

```csharp
// src/Orchitect.Core.Domain/Credential/Payloads/PersonalAccessTokenPayload.cs
namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record PersonalAccessTokenPayload
{
    public required string Token { get; init; }
}
```

```csharp
// src/Orchitect.Core.Domain/Credential/Payloads/OAuthPayload.cs
namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record OAuthPayload
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string RefreshToken { get; init; }
    public required string TokenUrl { get; init; }
}
```

```csharp
// src/Orchitect.Core.Domain/Credential/Payloads/ServicePrincipalPayload.cs
namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record ServicePrincipalPayload
{
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}
```

```csharp
// src/Orchitect.Core.Domain/Credential/Payloads/BasicAuthPayload.cs
namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record BasicAuthPayload
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}
```

### Credential Payload Resolver

A helper to decrypt and deserialize the payload into the correct typed model, placed in Core.Domain so it can be referenced by Engine and Inventory:

```csharp
// src/Orchitect.Core.Domain/Credential/CredentialPayloadResolver.cs
using System.Text.Json;
using Orchitect.Core.Domain.Credential.Payloads;

namespace Orchitect.Core.Domain.Credential;

public sealed class CredentialPayloadResolver(IEncryptionService encryptionService)
{
    public T Resolve<T>(Credential credential) where T : class
    {
        var decryptedJson = encryptionService.Decrypt(credential.EncryptedPayload);
        return JsonSerializer.Deserialize<T>(decryptedJson)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize credential '{credential.Name}' payload as {typeof(T).Name}.");
    }

    public PersonalAccessTokenPayload ResolvePersonalAccessToken(Credential credential)
    {
        ValidateType(credential, CredentialType.PersonalAccessToken);
        return Resolve<PersonalAccessTokenPayload>(credential);
    }

    public OAuthPayload ResolveOAuth(Credential credential)
    {
        ValidateType(credential, CredentialType.OAuth);
        return Resolve<OAuthPayload>(credential);
    }

    public ServicePrincipalPayload ResolveServicePrincipal(Credential credential)
    {
        ValidateType(credential, CredentialType.ServicePrincipal);
        return Resolve<ServicePrincipalPayload>(credential);
    }

    public BasicAuthPayload ResolveBasicAuth(Credential credential)
    {
        ValidateType(credential, CredentialType.BasicAuth);
        return Resolve<BasicAuthPayload>(credential);
    }

    private static void ValidateType(Credential credential, CredentialType expectedType)
    {
        if (credential.Type != expectedType)
        {
            throw new InvalidOperationException(
                $"Credential '{credential.Name}' is of type {credential.Type}, expected {expectedType}.");
        }
    }
}
```

### Example: Creating a Credential via the API

**Request** (POST `/credentials`):
```json
{
    "name": "GitHub Production PAT",
    "organisationId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
    "type": "PersonalAccessToken",
    "platform": "GitHub",
    "payload": "{\"token\": \"ghp_abc123def456\"}"
}
```

**Response** (200 OK - secrets never returned):
```json
{
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "organisationId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
    "name": "GitHub Production PAT",
    "type": "PersonalAccessToken",
    "platform": "GitHub",
    "createdAt": "2026-02-13T10:30:00Z",
    "updatedAt": "2026-02-13T10:30:00Z"
}
```

**What's stored in the database** (`core."Credentials"` table):
```
Id:               7c9e6679-7425-40de-944b-e07fc1f90ae7
OrganisationId:   d290f1ee-6c54-4b01-90e6-d701748f0851
Name:             GitHub Production PAT
Type:             PersonalAccessToken
Platform:         GitHub
EncryptedPayload: kJ3mF8xQ2... (AES-256 encrypted base64 blob containing IV + ciphertext)
CreatedAt:        2026-02-13 10:30:00
UpdatedAt:        2026-02-13 10:30:00
```

### Example: Using a Credential in Inventory.Infrastructure (GitHub)

This shows how the Inventory infrastructure would retrieve and use a stored credential to connect to GitHub, replacing the current `IOptions<GitHubSettings>` pattern:

```csharp
// How it currently works (hard-coded from appsettings.json):
public sealed class GitHubConnectionService(IOptions<GitHubSettings> options) : IGitHubConnectionService
{
    public GitHubClient Client { get; } = new(new ProductHeaderValue(options.Value.AgentName))
    {
        Credentials = new Credentials(options.Value.Token)
    };
}

// How it would work with the Credential system:
public sealed class GitHubConnectionService(
    ICredentialRepository credentialRepository,
    CredentialPayloadResolver payloadResolver) : IGitHubConnectionService
{
    public async Task<GitHubClient> CreateClientAsync(
        CredentialId credentialId,
        CancellationToken cancellationToken)
    {
        var credential = await credentialRepository.GetByIdAsync(credentialId, cancellationToken)
            ?? throw new InvalidOperationException($"Credential {credentialId} not found.");

        var payload = payloadResolver.ResolvePersonalAccessToken(credential);

        return new GitHubClient(new ProductHeaderValue("Orchitect"))
        {
            Credentials = new Credentials(payload.Token)
        };
    }
}
```

### Example: Using a Credential in Inventory.Infrastructure (Azure DevOps)

```csharp
// How it currently works:
public sealed class AzureDevOpsConnectionService(IOptions<AzureDevOpsSettings> options)
{
    // var credentials = new VssBasicCredential(string.Empty, options.Value.PersonalAccessToken);
    // _connection = new VssConnection(connectionUri, credentials);
}

// How it would work with the Credential system:
public sealed class AzureDevOpsConnectionService(
    ICredentialRepository credentialRepository,
    CredentialPayloadResolver payloadResolver)
{
    public async Task<VssConnection> CreateConnectionAsync(
        CredentialId credentialId,
        string organization,
        CancellationToken cancellationToken)
    {
        var credential = await credentialRepository.GetByIdAsync(credentialId, cancellationToken)
            ?? throw new InvalidOperationException($"Credential {credentialId} not found.");

        var payload = payloadResolver.ResolvePersonalAccessToken(credential);

        var connectionUri = new Uri($"https://dev.azure.com/{organization}");
        var vssCredentials = new VssBasicCredential(string.Empty, payload.Token);
        return new VssConnection(connectionUri, vssCredentials);
    }
}
```

### Example: Using a Credential in Inventory.Infrastructure (Azure Service Principal)

```csharp
// How it would work for Azure with a ServicePrincipal credential:
public sealed class AzureConnectionService(
    ICredentialRepository credentialRepository,
    CredentialPayloadResolver payloadResolver)
{
    public async Task<ArmClient> CreateClientAsync(
        CredentialId credentialId,
        CancellationToken cancellationToken)
    {
        var credential = await credentialRepository.GetByIdAsync(credentialId, cancellationToken)
            ?? throw new InvalidOperationException($"Credential {credentialId} not found.");

        var payload = payloadResolver.ResolveServicePrincipal(credential);

        var clientSecretCredential = new ClientSecretCredential(
            payload.TenantId,
            payload.ClientId,
            payload.ClientSecret);

        return new ArmClient(clientSecretCredential);
    }
}
```

### Example: Using a Credential in Inventory.Infrastructure (GitLab)

```csharp
// How it currently works:
public sealed class GitLabConnectionService(IOptions<GitLabSettings> options) : IGitLabConnectionService
{
    public GitLabClient Client { get; } = new(options.Value.HostUrl, options.Value.Token);
}

// How it would work with the Credential system:
public sealed class GitLabConnectionService(
    ICredentialRepository credentialRepository,
    CredentialPayloadResolver payloadResolver)
{
    public async Task<GitLabClient> CreateClientAsync(
        CredentialId credentialId,
        string hostUrl,
        CancellationToken cancellationToken)
    {
        var credential = await credentialRepository.GetByIdAsync(credentialId, cancellationToken)
            ?? throw new InvalidOperationException($"Credential {credentialId} not found.");

        var payload = payloadResolver.ResolvePersonalAccessToken(credential);

        return new GitLabClient(hostUrl, payload.Token);
    }
}
```

### Data Flow Summary

```
API Request (plaintext JSON payload)
    │
    ▼
Endpoint encrypts payload via IEncryptionService.Encrypt()
    │
    ▼
Credential entity stores EncryptedPayload (opaque base64 string)
    │
    ▼
Database: core."Credentials" table (AES-256 encrypted at rest)
    │
    ▼
Consumer retrieves Credential entity via ICredentialRepository
    │
    ▼
CredentialPayloadResolver decrypts + deserializes to typed record
    │
    ▼
Typed payload (e.g., PersonalAccessTokenPayload) used to build API client
```

### Additional Files from This Section

| # | File | Description |
|---|------|-------------|
| 17 | `src/Orchitect.Core.Domain/Credential/Payloads/PersonalAccessTokenPayload.cs` | PAT payload record |
| 18 | `src/Orchitect.Core.Domain/Credential/Payloads/OAuthPayload.cs` | OAuth payload record |
| 19 | `src/Orchitect.Core.Domain/Credential/Payloads/ServicePrincipalPayload.cs` | Service principal payload record |
| 20 | `src/Orchitect.Core.Domain/Credential/Payloads/BasicAuthPayload.cs` | Basic auth payload record |
| 21 | `src/Orchitect.Core.Domain/Credential/CredentialPayloadResolver.cs` | Decrypt + deserialize helper |
