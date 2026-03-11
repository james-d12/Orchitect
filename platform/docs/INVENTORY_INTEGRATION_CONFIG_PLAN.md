# Inventory Integration Configuration Plan

## Overview

This document outlines the design for a flexible, credential-based integration configuration system for the Inventory capability. This system will allow organizations to map Core credentials to third-party integrations (GitHub, GitLab, Azure DevOps, Azure) and configure data collection settings per integration.

## Problem Statement

Currently:
- Third-party integration credentials are configured globally via `appsettings.json`
- No per-organization credential mapping
- No way to configure different collection settings per organization
- Discovery runs once at startup with no scheduling
- No fine-grained control over what data to collect per integration

**Goals:**
1. Enable per-organization credential mapping to integrations
2. Support multiple integrations of the same type per organization (e.g., multiple GitHub accounts)
3. Configure data collection scope (which resources to discover)
4. Schedule discovery runs with configurable frequency
5. Maintain separation between Core (credential storage) and Inventory (credential usage)

---

## Architecture Overview

### Bounded Context Interaction

```
┌─────────────────────────────────────────────────────────────┐
│ Core (Schema: core)                                         │
│  • Organisations                                            │
│  • Credentials (encrypted, platform-agnostic)               │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ FK: CredentialId
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ Inventory (Schema: inventory)                               │
│  • IntegrationConfigurations                                │
│      - Maps Credential → Integration Type → Settings        │
│      - Scheduling & collection scope                        │
│  • Discovered Resources (Repos, WorkItems, CloudResources)  │
└─────────────────────────────────────────────────────────────┘
```

**Key Principle:** Core owns credentials (encrypted storage), Inventory owns integration configuration (how to use credentials).

---

## Domain Model

### 1. IntegrationConfiguration Entity

**Location:** `src/Orchitect.Inventory.Domain/Integration/IntegrationConfiguration.cs`

```csharp
public sealed record IntegrationConfiguration
{
    public required IntegrationConfigurationId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required CredentialId CredentialId { get; init; }
    public required string Name { get; init; }  // User-friendly name (e.g., "Main GitHub Account")
    public required IntegrationPlatform Platform { get; init; }
    public required IntegrationType Type { get; init; }
    public required bool IsEnabled { get; init; }

    // Scheduling
    public required DiscoverySchedule Schedule { get; init; }

    // Collection Scope (what to discover)
    public required DataCollectionScope CollectionScope { get; init; }

    // Platform-specific settings (JSON)
    public required string SettingsJson { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? LastDiscoveryAt { get; init; }
    public string? LastDiscoveryStatus { get; init; }
}
```

### 2. IntegrationPlatform Enum

```csharp
public enum IntegrationPlatform
{
    GitHub,
    GitLab,
    AzureDevOps,
    Azure
}
```

### 3. IntegrationType Enum

```csharp
public enum IntegrationType
{
    GitRepository,      // GitHub, GitLab, AzureDevOps repos
    Ticketing,          // AzureDevOps work items
    CloudInfrastructure // Azure resources
}
```

### 4. DiscoverySchedule Value Object

```csharp
public sealed record DiscoverySchedule
{
    public required ScheduleFrequency Frequency { get; init; }
    public required int Interval { get; init; }  // e.g., 6 for "every 6 hours"
    public TimeSpan? PreferredStartTime { get; init; }  // e.g., 02:00 for overnight runs
    public bool RunOnStartup { get; init; }
}

public enum ScheduleFrequency
{
    Manual,      // Only run on-demand
    Minutes,
    Hours,
    Daily,
    Weekly
}
```

### 5. DataCollectionScope Value Object

```csharp
public sealed record DataCollectionScope
{
    // Git-related
    public bool CollectRepositories { get; init; }
    public bool CollectPullRequests { get; init; }
    public bool CollectPipelines { get; init; }
    public bool CollectBranches { get; init; }

    // Ticketing
    public bool CollectWorkItems { get; init; }
    public bool CollectUsers { get; init; }

    // Cloud
    public bool CollectCloudResources { get; init; }
    public bool CollectCloudSecrets { get; init; }

    // Filters (JSON arrays)
    public List<string> ProjectFilters { get; init; } = [];       // For AzureDevOps
    public List<string> SubscriptionFilters { get; init; } = [];  // For Azure
    public List<string> RepositoryFilters { get; init; } = [];    // Regex patterns
}
```

### 6. Platform-Specific Settings (JSON)

Rather than creating separate tables for each platform's settings, store platform-specific configuration as JSON in `SettingsJson`:

**GitHub Example:**
```json
{
  "agentName": "Orchitect-Inventory",
  "includePrivateRepos": true,
  "organizationName": "myorg"
}
```

**Azure DevOps Example:**
```json
{
  "organization": "myazureorg",
  "includeDisabledProjects": false
}
```

**GitLab Example:**
```json
{
  "hostUrl": "https://gitlab.company.com",
  "includeArchived": false
}
```

**Azure Example:**
```json
{
  "tenantId": "guid",
  "includeDisabledResources": false
}
```

---

## Database Schema

### Table: IntegrationConfigurations

| Column | Type | Constraints |
|--------|------|-------------|
| Id | UUID | PK |
| OrganisationId | UUID | FK → core.Organisations (CASCADE DELETE) |
| CredentialId | UUID | FK → core.Credentials (CASCADE DELETE) |
| Name | VARCHAR(200) | NOT NULL |
| Platform | VARCHAR(50) | NOT NULL |
| Type | VARCHAR(50) | NOT NULL |
| IsEnabled | BOOLEAN | NOT NULL, DEFAULT TRUE |
| ScheduleFrequency | VARCHAR(20) | NOT NULL |
| ScheduleInterval | INT | NOT NULL |
| PreferredStartTime | TIME | NULL |
| RunOnStartup | BOOLEAN | NOT NULL, DEFAULT FALSE |
| CollectionScopeJson | JSONB | NOT NULL |
| SettingsJson | JSONB | NOT NULL |
| CreatedAt | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() |
| UpdatedAt | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() |
| LastDiscoveryAt | TIMESTAMPTZ | NULL |
| LastDiscoveryStatus | VARCHAR(100) | NULL |

**Indexes:**
- `idx_integration_configs_org_id` on `OrganisationId`
- `idx_integration_configs_credential_id` on `CredentialId`
- `idx_integration_configs_platform` on `Platform`
- `idx_integration_configs_enabled` on `IsEnabled`

**Constraints:**
- Unique constraint on `(OrganisationId, Name)` — prevent duplicate names per org
- Check constraint: `CredentialPlatform` from `Credentials` table must match `Platform` in `IntegrationConfigurations`

---

## Implementation Plan

### Phase 1: Domain & Persistence Layer

**Files to Create:**

1. **Domain Entities:**
   - `src/Orchitect.Inventory.Domain/Integration/IntegrationConfigurationId.cs`
   - `src/Orchitect.Inventory.Domain/Integration/IntegrationConfiguration.cs`
   - `src/Orchitect.Inventory.Domain/Integration/IntegrationPlatform.cs`
   - `src/Orchitect.Inventory.Domain/Integration/IntegrationType.cs`
   - `src/Orchitect.Inventory.Domain/Integration/DiscoverySchedule.cs`
   - `src/Orchitect.Inventory.Domain/Integration/DataCollectionScope.cs`
   - `src/Orchitect.Inventory.Domain/Integration/ScheduleFrequency.cs`

2. **EF Configuration:**
   - `src/Orchitect.Inventory.Persistence/Configurations/IntegrationConfigurationConfiguration.cs`
   - Update `InventoryDbContext.cs` to add `DbSet<IntegrationConfiguration>`

3. **Value Object Converters:**
   - JSON converters for `DiscoverySchedule` and `DataCollectionScope`

4. **Migration:**
   ```bash
   cd src/Orchitect.Inventory.Persistence
   dotnet ef migrations add AddIntegrationConfigurations
   ```

### Phase 2: Integration Service Refactoring

**Current Problem:** Services like `GitHubConnectionService` use `IOptions<GitHubSettings>` from appsettings.

**Solution:** Create a new abstraction that resolves settings from database configurations.

**New Service:**
```csharp
// src/Orchitect.Inventory.Infrastructure/Integration/IIntegrationConfigurationResolver.cs
public interface IIntegrationConfigurationResolver
{
    Task<T?> ResolveSettingsAsync<T>(
        OrganisationId organisationId,
        IntegrationPlatform platform,
        CancellationToken cancellationToken) where T : class;

    Task<(T settings, string decryptedCredential)?> ResolveWithCredentialAsync<T>(
        OrganisationId organisationId,
        IntegrationPlatform platform,
        CancellationToken cancellationToken) where T : class;
}
```

**Implementation:**
```csharp
// src/Orchitect.Inventory.Infrastructure/Integration/IntegrationConfigurationResolver.cs
public class IntegrationConfigurationResolver : IIntegrationConfigurationResolver
{
    private readonly InventoryDbContext _inventoryContext;
    private readonly CoreDbContext _coreContext;
    private readonly IEncryptionService _encryptionService;

    public async Task<(T settings, string decryptedCredential)?> ResolveWithCredentialAsync<T>(...)
    {
        // 1. Query IntegrationConfiguration for org + platform
        var config = await _inventoryContext.IntegrationConfigurations
            .FirstOrDefaultAsync(ic =>
                ic.OrganisationId == organisationId &&
                ic.Platform == platform &&
                ic.IsEnabled);

        if (config == null) return null;

        // 2. Fetch and decrypt credential from Core
        var credential = await _coreContext.Credentials.FindAsync(config.CredentialId);
        var decrypted = await _encryptionService.DecryptAsync(credential.EncryptedPayload);

        // 3. Deserialize settings JSON
        var settings = JsonSerializer.Deserialize<T>(config.SettingsJson);

        return (settings, decrypted);
    }
}
```

**Update Connection Services:**
```csharp
// Before:
public GitHubConnectionService(IOptions<GitHubSettings> options)
{
    Client = new GitHubClient(...) { Credentials = new Credentials(options.Value.Token) };
}

// After:
public GitHubConnectionService(
    IIntegrationConfigurationResolver resolver,
    OrganisationId organisationId)  // Scoped per request
{
    var (settings, token) = await resolver.ResolveWithCredentialAsync<GitHubSettings>(
        organisationId,
        IntegrationPlatform.GitHub);

    Client = new GitHubClient(...) { Credentials = new Credentials(token) };
}
```

### Phase 3: Scheduled Discovery

**Replace:** `DiscoveryHostedService` (runs once at startup)

**With:** `ScheduledDiscoveryHostedService` (runs on configurable schedules)

```csharp
// src/Orchitect.Inventory.Api/Jobs/ScheduledDiscoveryHostedService.cs
public class ScheduledDiscoveryHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

            var dueConfigs = await context.IntegrationConfigurations
                .Where(ic => ic.IsEnabled)
                .Where(ic => ic.LastDiscoveryAt == null ||
                             /* calculate next run based on schedule */)
                .ToListAsync(stoppingToken);

            foreach (var config in dueConfigs)
            {
                await ExecuteDiscoveryAsync(config, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);  // Check every minute
        }
    }

    private async Task ExecuteDiscoveryAsync(IntegrationConfiguration config, CancellationToken ct)
    {
        // Resolve credential, instantiate appropriate discovery service, run discovery
        // Update LastDiscoveryAt and LastDiscoveryStatus
    }
}
```

### Phase 4: API Endpoints (MVC Controllers)

**Location:** `src/Orchitect.Inventory.Api/Controllers/IntegrationConfigurationController.cs`

**Endpoints:**

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/integration-configurations` | List all configs for authenticated user's org |
| GET | `/api/integration-configurations/{id}` | Get specific config |
| POST | `/api/integration-configurations` | Create new integration config |
| PUT | `/api/integration-configurations/{id}` | Update existing config |
| DELETE | `/api/integration-configurations/{id}` | Delete config |
| POST | `/api/integration-configurations/{id}/trigger` | Manually trigger discovery |
| GET | `/api/integration-configurations/{id}/status` | Get last discovery status |

**Request/Response DTOs:**

```csharp
// POST/PUT request
public record CreateIntegrationConfigurationRequest
{
    public required Guid CredentialId { get; init; }
    public required string Name { get; init; }
    public required string Platform { get; init; }
    public required string Type { get; init; }
    public required bool IsEnabled { get; init; }
    public required DiscoveryScheduleDto Schedule { get; init; }
    public required DataCollectionScopeDto CollectionScope { get; init; }
    public required JsonDocument Settings { get; init; }  // Platform-specific JSON
}

// Response
public record IntegrationConfigurationResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Platform { get; init; }
    public string Type { get; init; }
    public bool IsEnabled { get; init; }
    public DiscoveryScheduleDto Schedule { get; init; }
    public DataCollectionScopeDto CollectionScope { get; init; }
    public JsonDocument Settings { get; init; }
    public DateTime? LastDiscoveryAt { get; init; }
    public string? LastDiscoveryStatus { get; init; }
}
```

### Phase 5: Migration Strategy

**Backwards Compatibility:**

During transition period, support both old (appsettings-based) and new (database-based) configuration:

1. Update `IntegrationConfigurationResolver` to fall back to `IOptions<T>` if no DB config exists
2. Provide migration script to create IntegrationConfigurations from existing appsettings
3. Deprecation notice in logs when using appsettings approach

**Migration Script Example:**
```csharp
// src/Orchitect.Inventory.Persistence/Migrations/DataMigrations/MigrateAppsettingsToDatabase.cs
public static async Task MigrateAsync(IServiceProvider services)
{
    // Read appsettings configurations
    // Create default IntegrationConfiguration entries for existing organizations
    // Link to existing credentials if available, or create placeholder credentials
}
```

---

## Security Considerations

### 1. Credential Access Control

- **Validation:** When creating IntegrationConfiguration, verify that `CredentialId` belongs to the same `OrganisationId`
- **Authorization:** Users can only configure integrations for their own organization
- **Audit Logging:** Track who creates/modifies integration configurations

### 2. Credential Decryption

- Credentials decrypted **only** when needed (at discovery execution time)
- Never return decrypted credentials in API responses
- Use scoped lifetime for decrypted credentials (dispose after use)

### 3. Settings Validation

- Validate platform-specific `SettingsJson` against schemas
- Prevent injection attacks in filter fields (ProjectFilters, RepositoryFilters)
- Sanitize regex patterns in RepositoryFilters

---

## Testing Strategy

### 1. Unit Tests

- Value object validation (DiscoverySchedule, DataCollectionScope)
- IntegrationConfiguration entity factory methods
- Settings JSON serialization/deserialization

### 2. Integration Tests

- IntegrationConfigurationResolver with mocked DbContexts
- Cross-schema FK constraints (Inventory → Core)
- Scheduled discovery service with test clock

### 3. End-to-End Tests

- Create integration config via API
- Trigger manual discovery
- Verify discovered resources in database
- Delete credential and verify cascade delete to IntegrationConfiguration

---

## Example Usage Flow

### 1. User Creates GitHub Integration

**Step 1:** User creates a GitHub PAT credential in Core:
```http
POST /api/credentials
{
  "organisationId": "uuid",
  "name": "GitHub Main Account",
  "type": "PersonalAccessToken",
  "platform": "GitHub",
  "payload": {
    "token": "ghp_xxxxxxxxxxxx"
  }
}
```
**Response:** `{ "credentialId": "cred-uuid" }`

**Step 2:** User configures GitHub integration in Inventory:
```http
POST /api/integration-configurations
{
  "credentialId": "cred-uuid",
  "name": "GitHub Main Account Discovery",
  "platform": "GitHub",
  "type": "GitRepository",
  "isEnabled": true,
  "schedule": {
    "frequency": "Hours",
    "interval": 6,
    "runOnStartup": true
  },
  "collectionScope": {
    "collectRepositories": true,
    "collectPullRequests": true,
    "collectPipelines": true,
    "repositoryFilters": ["orchitect-.*"]
  },
  "settings": {
    "agentName": "Orchitect-Inventory",
    "includePrivateRepos": true,
    "organizationName": "myorg"
  }
}
```

**Step 3:** Discovery runs automatically every 6 hours, or manually triggered:
```http
POST /api/integration-configurations/{id}/trigger
```

### 2. User Creates Multiple Azure DevOps Integrations

```http
POST /api/integration-configurations
{
  "credentialId": "cred-azure-devops-team-a",
  "name": "Team A Azure DevOps",
  "platform": "AzureDevOps",
  "type": "GitRepository",
  "collectionScope": {
    "collectRepositories": true,
    "projectFilters": ["TeamA-*"]
  },
  ...
}

POST /api/integration-configurations
{
  "credentialId": "cred-azure-devops-team-b",
  "name": "Team B Azure DevOps",
  "platform": "AzureDevOps",
  "type": "Ticketing",
  "collectionScope": {
    "collectWorkItems": true,
    "projectFilters": ["TeamB-*"]
  },
  ...
}
```

---

## Future Enhancements

### 1. Discovery History Tracking

Add `DiscoveryRuns` table to track history:
```csharp
public record DiscoveryRun
{
    public Guid Id { get; init; }
    public Guid IntegrationConfigurationId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string Status { get; init; }  // Success, Failed, PartialSuccess
    public int ResourcesDiscovered { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### 2. Credential Rotation Support

When a credential is updated in Core, automatically mark all dependent IntegrationConfigurations for re-validation.

### 3. Multi-Region Azure Support

Allow specifying multiple Azure regions in settings for cloud resource discovery.

### 4. Webhooks for Real-Time Updates

Instead of polling, support webhook endpoints for platforms that offer them (GitHub, GitLab).

### 5. Discovery Prioritization

Add priority levels to IntegrationConfigurations for resource-constrained environments.

---

## Open Questions

1. **Multi-Tenancy:** Should IntegrationConfigurations support multiple users/teams within an organization configuring different integrations?
   - **Recommendation:** Start with org-level only, add team-level in v2

2. **Credential Sharing:** Can multiple IntegrationConfigurations share the same CredentialId?
   - **Recommendation:** Yes, allow sharing (e.g., one GitHub PAT for both repos and pipelines)

3. **Concurrency:** How to handle overlapping discovery runs?
   - **Recommendation:** Use distributed locks (Redis) or database row-level locks to prevent concurrent runs

4. **Failure Handling:** Should failed discoveries retry automatically?
   - **Recommendation:** Implement exponential backoff retry logic with max attempts

5. **Settings Schema Validation:** How to enforce platform-specific settings schemas?
   - **Recommendation:** Use JSON Schema validation + fluent validation in API layer

---

## Summary

This plan provides a comprehensive, scalable solution for credential-based integration configuration in the Inventory system:

- **✅ Separates concerns:** Core owns credentials, Inventory owns configuration
- **✅ Flexible scheduling:** Per-integration discovery schedules
- **✅ Fine-grained control:** Configurable data collection scopes
- **✅ Multi-platform support:** Unified model for GitHub, GitLab, Azure DevOps, Azure
- **✅ Organization-scoped:** Each organization configures their own integrations
- **✅ Secure:** Credentials encrypted at rest, decrypted only when needed
- **✅ Extensible:** Easy to add new platforms or integration types

**Next Steps:**
1. Review and approve this plan
2. Create implementation tasks in order (Phase 1 → Phase 5)
3. Begin with domain model and persistence layer
4. Incrementally migrate existing discovery services
