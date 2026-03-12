# Discovery Credential Integration Plan

## Overview

This document outlines the plan to integrate the Core credential system into the Inventory discovery workflow. This will transform the discovery system from a global, static configuration model (appsettings.json) to an organization-scoped, credential-based model.

## Current State Analysis

### What We Have
- ✅ Core credential infrastructure exists with encryption, platform support (GitHub, AzureDevOps, GitLab, Azure)
- ✅ Credential payloads support PAT, OAuth, ServicePrincipal, BasicAuth
- ✅ CredentialPayloadResolver for decrypting and deserializing credential payloads
- ✅ ICredentialRepository with organization-scoped queries

### Current Problems
- ❌ Discovery services use static appsettings.json configuration (not organization-scoped)
- ❌ Connection services depend on IOptions<Settings> (global, not per-org)
- ❌ DiscoveryHostedService runs once for all platforms (not per-organization)
- ❌ No multi-tenancy support - all organizations would share same credentials
- ❌ Secrets stored in appsettings.json (not encrypted)

## Target Architecture

### Goals
- Each organization configures their own credentials for discovery platforms
- Background job runs **per organization** (not globally)
- Connection services instantiated **per credential** (not from IOptions)
- Graceful degradation: skip organizations without credentials
- All secrets encrypted in database using Core's IEncryptionService

### Key Principles
- **Multi-tenancy first**: Each org's discovery runs independently
- **Credential-based authentication**: All platform connections use Core credentials
- **Configuration as data**: Discovery settings stored in database, not config files
- **Security**: No secrets in appsettings.json, all credentials encrypted at rest

---

## Phase 1: Domain Layer Changes

### 1.1 Inventory.Domain - New DiscoveryConfiguration Model

**Location:** `src/Orchitect.Inventory.Domain/Discovery/`

**Files to Create:**
- `DiscoveryConfiguration.cs`
- `DiscoveryConfigurationId.cs`
- `DiscoveryPlatform.cs`
- `IDiscoveryConfigurationRepository.cs`

#### DiscoveryConfiguration.cs

```csharp
using Orchitect.Core.Domain.Organisation;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Domain.Discovery;

public sealed record DiscoveryConfiguration
{
    public required DiscoveryConfigurationId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required CredentialId CredentialId { get; init; }  // FK to Core.Credentials
    public required DiscoveryPlatform Platform { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string? Schedule { get; init; }  // Future: cron expression for per-config scheduling
    public Dictionary<string, string> PlatformConfig { get; init; } = new();
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
            PlatformConfig = platformConfig ?? new(),
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
```

#### DiscoveryConfigurationId.cs

```csharp
namespace Orchitect.Inventory.Domain.Discovery;

public sealed record DiscoveryConfigurationId(Guid Value)
{
    public DiscoveryConfigurationId() : this(Guid.NewGuid()) { }
}
```

#### DiscoveryPlatform.cs

```csharp
namespace Orchitect.Inventory.Domain.Discovery;

public enum DiscoveryPlatform
{
    GitHub,
    AzureDevOps,
    GitLab,
    Azure
}
```

**PlatformConfig examples:**
- **GitHub**: `{ "includeArchived": "false" }`
- **AzureDevOps**: `{ "organization": "myorg", "projectFilters": "Project1,Project2" }`
- **GitLab**: `{ "hostUrl": "https://gitlab.company.com", "groupIds": "123,456" }`
- **Azure**: `{ "subscriptionFilters": "sub-1,sub-2" }`

#### IDiscoveryConfigurationRepository.cs

```csharp
using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Inventory.Domain.Discovery;

public interface IDiscoveryConfigurationRepository : IRepository<DiscoveryConfiguration, DiscoveryConfigurationId>
{
    Task<IEnumerable<DiscoveryConfiguration>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DiscoveryConfiguration>> GetEnabledConfigurationsAsync(
        CancellationToken cancellationToken = default);

    Task<DiscoveryConfiguration?> UpdateAsync(
        DiscoveryConfiguration configuration,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        DiscoveryConfigurationId id,
        CancellationToken cancellationToken = default);
}
```

### 1.2 Update IDiscoveryService Interface

**Location:** `src/Orchitect.Inventory.Domain/Discovery/IDiscoveryService.cs`

**Change from:**
```csharp
public interface IDiscoveryService
{
    string Platform { get; }
    Task DiscoveryAsync(CancellationToken cancellationToken);
}
```

**Change to:**
```csharp
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Domain.Discovery;

public interface IDiscoveryService
{
    string Platform { get; }

    Task DiscoverAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken);
}
```

**Why pass `DiscoveryConfiguration` instead of individual parameters?**
- **Fewer parameters** - cleaner method signature (2 params instead of 4)
- **No duplication** - `OrganisationId` and `PlatformConfig` already in configuration
- **Cohesive** - all discovery settings in one place
- **Future-proof** - easy access to `Schedule`, `IsEnabled`, or other metadata if needed
- **No architectural violation** - both `IDiscoveryService` and `DiscoveryConfiguration` live in `Inventory.Domain`

**Why pass `Credential` separately?**
- Comes from `Core.Domain` (different bounded context)
- `DiscoveryConfiguration` only has `CredentialId` (FK reference)
- Caller (DiscoveryHostedService) fetches and decrypts the credential
- Keeps responsibility clear: caller resolves credential, service consumes it

---

## Phase 2: Persistence Layer Changes

### 2.1 Inventory.Persistence - Add DbSet and Configuration

**Location:** `src/Orchitect.Inventory.Persistence/`

#### Update InventoryDbContext.cs

```csharp
public DbSet<DiscoveryConfiguration> DiscoveryConfigurations { get; set; }
```

#### New File: Configurations/DiscoveryConfigurationConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Organisation;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Persistence.Configurations;

public class DiscoveryConfigurationConfiguration : IEntityTypeConfiguration<DiscoveryConfiguration>
{
    public void Configure(EntityTypeBuilder<DiscoveryConfiguration> builder)
    {
        builder.ToTable("DiscoveryConfigurations", "inventory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(
                id => id.Value,
                value => new DiscoveryConfigurationId(value));

        builder.Property(x => x.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value))
            .IsRequired();

        builder.Property(x => x.CredentialId)
            .HasConversion(
                id => id.Value,
                value => new CredentialId(value))
            .IsRequired();

        builder.Property(x => x.Platform)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.Property(x => x.Schedule)
            .HasMaxLength(100);

        builder.Property(x => x.PlatformConfig)
            .HasColumnType("jsonb")  // PostgreSQL JSON
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        // Cross-schema FK to core.Organisations with CASCADE DELETE
        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(x => x.OrganisationId)
            .HasPrincipalSchema("core")
            .HasPrincipalKey(o => o.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // Cross-schema FK to core.Credentials with CASCADE DELETE
        builder.HasOne<Credential>()
            .WithMany()
            .HasForeignKey(x => x.CredentialId)
            .HasPrincipalSchema("core")
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.OrganisationId);
        builder.HasIndex(x => x.CredentialId);
        builder.HasIndex(x => new { x.OrganisationId, x.Platform });
        builder.HasIndex(x => x.IsEnabled);
    }
}
```

#### New File: Repositories/DiscoveryConfigurationRepository.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Inventory.Persistence.Repositories;

public sealed class DiscoveryConfigurationRepository : IDiscoveryConfigurationRepository
{
    private readonly InventoryDbContext _context;

    public DiscoveryConfigurationRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<DiscoveryConfiguration?> GetByIdAsync(
        DiscoveryConfigurationId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.DiscoveryConfigurations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<DiscoveryConfiguration>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DiscoveryConfigurations
            .Where(x => x.OrganisationId == organisationId)
            .OrderBy(x => x.Platform)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DiscoveryConfiguration>> GetEnabledConfigurationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.DiscoveryConfigurations
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.OrganisationId)
            .ThenBy(x => x.Platform)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        DiscoveryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await _context.DiscoveryConfigurations.AddAsync(configuration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiscoveryConfiguration?> UpdateAsync(
        DiscoveryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _context.DiscoveryConfigurations.Update(configuration);
        await _context.SaveChangesAsync(cancellationToken);
        return configuration;
    }

    public async Task<bool> DeleteAsync(
        DiscoveryConfigurationId id,
        CancellationToken cancellationToken = default)
    {
        var configuration = await GetByIdAsync(id, cancellationToken);
        if (configuration == null) return false;

        _context.DiscoveryConfigurations.Remove(configuration);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
```

#### Update Extensions.cs (DI Registration)

```csharp
public static IServiceCollection AddInventoryPersistenceServices(this IServiceCollection services)
{
    // ... existing registrations ...
    services.AddScoped<IDiscoveryConfigurationRepository, DiscoveryConfigurationRepository>();
    return services;
}
```

### 2.2 Create Migration

Run from project root:
```bash
./scripts/efm.sh AddDiscoveryConfiguration
```

Or manually:
```bash
cd src/Orchitect.Inventory.Persistence
dotnet ef migrations add AddDiscoveryConfiguration
```

---

## Phase 3: Infrastructure Layer Changes

### 3.1 Refactor Connection Services

**Goal:** Remove IOptions dependency, accept credentials as constructor parameters

#### Example: GitHubConnectionService.cs

**Location:** `src/Orchitect.Inventory.Infrastructure/GitHub/Services/GitHubConnectionService.cs`

**FROM:**
```csharp
using Microsoft.Extensions.Options;
using Octokit;
using Orchitect.Inventory.Infrastructure.GitHub.Models;

namespace Orchitect.Inventory.Infrastructure.GitHub.Services;

public sealed class GitHubConnectionService(IOptions<GitHubSettings> options) : IGitHubConnectionService
{
    public GitHubClient Client { get; } = new(new ProductHeaderValue(options.Value.AgentName))
    {
        Credentials = new Credentials(options.Value.Token)
    };
}
```

**TO:**
```csharp
using Octokit;
using Orchitect.Core.Domain.Credential;
using Orchitect.Core.Domain.Credential.Payloads;

namespace Orchitect.Inventory.Infrastructure.GitHub.Services;

public sealed class GitHubConnectionService : IGitHubConnectionService
{
    public GitHubClient Client { get; }

    public GitHubConnectionService(string token, string agentName = "Orchitect")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        Client = new GitHubClient(new ProductHeaderValue(agentName))
        {
            Credentials = new Credentials(token)
        };
    }

    /// <summary>
    /// Factory method for creating connection from Core credential
    /// </summary>
    public static GitHubConnectionService FromCredential(
        Credential credential,
        CredentialPayloadResolver resolver,
        Dictionary<string, string>? platformConfig = null)
    {
        if (credential.Platform != CredentialPlatform.GitHub)
            throw new InvalidOperationException(
                $"Credential '{credential.Name}' is for {credential.Platform}, expected GitHub");

        var payload = resolver.ResolvePersonalAccessToken(credential);
        var agentName = platformConfig?.GetValueOrDefault("agentName", "Orchitect") ?? "Orchitect";

        return new GitHubConnectionService(payload.Token, agentName);
    }
}
```

#### Apply Similar Pattern To:

**AzureDevOpsConnectionService.cs:**
```csharp
public static AzureDevOpsConnectionService FromCredential(
    Credential credential,
    CredentialPayloadResolver resolver,
    Dictionary<string, string>? platformConfig = null)
{
    if (credential.Platform != CredentialPlatform.AzureDevOps)
        throw new InvalidOperationException(
            $"Credential '{credential.Name}' is for {credential.Platform}, expected AzureDevOps");

    var payload = resolver.ResolvePersonalAccessToken(credential);
    var organization = platformConfig?.GetValueOrDefault("organization")
        ?? throw new InvalidOperationException("AzureDevOps requires 'organization' in platformConfig");

    return new AzureDevOpsConnectionService(organization, payload.Token);
}
```

**GitLabConnectionService.cs:**
```csharp
public static GitLabConnectionService FromCredential(
    Credential credential,
    CredentialPayloadResolver resolver,
    Dictionary<string, string>? platformConfig = null)
{
    if (credential.Platform != CredentialPlatform.GitLab)
        throw new InvalidOperationException(
            $"Credential '{credential.Name}' is for {credential.Platform}, expected GitLab");

    var payload = resolver.ResolvePersonalAccessToken(credential);
    var hostUrl = platformConfig?.GetValueOrDefault("hostUrl", "https://gitlab.com")
        ?? "https://gitlab.com";

    return new GitLabConnectionService(hostUrl, payload.Token);
}
```

**AzureConnectionService.cs:**
```csharp
public static AzureConnectionService FromCredential(
    Credential credential,
    CredentialPayloadResolver resolver,
    Dictionary<string, string>? platformConfig = null)
{
    if (credential.Platform != CredentialPlatform.Azure)
        throw new InvalidOperationException(
            $"Credential '{credential.Name}' is for {credential.Platform}, expected Azure");

    var payload = resolver.ResolveServicePrincipal(credential);

    return new AzureConnectionService(
        payload.TenantId,
        payload.ClientId,
        payload.ClientSecret);
}
```

### 3.2 Refactor Discovery Services

**Goal:** Accept organization-scoped parameters instead of global settings

#### Example: GitHubDiscoveryService.cs

**Location:** `src/Orchitect.Inventory.Infrastructure/GitHub/Services/GitHubDiscoveryService.cs`

**FROM:**
```csharp
public sealed class GitHubDiscoveryService : DiscoveryService
{
    private readonly IGitHubService _gitHubService;
    private readonly IMemoryCache _memoryCache;

    public GitHubDiscoveryService(
        ILogger<GitHubDiscoveryService> logger,
        IGitHubService gitHubService,
        IMemoryCache memoryCache) : base(logger)
    {
        _gitHubService = gitHubService;
        _memoryCache = memoryCache;
    }

    public override string Platform => "GitHub";

    protected override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Uses injected _gitHubService with global settings
    }
}
```

**TO:**
```csharp
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Inventory.Infrastructure.Discovery;
using Orchitect.Inventory.Infrastructure.GitHub.Constants;
using Orchitect.Inventory.Infrastructure.GitHub.Models;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.GitHub.Services;

public sealed class GitHubDiscoveryService : DiscoveryService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CredentialPayloadResolver _payloadResolver;

    public GitHubDiscoveryService(
        ILogger<GitHubDiscoveryService> logger,
        IMemoryCache memoryCache,
        CredentialPayloadResolver payloadResolver) : base(logger)
    {
        _memoryCache = memoryCache;
        _payloadResolver = payloadResolver;
    }

    public override string Platform => "GitHub";

    protected override async Task StartAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        // Create connection service from credential
        var connectionService = GitHubConnectionService.FromCredential(
            credential,
            _payloadResolver,
            configuration.PlatformConfig);

        // Create GitHub service with this connection
        var gitHubService = new GitHubService(connectionService);

        // Perform discovery
        var repositories = await gitHubService.GetRepositoriesAsync();

        var pullRequests = new List<GitHubPullRequest>();
        var pipelines = new List<GitHubPipeline>();

        foreach (var repository in repositories)
        {
            var repositoryPullRequests = await gitHubService.GetPullRequestsAsync(repository);
            pullRequests.AddRange(repositoryPullRequests);

            var repositoryPipelines = await gitHubService.GetPipelinesAsync(repository);
            pipelines.AddRange(repositoryPipelines);
        }

        // Cache with organization-specific keys
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        };

        _memoryCache.Set($"GitHub:Repositories:{configuration.OrganisationId.Value}", repositories, cacheOptions);
        _memoryCache.Set($"GitHub:Pipelines:{configuration.OrganisationId.Value}", pipelines, cacheOptions);
        _memoryCache.Set($"GitHub:PullRequests:{configuration.OrganisationId.Value}", pullRequests, cacheOptions);
    }
}
```

#### Update Base Class: DiscoveryService.cs

**Location:** `src/Orchitect.Inventory.Infrastructure/Discovery/DiscoveryService.cs`

```csharp
using System.Diagnostics;
using Orchitect.Inventory.Domain.Discovery;
using Microsoft.Extensions.Logging;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.Discovery;

public abstract class DiscoveryService : IDiscoveryService
{
    private readonly ILogger<DiscoveryService> _logger;

    protected DiscoveryService(ILogger<DiscoveryService> logger)
    {
        _logger = logger;
    }

    public virtual string Platform => string.Empty;

    public async Task DiscoverAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        try
        {
            _logger.LogInformation(
                "{Platform} Discovery Service started for organisation {OrgId}.",
                Platform,
                configuration.OrganisationId);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await StartAsync(configuration, credential, cancellationToken);

            stopWatch.Stop();
            var milliseconds = stopWatch.Elapsed.TotalMilliseconds;

            _logger.LogInformation(
                "{Platform} Discovery Service for org {OrgId} took: {Milliseconds} ms",
                Platform,
                configuration.OrganisationId,
                milliseconds);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(
                exception,
                "Error occurred whilst trying to discover {Platform} resources for organisation {OrgId}.",
                Platform,
                configuration.OrganisationId);
            throw;
        }
    }

    protected abstract Task StartAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken);
}
```

#### Apply to All Discovery Services:
- `AzureDevOpsDiscoveryService.cs`
- `GitLabDiscoveryService.cs`
- `AzureDiscoveryService.cs`

### 3.3 Update DI Registrations

**Location:** `src/Orchitect.Inventory.Infrastructure/Extensions.cs`

**REMOVE:**
```csharp
// Settings registrations
services.AddOptions<GitHubSettings>()
    .BindConfiguration(nameof(GitHubSettings))
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.AddOptions<AzureDevOpsSettings>()
    .BindConfiguration(nameof(AzureDevOpsSettings))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Singleton connection services
services.AddSingleton<IGitHubConnectionService, GitHubConnectionService>();
services.AddSingleton<IAzureDevOpsConnectionService, AzureDevOpsConnectionService>();
// ... etc
```

**ADD:**
```csharp
// Discovery services as transient (created per-request)
services.AddTransient<IDiscoveryService, GitHubDiscoveryService>();
services.AddTransient<IDiscoveryService, AzureDevOpsDiscoveryService>();
services.AddTransient<IDiscoveryService, GitLabDiscoveryService>();
services.AddTransient<IDiscoveryService, AzureDiscoveryService>();

// Credential payload resolver (needs IEncryptionService from Core)
services.AddScoped<CredentialPayloadResolver>();
```

**Note:** Connection services are no longer registered in DI - they're created on-demand via `FromCredential()` factory methods.

---

## Phase 4: API Layer Changes

### 4.1 New Inventory.Api Endpoints

**Location:** `src/Orchitect.Inventory.Api/Endpoints/Discovery/`

#### CreateDiscoveryConfigurationEndpoint.cs

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Credential;
using Orchitect.Shared;
using System.Security.Claims;

namespace Orchitect.Inventory.Api.Endpoints.Discovery;

public sealed class CreateDiscoveryConfigurationEndpoint : IEndpoint
{
    public record Request(
        CredentialId CredentialId,
        DiscoveryPlatform Platform,
        bool IsEnabled,
        Dictionary<string, string>? PlatformConfig);

    public record Response(DiscoveryConfigurationId Id);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithName("CreateDiscoveryConfiguration")
        .WithSummary("Create a discovery configuration for the current organisation")
        .WithDescription("Links a credential to a discovery platform with optional configuration")
        .Produces<Response>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

    private static async Task<Results<Ok<Response>, BadRequest<ErrorResponse>>> HandleAsync(
        [FromBody] Request request,
        [FromServices] IDiscoveryConfigurationRepository configRepository,
        [FromServices] ICredentialRepository credentialRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();

        // Validate credential exists and belongs to this organisation
        var credential = await credentialRepository.GetByIdAsync(request.CredentialId, cancellationToken);
        if (credential == null)
            return TypedResults.BadRequest(new ErrorResponse("Credential not found"));

        if (credential.OrganisationId != organisationId)
            return TypedResults.BadRequest(new ErrorResponse("Credential does not belong to your organisation"));

        // Validate platform matches credential platform
        var expectedPlatform = request.Platform.ToString();
        if (credential.Platform.ToString() != expectedPlatform)
            return TypedResults.BadRequest(
                new ErrorResponse($"Credential platform ({credential.Platform}) does not match discovery platform ({request.Platform})"));

        // Create configuration
        var config = DiscoveryConfiguration.Create(
            organisationId,
            request.CredentialId,
            request.Platform,
            request.IsEnabled,
            request.PlatformConfig);

        await configRepository.AddAsync(config, cancellationToken);

        return TypedResults.Ok(new Response(config.Id));
    }
}
```

#### ListDiscoveryConfigurationsEndpoint.cs

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Shared;
using System.Security.Claims;

namespace Orchitect.Inventory.Api.Endpoints.Discovery;

public sealed class ListDiscoveryConfigurationsEndpoint : IEndpoint
{
    public record Response(
        DiscoveryConfigurationId Id,
        CredentialId CredentialId,
        string CredentialName,
        DiscoveryPlatform Platform,
        bool IsEnabled,
        Dictionary<string, string> PlatformConfig,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", HandleAsync)
        .WithName("ListDiscoveryConfigurations")
        .WithSummary("List all discovery configurations for the current organisation")
        .Produces<IEnumerable<Response>>(StatusCodes.Status200OK);

    private static async Task<Ok<IEnumerable<Response>>> HandleAsync(
        [FromServices] IDiscoveryConfigurationRepository configRepository,
        [FromServices] ICredentialRepository credentialRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();

        var configs = await configRepository.GetByOrganisationIdAsync(organisationId, cancellationToken);

        // Join with credentials to get credential names
        var credentials = new Dictionary<CredentialId, string>();
        foreach (var config in configs)
        {
            if (!credentials.ContainsKey(config.CredentialId))
            {
                var cred = await credentialRepository.GetByIdAsync(config.CredentialId, cancellationToken);
                if (cred != null)
                    credentials[config.CredentialId] = cred.Name;
            }
        }

        var response = configs.Select(c => new Response(
            c.Id,
            c.CredentialId,
            credentials.GetValueOrDefault(c.CredentialId, "Unknown"),
            c.Platform,
            c.IsEnabled,
            c.PlatformConfig,
            c.CreatedAt,
            c.UpdatedAt));

        return TypedResults.Ok(response);
    }
}
```

#### UpdateDiscoveryConfigurationEndpoint.cs

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Shared;
using System.Security.Claims;

namespace Orchitect.Inventory.Api.Endpoints.Discovery;

public sealed class UpdateDiscoveryConfigurationEndpoint : IEndpoint
{
    public record Request(
        bool IsEnabled,
        Dictionary<string, string>? PlatformConfig);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id}", HandleAsync)
        .WithName("UpdateDiscoveryConfiguration")
        .WithSummary("Update a discovery configuration")
        .Produces(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok, NotFound<ErrorResponse>>> HandleAsync(
        [FromRoute] Guid id,
        [FromBody] Request request,
        [FromServices] IDiscoveryConfigurationRepository repository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var configId = new DiscoveryConfigurationId(id);

        var existing = await repository.GetByIdAsync(configId, cancellationToken);
        if (existing == null || existing.OrganisationId != organisationId)
            return TypedResults.NotFound(new ErrorResponse("Discovery configuration not found"));

        var updated = existing.Update(request.IsEnabled, request.PlatformConfig);
        await repository.UpdateAsync(updated, cancellationToken);

        return TypedResults.Ok();
    }
}
```

#### DeleteDiscoveryConfigurationEndpoint.cs

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Shared;
using System.Security.Claims;

namespace Orchitect.Inventory.Api.Endpoints.Discovery;

public sealed class DeleteDiscoveryConfigurationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id}", HandleAsync)
        .WithName("DeleteDiscoveryConfiguration")
        .WithSummary("Delete a discovery configuration")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, NotFound<ErrorResponse>>> HandleAsync(
        [FromRoute] Guid id,
        [FromServices] IDiscoveryConfigurationRepository repository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var configId = new DiscoveryConfigurationId(id);

        var existing = await repository.GetByIdAsync(configId, cancellationToken);
        if (existing == null || existing.OrganisationId != organisationId)
            return TypedResults.NotFound(new ErrorResponse("Discovery configuration not found"));

        await repository.DeleteAsync(configId, cancellationToken);

        return TypedResults.NoContent();
    }
}
```

#### TriggerDiscoveryEndpoint.cs

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Credential;
using Orchitect.Shared;
using System.Security.Claims;

namespace Orchitect.Inventory.Api.Endpoints.Discovery;

public sealed class TriggerDiscoveryEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/{id}/trigger", HandleAsync)
        .WithName("TriggerDiscovery")
        .WithSummary("Manually trigger discovery for a specific configuration")
        .Produces(StatusCodes.Status202Accepted)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

    private static async Task<Results<Accepted, NotFound<ErrorResponse>, BadRequest<ErrorResponse>>> HandleAsync(
        [FromRoute] Guid id,
        [FromServices] IDiscoveryConfigurationRepository configRepository,
        [FromServices] ICredentialRepository credentialRepository,
        [FromServices] IEnumerable<IDiscoveryService> discoveryServices,
        [FromServices] CredentialPayloadResolver payloadResolver,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var configId = new DiscoveryConfigurationId(id);

        var config = await configRepository.GetByIdAsync(configId, cancellationToken);
        if (config == null || config.OrganisationId != organisationId)
            return TypedResults.NotFound(new ErrorResponse("Discovery configuration not found"));

        // Get credential
        var credential = await credentialRepository.GetByIdAsync(config.CredentialId, cancellationToken);
        if (credential == null)
            return TypedResults.BadRequest(new ErrorResponse("Associated credential not found"));

        // Find matching discovery service
        var service = discoveryServices.FirstOrDefault(s =>
            s.Platform.Equals(config.Platform.ToString(), StringComparison.OrdinalIgnoreCase));

        if (service == null)
            return TypedResults.BadRequest(
                new ErrorResponse($"No discovery service available for platform {config.Platform}"));

        // Trigger discovery (fire and forget - could use background queue)
        _ = Task.Run(async () =>
        {
            try
            {
                await service.DiscoverAsync(
                    config,
                    credential,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Log error (inject ILogger if needed)
                Console.WriteLine($"Discovery failed: {ex.Message}");
            }
        }, cancellationToken);

        return TypedResults.Accepted($"/api/discovery/{id}");
    }
}
```

### 4.2 Register Endpoints

**Location:** `src/Orchitect.Inventory.Api/Endpoints.cs`

```csharp
using Orchitect.Inventory.Api.Endpoints.Discovery;

// ... existing code ...

var discoveryGroup = app.MapGroup("/api/discovery")
    .RequireAuthorization()
    .WithTags("Discovery");

discoveryGroup.MapEndpoint<CreateDiscoveryConfigurationEndpoint>();
discoveryGroup.MapEndpoint<ListDiscoveryConfigurationsEndpoint>();
discoveryGroup.MapEndpoint<UpdateDiscoveryConfigurationEndpoint>();
discoveryGroup.MapEndpoint<DeleteDiscoveryConfigurationEndpoint>();
discoveryGroup.MapEndpoint<TriggerDiscoveryEndpoint>();
```

---

## Phase 5: Background Job Refactoring

### 5.1 Refactor DiscoveryHostedService

**Location:** `src/Orchitect.Inventory.Api/Jobs/DiscoveryHostedService.cs`

**Complete Replacement:**

```csharp
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Api.Jobs;

public sealed class DiscoveryHostedService : BackgroundService
{
    private readonly ILogger<DiscoveryHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _interval;

    public DiscoveryHostedService(
        ILogger<DiscoveryHostedService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Read interval from config, default 30 minutes
        var intervalMinutes = configuration.GetValue<int>("DiscoverySettings:IntervalMinutes", 30);
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Discovery Hosted Service started. Interval: {Interval}", _interval);

        // Optional: delay first run to allow app to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = Tracing.StartActivity();
            _logger.LogInformation("Discovery cycle starting at {Time}", DateTimeOffset.Now);

            try
            {
                await RunDiscoveryCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discovery cycle failed");
            }

            _logger.LogInformation(
                "Discovery cycle completed. Next run in {Interval} at {NextRun}",
                _interval,
                DateTimeOffset.Now.Add(_interval));

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Discovery Hosted Service stopped");
    }

    private async Task RunDiscoveryCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var configRepository = scope.ServiceProvider
            .GetRequiredService<IDiscoveryConfigurationRepository>();
        var credentialRepository = scope.ServiceProvider
            .GetRequiredService<ICredentialRepository>();
        var payloadResolver = scope.ServiceProvider
            .GetRequiredService<CredentialPayloadResolver>();
        var discoveryServices = scope.ServiceProvider
            .GetServices<IDiscoveryService>()
            .ToList();

        _logger.LogDebug("Fetching enabled discovery configurations...");

        // Get all enabled discovery configurations
        var configurations = await configRepository.GetEnabledConfigurationsAsync(cancellationToken);
        var configList = configurations.ToList();

        _logger.LogInformation("Found {Count} enabled discovery configurations", configList.Count);

        // Group by organisation for better logging
        var orgGroups = configList.GroupBy(c => c.OrganisationId);

        foreach (var orgGroup in orgGroups)
        {
            var organisationId = orgGroup.Key;
            var orgConfigs = orgGroup.ToList();

            _logger.LogInformation(
                "Processing {Count} discovery configurations for organisation {OrgId}",
                orgConfigs.Count,
                organisationId);

            foreach (var config in orgConfigs)
            {
                try
                {
                    await ProcessDiscoveryConfigurationAsync(
                        config,
                        credentialRepository,
                        payloadResolver,
                        discoveryServices,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing discovery config {ConfigId} for platform {Platform}, org {OrgId}",
                        config.Id,
                        config.Platform,
                        organisationId);
                    // Continue with next configuration
                }
            }

            _logger.LogInformation(
                "Completed discovery for organisation {OrgId}",
                organisationId);
        }
    }

    private async Task ProcessDiscoveryConfigurationAsync(
        DiscoveryConfiguration config,
        ICredentialRepository credentialRepository,
        CredentialPayloadResolver payloadResolver,
        List<IDiscoveryService> discoveryServices,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Processing discovery config {ConfigId}: {Platform} for org {OrgId}",
            config.Id,
            config.Platform,
            config.OrganisationId);

        // Get credential
        var credential = await credentialRepository.GetByIdAsync(config.CredentialId, cancellationToken);
        if (credential == null)
        {
            _logger.LogWarning(
                "Credential {CredentialId} not found for config {ConfigId}, skipping",
                config.CredentialId,
                config.Id);
            return;
        }

        // Validate credential belongs to same org (shouldn't happen due to FK, but defensive)
        if (credential.OrganisationId != config.OrganisationId)
        {
            _logger.LogError(
                "Credential {CredentialId} belongs to org {CredOrgId} but config {ConfigId} is for org {ConfigOrgId}",
                credential.Id,
                credential.OrganisationId,
                config.Id,
                config.OrganisationId);
            return;
        }

        // Find matching discovery service
        var service = discoveryServices.FirstOrDefault(s =>
            s.Platform.Equals(config.Platform.ToString(), StringComparison.OrdinalIgnoreCase));

        if (service == null)
        {
            _logger.LogWarning(
                "No discovery service registered for platform {Platform}, skipping config {ConfigId}",
                config.Platform,
                config.Id);
            return;
        }

        // Run discovery
        _logger.LogInformation(
            "Starting {Platform} discovery for organisation {OrgId} using credential '{CredentialName}'",
            config.Platform,
            config.OrganisationId,
            credential.Name);

        await service.DiscoverAsync(
            config,
            credential,
            cancellationToken);

        _logger.LogInformation(
            "Completed {Platform} discovery for organisation {OrgId}",
            config.Platform,
            config.OrganisationId);
    }
}
```

### 5.2 Add Configuration Section

**Location:** `src/Orchitect.Inventory.Api/appsettings.json`

**ADD:**
```json
{
  "DiscoverySettings": {
    "IntervalMinutes": 30
  }
}
```

### 5.3 Update Service Registration

**Location:** `src/Orchitect.Inventory.Api/Program.cs`

Ensure hosted service is registered:
```csharp
builder.Services.AddHostedService<DiscoveryHostedService>();
```

---

## Phase 6: Migration Strategy

### 6.1 Remove Old Settings

**Location:** `src/Orchitect.Inventory.Api/appsettings.json`

**REMOVE these sections:**
```json
"AzureSettings": {
  "IsEnabled": false,
  "SubscriptionFilters": []
},
"AzureDevOpsSettings": {
  "Organization": "",
  "PersonalAccessToken": "",
  "IsEnabled": false,
  "ProjectFilter": []
},
"GitHubSettings": {
  "AgentName": "",
  "Token": "",
  "IsEnabled": false
},
"GitLabSettings": {
  "HostUrl": "",
  "Token": "",
  "IsEnabled": false
}
```

**KEEP:**
```json
{
  "Logging": { ... },
  "AllowedHosts": "*",
  "CorsSettings": {
    "AllowedFrontend": ""
  },
  "DiscoverySettings": {
    "IntervalMinutes": 30
  }
}
```

### 6.2 Delete Old Files

**Settings Models (no longer needed):**
```bash
rm src/Orchitect.Inventory.Infrastructure/GitHub/Models/GitHubSettings.cs
rm src/Orchitect.Inventory.Infrastructure/AzureDevOps/Models/AzureDevOpsSettings.cs
rm src/Orchitect.Inventory.Infrastructure/GitLab/Models/GitLabSettings.cs
rm src/Orchitect.Inventory.Infrastructure/Azure/Models/AzureSettings.cs
rm src/Orchitect.Inventory.Infrastructure/Shared/Settings.cs
```

**Validators (no longer needed):**
```bash
rm src/Orchitect.Inventory.Infrastructure/GitHub/Validator/GitHubSettingsValidator.cs
rm src/Orchitect.Inventory.Infrastructure/AzureDevOps/Validation/AzureDevOpsSettingsValidator.cs
rm src/Orchitect.Inventory.Infrastructure/GitLab/Validator/GitLabSettingsValidator.cs
rm src/Orchitect.Inventory.Infrastructure/Azure/Validation/AzureSettingsValidator.cs
```

### 6.3 Migration Workflow for Existing Users

**Option A: Manual Migration (Recommended)**
1. Users create their organisation via Core API
2. Users create credentials via Core API (encrypted)
3. Users create discovery configurations via new Inventory API endpoints
4. Background job picks up new configurations on next cycle

**Option B: Automated Migration Script (If needed)**
Create a one-time migration endpoint:
```csharp
// src/Orchitect.Inventory.Api/Endpoints/Admin/MigrateFromAppSettingsEndpoint.cs
// Reads old appsettings, creates default org + credentials + configs
// Only callable in Development environment
```

---

## Phase 7: Testing Strategy

### 7.1 Unit Tests

**New Test Files:**
```
src/Orchitect.Inventory.Infrastructure.Tests/Discovery/DiscoveryConfigurationTests.cs
src/Orchitect.Inventory.Infrastructure.Tests/GitHub/GitHubConnectionServiceTests.cs
src/Orchitect.Inventory.Infrastructure.Tests/GitHub/GitHubDiscoveryServiceTests.cs
```

**Example - GitHubConnectionServiceTests.cs:**
```csharp
[Fact]
public void FromCredential_WithValidPAT_CreatesConnection()
{
    // Arrange
    var credential = CreateGitHubCredential();
    var resolver = CreateMockPayloadResolver();

    // Act
    var service = GitHubConnectionService.FromCredential(credential, resolver);

    // Assert
    Assert.NotNull(service.Client);
}

[Fact]
public void FromCredential_WithWrongPlatform_ThrowsException()
{
    // Arrange
    var credential = CreateAzureDevOpsCredential(); // Wrong platform
    var resolver = CreateMockPayloadResolver();

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() =>
        GitHubConnectionService.FromCredential(credential, resolver));
}
```

### 7.2 Integration Tests

**Test Scenarios:**
1. Create org → Create credential → Create discovery config → Trigger discovery
2. Delete credential → Verify cascade delete of discovery configs
3. Delete org → Verify cascade delete of credentials + configs
4. Multi-org isolation: Org A's discoveries don't appear in Org B's cache

### 7.3 Manual Testing Checklist

- [ ] Create organisation via Core API
- [ ] Create GitHub PAT credential via Core API
- [ ] Verify credential is encrypted in database
- [ ] Create discovery configuration linking org + credential
- [ ] List discovery configurations - verify it appears
- [ ] Trigger manual discovery via API
- [ ] Check logs for successful discovery
- [ ] Verify cache keys are org-specific
- [ ] Wait for background job cycle
- [ ] Verify automatic discovery runs
- [ ] Update configuration (disable)
- [ ] Verify next cycle skips disabled config
- [ ] Delete configuration
- [ ] Verify it's removed from database
- [ ] Create multiple configs for same org (GitHub + Azure DevOps)
- [ ] Verify both run in same cycle

---

## File Checklist

### New Files to Create

**Domain:**
- `src/Orchitect.Inventory.Domain/Discovery/DiscoveryConfiguration.cs`
- `src/Orchitect.Inventory.Domain/Discovery/DiscoveryConfigurationId.cs`
- `src/Orchitect.Inventory.Domain/Discovery/DiscoveryPlatform.cs`
- `src/Orchitect.Inventory.Domain/Discovery/IDiscoveryConfigurationRepository.cs`

**Persistence:**
- `src/Orchitect.Inventory.Persistence/Configurations/DiscoveryConfigurationConfiguration.cs`
- `src/Orchitect.Inventory.Persistence/Repositories/DiscoveryConfigurationRepository.cs`

**API Endpoints:**
- `src/Orchitect.Inventory.Api/Endpoints/Discovery/CreateDiscoveryConfigurationEndpoint.cs`
- `src/Orchitect.Inventory.Api/Endpoints/Discovery/ListDiscoveryConfigurationsEndpoint.cs`
- `src/Orchitect.Inventory.Api/Endpoints/Discovery/UpdateDiscoveryConfigurationEndpoint.cs`
- `src/Orchitect.Inventory.Api/Endpoints/Discovery/DeleteDiscoveryConfigurationEndpoint.cs`
- `src/Orchitect.Inventory.Api/Endpoints/Discovery/TriggerDiscoveryEndpoint.cs`

**Tests:**
- `src/Orchitect.Inventory.Infrastructure.Tests/Discovery/DiscoveryConfigurationTests.cs`
- `src/Orchitect.Inventory.Infrastructure.Tests/GitHub/GitHubConnectionServiceTests.cs`

### Files to Modify

**Domain:**
- `src/Orchitect.Inventory.Domain/Discovery/IDiscoveryService.cs`

**Infrastructure:**
- `src/Orchitect.Inventory.Infrastructure/Discovery/DiscoveryService.cs`
- `src/Orchitect.Inventory.Infrastructure/GitHub/Services/GitHubConnectionService.cs`
- `src/Orchitect.Inventory.Infrastructure/GitHub/Services/GitHubDiscoveryService.cs`
- `src/Orchitect.Inventory.Infrastructure/AzureDevOps/Services/AzureDevOpsConnectionService.cs`
- `src/Orchitect.Inventory.Infrastructure/AzureDevOps/Services/AzureDevOpsDiscoveryService.cs`
- `src/Orchitect.Inventory.Infrastructure/GitLab/Services/GitLabConnectionService.cs`
- `src/Orchitect.Inventory.Infrastructure/GitLab/Services/GitLabDiscoveryService.cs`
- `src/Orchitect.Inventory.Infrastructure/Azure/Services/AzureConnectionService.cs`
- `src/Orchitect.Inventory.Infrastructure/Azure/Services/AzureDiscoveryService.cs`
- `src/Orchitect.Inventory.Infrastructure/Extensions.cs`

**Persistence:**
- `src/Orchitect.Inventory.Persistence/InventoryDbContext.cs`
- `src/Orchitect.Inventory.Persistence/Extensions.cs`

**API:**
- `src/Orchitect.Inventory.Api/Jobs/DiscoveryHostedService.cs`
- `src/Orchitect.Inventory.Api/Endpoints.cs`
- `src/Orchitect.Inventory.Api/appsettings.json`
- `src/Orchitect.Inventory.Api/Program.cs` (verify hosted service registration)

### Files to Delete

**Settings Models:**
- `src/Orchitect.Inventory.Infrastructure/GitHub/Models/GitHubSettings.cs`
- `src/Orchitect.Inventory.Infrastructure/AzureDevOps/Models/AzureDevOpsSettings.cs`
- `src/Orchitect.Inventory.Infrastructure/GitLab/Models/GitLabSettings.cs`
- `src/Orchitect.Inventory.Infrastructure/Azure/Models/AzureSettings.cs`
- `src/Orchitect.Inventory.Infrastructure/Shared/Settings.cs`

**Validators:**
- `src/Orchitect.Inventory.Infrastructure/GitHub/Validator/GitHubSettingsValidator.cs`
- `src/Orchitect.Inventory.Infrastructure/AzureDevOps/Validation/AzureDevOpsSettingsValidator.cs`
- `src/Orchitect.Inventory.Infrastructure/GitLab/Validator/GitLabSettingsValidator.cs`
- `src/Orchitect.Inventory.Infrastructure/Azure/Validation/AzureSettingsValidator.cs`

---

## Implementation Order

### Step 1: Domain Foundation
1. Create `DiscoveryConfiguration` domain model and related files
2. Update `IDiscoveryService` interface
3. Create `IDiscoveryConfigurationRepository` interface

### Step 2: Persistence Layer
1. Add `DiscoveryConfigurationConfiguration` EF configuration
2. Implement `DiscoveryConfigurationRepository`
3. Update `InventoryDbContext`
4. Create and apply migration

### Step 3: Infrastructure Refactoring
1. Refactor connection services (add `FromCredential` factory methods)
2. Update `DiscoveryService` base class
3. Refactor all discovery service implementations
4. Update DI registrations in `Extensions.cs`

### Step 4: API Endpoints
1. Create discovery configuration endpoints
2. Register endpoints in route builder
3. Test endpoints manually

### Step 5: Background Job
1. Refactor `DiscoveryHostedService`
2. Add configuration section
3. Test background job execution

### Step 6: Cleanup
1. Remove old settings from appsettings.json
2. Delete old settings classes and validators
3. Run tests
4. Update documentation

---

## Summary

This plan transforms the Inventory discovery system from a **global, static configuration** model to an **organization-scoped, credential-based** model.

### Key Changes

1. **Domain:** New `DiscoveryConfiguration` entity links organisations, credentials, and platforms
2. **Infrastructure:** Connection/discovery services accept credentials as parameters (not IOptions)
3. **Background Job:** Runs per-organization, iterating through each org's configurations
4. **Security:** Credentials encrypted in Core, resolved at runtime via `CredentialPayloadResolver`
5. **Multi-tenancy:** Each org independently configures their discovery platforms

### Benefits

- ✅ True multi-tenancy (each org has separate credentials)
- ✅ No secrets in appsettings.json
- ✅ Dynamic configuration via API
- ✅ Encrypted credential storage using Core infrastructure
- ✅ Per-org discovery scheduling
- ✅ Graceful handling of missing/invalid credentials
- ✅ Cross-schema referential integrity with cascade deletes

### Next Steps

1. Review this plan with team
2. Create GitHub issue/tracking ticket
3. Begin implementation following the order above
4. Write tests as you go
5. Update Bruno API collection with new endpoints
6. Document new workflow in user-facing docs