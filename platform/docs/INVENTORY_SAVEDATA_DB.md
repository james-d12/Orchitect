# Inventory Discovery Data Persistence Plan

**Status:** Planning
**Created:** 2026-03-14
**Context:** Replace in-memory caching with database persistence for all discovery data

## Overview

Currently, all discovery services (GitHub, Azure DevOps, GitLab, Azure) cache their discovered data in `IMemoryCache`. This plan outlines the migration to persist all discovered data in PostgreSQL, providing durability, historical tracking, and multi-instance support.

## Current State Analysis

### What's Currently Cached (Not Persisted)

| Platform | Cached Data | Cache Key Pattern |
|----------|-------------|-------------------|
| GitHub | Repositories, Pipelines, PullRequests, Teams (if applicable) | `GitHub:{Type}:{orgId}` |
| Azure DevOps | Repositories, Pipelines, PullRequests, WorkItems, Teams, ~~Projects~~ | `AzureDevOps:{Type}:{orgId}` |
| GitLab | Repositories, Pipelines, PullRequests, Groups (as Teams) | `gitlab-{type}` (static, no org isolation) |
| Azure | CloudResources | `Azure:CloudResources:{orgId}` |

**Note:** AzureDevOps "Projects" will not be persisted as a separate entity - they are represented by the existing `Owner` entity.

### What's Already Persisted

- **DiscoveryConfiguration** (with repository) - Discovery job configuration
- Domain entities exist in DbContext but **not populated by discovery services**:
  - Repository, Pipeline, PullRequest (Git domain)
  - WorkItem, User (Ticketing domain)
  - CloudResource, CloudSecret (Cloud domain)
  - Owner (nested in Repository/Pipeline)

### Key Problems

1. **Data Loss**: Cache cleared on app restart, no persistence
2. **No Multi-Instance Support**: Shared cache state across instances
3. **No Historical Tracking**: No audit trail of discovery runs
4. **Incomplete Organization Isolation**: Discovered entities lack OrganisationId FK
5. **Missing Repositories**: Only DiscoveryConfiguration has repository pattern
6. **Missing Team Entity**: Teams are cached but not persisted as domain entities

## Architecture Changes

### 1. Domain Model Updates

#### Add Organization Context to All Discovered Entities

All discovered entities need explicit organization ownership:

**Entities to Update:**
- `Repository` - Add OrganisationId FK
- `Pipeline` - Add OrganisationId FK
- `PullRequest` - Add OrganisationId FK
- `WorkItem` - Add OrganisationId FK
- `CloudResource` - Add OrganisationId FK
- `CloudSecret` - Add OrganisationId FK (if not already present)
- `Owner` - Add OrganisationId FK
- `Team` - NEW domain entity (see below)

**Schema Changes:**
```csharp
public sealed record Repository(
    RepositoryId Id,
    OrganisationId OrganisationId,  // NEW
    string Name,
    string Url,
    string DefaultBranch,
    Owner Owner,
    Platform Platform,
    DateTime DiscoveredAt,          // NEW
    DateTime UpdatedAt              // NEW
);
```

#### New Domain Entity: Team

**Decision:** Create `Team` as a new domain entity; discard `Project` (use existing `Owner` entity instead).

**Rationale:**
- **Team** is a valuable cross-platform concept:
  - AzureDevOps: Teams within Projects (user groups with permissions)
  - GitHub: Teams within Organizations (user groups, can own repos)
  - GitLab: Groups (hierarchical, similar to teams)
  - Jira: Teams (for capacity planning, ownership)
- **Project** is NOT consistent across platforms:
  - AzureDevOps: Projects are containers for repos/pipelines/boards
  - GitHub: No project concept (repos live under organizations)
  - GitLab: "Project" = their term for repository (confusing!)
  - Existing `Owner` entity already captures the grouping/namespace concept

**Team Domain Entity:**
```csharp
// src/Orchitect.Domain/Inventory/Shared/Team.cs
public sealed record Team(
    TeamId Id,
    OrganisationId OrganisationId,
    string Name,
    string? Description,
    string Url,
    Platform Platform,
    DateTime DiscoveredAt,
    DateTime UpdatedAt
);

public sealed record TeamId(string Value);
```

**Project Handling (Use Owner):**
For AzureDevOps repositories, the `Owner` entity represents the project:
```csharp
// AzureDevOps repository discovery
var repository = new Repository(
    id: new RepositoryId(azureRepo.Id),
    organisationId: config.OrganisationId,
    name: azureRepo.Name,
    url: azureRepo.Url,
    defaultBranch: azureRepo.DefaultBranch,
    owner: new Owner(
        id: new OwnerId(azureProject.Id),      // Owner IS the project
        name: azureProject.Name,                // "MyAzureDevOpsProject"
        description: azureProject.Description,
        url: azureProject.Url,
        platform: Platform.AzureDevOps
    ),
    platform: Platform.AzureDevOps,
    discoveredAt: DateTime.UtcNow,
    updatedAt: DateTime.UtcNow
);
```

This approach:
- Reuses existing `Owner` entity (no new Project table needed)
- Owner naturally represents "what owns this repo" (in AzureDevOps that's the project)
- Simpler model with fewer entities to maintain

#### Add Audit Fields

All discovered entities should track:
- `DateTime DiscoveredAt` - First discovery timestamp (immutable)
- `DateTime UpdatedAt` - Last seen/updated timestamp
- `Guid? DiscoveryRunId` - Link to specific discovery run (optional, for traceability)

### 2. Repository Pattern Implementation

Create repository interfaces and implementations for all discovery entities.

#### New Repository Interfaces

**Location:** `src/Orchitect.Domain/Inventory/{Domain}/Service/`

```csharp
// Git repositories
public interface IRepositoryRepository : IRepository<Repository, RepositoryId>
{
    Task<IReadOnlyList<Repository>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Repository>> GetByPlatformAsync(
        OrganisationId organisationId,
        Platform platform,
        CancellationToken cancellationToken = default);

    Task<Repository?> GetByUrlAsync(
        string url,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        Repository repository,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<Repository> repositories,
        CancellationToken cancellationToken = default);
}

// Pipelines
public interface IPipelineRepository : IRepository<Pipeline, PipelineId>
{
    Task<IReadOnlyList<Pipeline>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Pipeline>> GetByPlatformAsync(
        OrganisationId organisationId,
        Platform platform,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<Pipeline> pipelines,
        CancellationToken cancellationToken = default);
}

// Pull Requests
public interface IPullRequestRepository : IRepository<PullRequest, PullRequestId>
{
    Task<IReadOnlyList<PullRequest>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PullRequest>> GetByRepositoryAsync(
        string repositoryUrl,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PullRequest>> GetActiveAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<PullRequest> pullRequests,
        CancellationToken cancellationToken = default);
}

// Work Items
public interface IWorkItemRepository : IRepository<WorkItem, WorkItemId>
{
    Task<IReadOnlyList<WorkItem>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetByPlatformAsync(
        OrganisationId organisationId,
        Platform platform,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<WorkItem> workItems,
        CancellationToken cancellationToken = default);
}

// Cloud Resources
public interface ICloudResourceRepository : IRepository<CloudResource, CloudResourceId>
{
    Task<IReadOnlyList<CloudResource>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CloudResource>> GetByPlatformAsync(
        OrganisationId organisationId,
        Platform platform,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<CloudResource> cloudResources,
        CancellationToken cancellationToken = default);
}

// Owner (nested in Repo/Pipeline)
public interface IOwnerRepository : IRepository<Owner, OwnerId>
{
    Task<Owner?> GetByNameAndPlatformAsync(
        string name,
        Platform platform,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        Owner owner,
        CancellationToken cancellationToken = default);
}

// Teams
public interface ITeamRepository : IRepository<Team, TeamId>
{
    Task<IReadOnlyList<Team>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Team>> GetByPlatformAsync(
        OrganisationId organisationId,
        Platform platform,
        CancellationToken cancellationToken = default);

    Task<Team?> GetByUrlAsync(
        string url,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<Team> teams,
        CancellationToken cancellationToken = default);
}
```

#### Repository Implementations

**Location:** `src/Orchitect.Persistence/Repositories/Inventory/`

Each repository should:
- Inherit from base `Repository<T, TId>` class (if available) or implement from scratch
- Use `OrchitectDbContext` for data access
- Implement bulk upsert logic (insert or update based on natural key like URL)
- Handle organization isolation via OrganisationId filter
- Use EF Core `ExecuteUpdate` for efficient bulk operations where appropriate

**Example: RepositoryRepository.cs**
```csharp
public sealed class RepositoryRepository : IRepositoryRepository
{
    private readonly OrchitectDbContext _context;

    public RepositoryRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Repository>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Repositories
            .Where(r => r.OrganisationId == organisationId)
            .Include(r => r.Owner)
            .ToListAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(
        IEnumerable<Repository> repositories,
        CancellationToken cancellationToken = default)
    {
        foreach (var repo in repositories)
        {
            // Find existing by URL (natural key)
            var existing = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Url == repo.Url, cancellationToken);

            if (existing is null)
            {
                // New discovery
                _context.Repositories.Add(repo);
            }
            else
            {
                // Update existing (preserve DiscoveredAt, update UpdatedAt)
                _context.Entry(existing).CurrentValues.SetValues(repo);
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Implement other interface methods...
}
```

### 3. EF Core Configuration Updates

#### Update Existing Configurations

**Files to Update:**
- `CloudResourceConfiguration.cs` - Add OrganisationId FK and indexes
- `CloudSecretConfiguration.cs` - Add OrganisationId FK
- `PipelineConfiguration.cs` - Add OrganisationId FK and indexes
- `RepositoryConfiguration.cs` - Add OrganisationId FK and indexes
- `PullRequestConfiguration.cs` - Add OrganisationId FK and indexes
- `WorkItemConfiguration.cs` - Add OrganisationId FK and indexes
- `OwnerConfiguration.cs` - Add OrganisationId FK

**New Configuration to Create:**
- `TeamConfiguration.cs` - New entity with OrganisationId FK, indexes on (OrganisationId, Platform), unique index on Url

**Example Configuration Update:**
```csharp
public sealed class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("Repositories", "inventory");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new RepositoryId(value));

        // NEW: Organisation FK with cascade delete
        builder.Property(r => r.OrganisationId)
            .HasConversion(id => id.Value, value => new OrganisationId(value))
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(r => r.OrganisationId)
            .HasConstraintName("FK_Repositories_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        // NEW: Indexes for queries
        builder.HasIndex(r => r.OrganisationId)
            .HasDatabaseName("IX_Repositories_OrganisationId");

        builder.HasIndex(r => new { r.OrganisationId, r.Platform })
            .HasDatabaseName("IX_Repositories_OrganisationId_Platform");

        builder.HasIndex(r => r.Url)
            .IsUnique()
            .HasDatabaseName("IX_Repositories_Url");

        // NEW: Audit timestamps
        builder.Property(r => r.DiscoveredAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .IsRequired();

        // Existing configuration...
        builder.Property(r => r.Name).IsRequired().HasMaxLength(500);
        builder.Property(r => r.Url).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.DefaultBranch).HasMaxLength(200);

        builder.HasOne(r => r.Owner)
            .WithMany()
            .HasForeignKey("OwnerId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.Platform)
            .HasConversion<string>()
            .IsRequired();
    }
}
```

### 4. Discovery Service Updates

#### Remove Cache, Add Repository Persistence

Update all discovery services to persist instead of cache:

**Services to Update:**
- `GitHubDiscoveryService.cs`
- `AzureDevOpsDiscoveryService.cs`
- `GitLabDiscoveryService.cs`
- `AzureDiscoveryService.cs`

**Example: GitHubDiscoveryService.cs**

**Before:**
```csharp
var repositories = await gitHubService.GetRepositoriesAsync();
var pullRequests = new List<GitHubPullRequest>();
var pipelines = new List<GitHubPipeline>();

// ... collect data ...

var orgId = configuration.OrganisationId.Value;
_memoryCache.Set($"GitHub:Repositories:{orgId}", repositories);
_memoryCache.Set($"GitHub:Pipelines:{orgId}", pipelines);
_memoryCache.Set($"GitHub:PullRequests:{orgId}", pullRequests);
```

**After:**
```csharp
private readonly IRepositoryRepository _repositoryRepository;
private readonly IPipelineRepository _pipelineRepository;
private readonly IPullRequestRepository _pullRequestRepository;
private readonly IOwnerRepository _ownerRepository;

public GitHubDiscoveryService(
    ILogger<GitHubDiscoveryService> logger,
    CredentialPayloadResolver payloadResolver,
    IRepositoryRepository repositoryRepository,
    IPipelineRepository pipelineRepository,
    IPullRequestRepository pullRequestRepository,
    IOwnerRepository ownerRepository) : base(logger)
{
    _payloadResolver = payloadResolver;
    _repositoryRepository = repositoryRepository;
    _pipelineRepository = pipelineRepository;
    _pullRequestRepository = pullRequestRepository;
    _ownerRepository = ownerRepository;
}

protected override async Task StartAsync(
    DiscoveryConfiguration configuration,
    Credential credential,
    CancellationToken cancellationToken)
{
    using var activity = Tracing.StartActivity();

    var connectionService = GitHubConnectionService.FromCredential(
        credential,
        _payloadResolver,
        configuration.PlatformConfig);

    var gitHubService = new GitHubService(connectionService);

    // Discover repositories
    var githubRepositories = await gitHubService.GetRepositoriesAsync();

    // Convert to domain entities with OrganisationId
    var repositories = githubRepositories.Select(gr => new Repository(
        new RepositoryId(gr.Id),
        configuration.OrganisationId,  // NEW
        gr.Name,
        gr.Url,
        gr.DefaultBranch,
        gr.Owner,  // Upsert owner separately
        gr.Platform,
        DateTime.UtcNow,  // DiscoveredAt
        DateTime.UtcNow   // UpdatedAt
    )).ToList();

    // Upsert owners first (FK constraint)
    var owners = repositories.Select(r => r.Owner).Distinct();
    foreach (var owner in owners)
    {
        await _ownerRepository.UpsertAsync(owner, cancellationToken);
    }

    // Bulk upsert repositories
    await _repositoryRepository.BulkUpsertAsync(repositories, cancellationToken);

    // Discover and persist pipelines
    var pipelines = new List<Pipeline>();
    foreach (var repository in repositories)
    {
        var repoPipelines = await gitHubService.GetPipelinesAsync(repository);
        pipelines.AddRange(repoPipelines.Select(p => new Pipeline(
            new PipelineId(p.Id),
            configuration.OrganisationId,
            p.Name,
            p.Url,
            p.Owner,
            p.Platform,
            DateTime.UtcNow,
            DateTime.UtcNow
        )));
    }
    await _pipelineRepository.BulkUpsertAsync(pipelines, cancellationToken);

    // Discover and persist pull requests
    var pullRequests = new List<PullRequest>();
    foreach (var repository in repositories)
    {
        var repoPRs = await gitHubService.GetPullRequestsAsync(repository);
        pullRequests.AddRange(repoPRs.Select(pr => new PullRequest(
            new PullRequestId(pr.Id),
            configuration.OrganisationId,
            pr.Name,
            pr.Description,
            pr.Url,
            pr.Labels,
            pr.Reviewers,
            pr.Status,
            pr.Platform,
            pr.LastCommit,
            pr.RepositoryUrl,
            pr.RepositoryName,
            pr.CreatedOnDate,
            DateTime.UtcNow,  // DiscoveredAt
            DateTime.UtcNow   // UpdatedAt
        )));
    }
    await _pullRequestRepository.BulkUpsertAsync(pullRequests, cancellationToken);

    Logger.LogInformation(
        "GitHub discovery completed for organisation {OrgId}: {RepoCount} repos, {PipelineCount} pipelines, {PRCount} PRs",
        configuration.OrganisationId,
        repositories.Count,
        pipelines.Count,
        pullRequests.Count);
}
```

### 5. Query Service Updates

#### Replace Cache Reads with Repository Queries

Update all query services to read from database instead of cache:

**Services to Update:**
- `GitHubGitQueryService.cs`
- `AzureDevOpsGitQueryService.cs`
- `GitLabGitQueryService.cs`
- `AzureDevOpsTicketingQueryService.cs`
- `AzureCloudQueryService.cs`

**Example: GitHubGitQueryService.cs**

**Before:**
```csharp
public Task<List<Repository>> QueryRepositories(RepositoryQueryRequest request)
{
    var cachedRepositories = _memoryCache.Get<List<GitHubRepository>>(
        $"GitHub:Repositories:{request.OrganisationId}") ?? [];

    return Task.FromResult(cachedRepositories.Cast<Repository>().ToList());
}
```

**After:**
```csharp
private readonly IRepositoryRepository _repositoryRepository;

public GitHubGitQueryService(IRepositoryRepository repositoryRepository)
{
    _repositoryRepository = repositoryRepository;
}

public async Task<List<Repository>> QueryRepositories(RepositoryQueryRequest request)
{
    var repositories = await _repositoryRepository.GetByPlatformAsync(
        request.OrganisationId,
        Platform.GitHub,
        request.CancellationToken);

    // Apply filters from request (if any)
    var filtered = repositories.AsEnumerable();

    if (!string.IsNullOrEmpty(request.NameFilter))
    {
        filtered = filtered.Where(r => r.Name.Contains(
            request.NameFilter,
            StringComparison.OrdinalIgnoreCase));
    }

    return filtered.ToList();
}
```

### 6. Service Registration Updates

Update dependency injection registration:

**File:** `src/Orchitect.Inventory.Persistence/PersistenceExtensions.cs` (or equivalent)

```csharp
public static IServiceCollection AddInventoryPersistenceServices(
    this IServiceCollection services)
{
    // Existing
    services.AddScoped<IDiscoveryConfigurationRepository, DiscoveryConfigurationRepository>();

    // NEW: Register all new repositories
    services.AddScoped<IRepositoryRepository, RepositoryRepository>();
    services.AddScoped<IPipelineRepository, PipelineRepository>();
    services.AddScoped<IPullRequestRepository, PullRequestRepository>();
    services.AddScoped<IWorkItemRepository, WorkItemRepository>();
    services.AddScoped<ICloudResourceRepository, CloudResourceRepository>();
    services.AddScoped<IOwnerRepository, OwnerRepository>();
    services.AddScoped<ITeamRepository, TeamRepository>();

    return services;
}
```

### 7. Migration Strategy

#### Create New Migration

```bash
cd src/Orchitect.Persistence
dotnet ef migrations add AddOrganisationIdToInventoryEntities
```

**Migration will include:**
- Add `OrganisationId` column to all existing inventory tables
- Add FK constraints to `core.Organisations` with CASCADE DELETE
- Add indexes on `OrganisationId` and `(OrganisationId, Platform)`
- Add unique indexes on natural keys (e.g., Repository.Url)
- Add `DiscoveredAt` and `UpdatedAt` timestamp columns to all entities
- **Create new `Teams` table** with OrganisationId, Name, Description, Url, Platform, DiscoveredAt, UpdatedAt
- Backfill existing data (if any) with default OrganisationId (MANUAL STEP)

**Data Migration Considerations:**
- Existing cached data will be lost (acceptable - ephemeral)
- Existing persisted entities (if any) need OrganisationId backfill
- Consider clearing CloudResources/CloudSecrets tables if existing data lacks org context

## Implementation Checklist

### Phase 1: Domain & Persistence Foundation
- [ ] Update all discovery entity records to include OrganisationId, DiscoveredAt, UpdatedAt
- [ ] Create new `Team` domain entity in `src/Orchitect.Domain/Inventory/Shared/`
- [ ] Create repository interfaces in Domain layer (7 new interfaces including ITeamRepository)
- [ ] Create repository implementations in Persistence layer (7 new classes including TeamRepository)
- [ ] Update EF Core configurations for all entities (7 files updated)
- [ ] Create new `TeamConfiguration` in Persistence layer
- [ ] Create migration: `AddOrganisationIdAndTeamToInventory`
- [ ] Review migration SQL for correctness
- [ ] Update service registration in Persistence extensions (add TeamRepository)

### Phase 2: Discovery Service Refactoring
- [ ] Update GitHubDiscoveryService - inject repositories, remove cache, discover/persist teams
- [ ] Update AzureDevOpsDiscoveryService - inject repositories, remove cache, discover/persist teams
- [ ] Update GitLabDiscoveryService - inject repositories, remove cache, discover/persist groups as teams
- [ ] Update AzureDiscoveryService - inject repositories, remove cache
- [ ] Remove IMemoryCache dependency from all discovery services
- [ ] Add logging for persistence operations (count of entities upserted including teams)

### Phase 3: Query Service Refactoring
- [ ] Update GitHubGitQueryService - inject repository, query DB
- [ ] Update AzureDevOpsGitQueryService - inject repository, query DB
- [ ] Update GitLabGitQueryService - inject repository, query DB
- [ ] Update AzureDevOpsTicketingQueryService - inject repository, query DB
- [ ] Update AzureCloudQueryService - inject repository, query DB
- [ ] Remove IMemoryCache dependency from all query services

### Phase 4: Testing & Validation
- [ ] Run migration on dev database
- [ ] Test discovery run for each platform (GitHub, AzureDevOps, GitLab, Azure)
- [ ] Verify data persists across app restarts
- [ ] Verify query services return correct data from DB
- [ ] Test organization isolation (multi-org scenarios)
- [ ] Test upsert logic (run discovery twice, verify updates)
- [ ] Performance test bulk upserts (100+ repositories)
- [ ] Update unit tests for discovery services (mock repositories)
- [ ] Update unit tests for query services (mock repositories)
- [ ] Update integration tests if applicable

### Phase 5: Cleanup
- [ ] Remove all `_memoryCache.Set()` calls for discovery data
- [ ] Remove all `_memoryCache.Get()` calls for discovery data
- [ ] Consider removing IMemoryCache from discovery/query service constructors
- [ ] Update API documentation (if discovery data queries exposed)
- [ ] Update INVENTORY_INTEGRATION_CONFIG_PLAN.md with persistence details

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Performance degradation on large datasets | Medium | High | Implement pagination in repository queries, add database indexes, benchmark bulk upserts |
| Migration failure on existing data | Low | Medium | Test migration on copy of production data, provide rollback script |
| FK constraint violations | Low | High | Ensure Owner entities upserted before Repositories/Pipelines, handle orphaned data |
| Duplicate key violations | Medium | Medium | Implement proper upsert logic using URL as natural key, handle conflicts gracefully |
| Query service performance | Medium | Medium | Add appropriate indexes (OrganisationId, Platform), consider EF query optimization |
| Multi-instance concurrency | Low | Low | EF Core optimistic concurrency (if needed), database-level uniqueness constraints |

## Performance Considerations

### Database Indexes (Critical)

```sql
-- Repository table
CREATE INDEX IX_Repositories_OrganisationId ON inventory.Repositories(OrganisationId);
CREATE INDEX IX_Repositories_OrganisationId_Platform ON inventory.Repositories(OrganisationId, Platform);
CREATE UNIQUE INDEX IX_Repositories_Url ON inventory.Repositories(Url);

-- Pipeline table
CREATE INDEX IX_Pipelines_OrganisationId ON inventory.Pipelines(OrganisationId);
CREATE INDEX IX_Pipelines_OrganisationId_Platform ON inventory.Pipelines(OrganisationId, Platform);

-- PullRequest table
CREATE INDEX IX_PullRequests_OrganisationId ON inventory.PullRequests(OrganisationId);
CREATE INDEX IX_PullRequests_RepositoryUrl ON inventory.PullRequests(RepositoryUrl);
CREATE INDEX IX_PullRequests_Status ON inventory.PullRequests(Status) WHERE Status IN ('Active', 'Draft');

-- WorkItem table
CREATE INDEX IX_WorkItems_OrganisationId ON inventory.WorkItems(OrganisationId);
CREATE INDEX IX_WorkItems_OrganisationId_Platform ON inventory.WorkItems(OrganisationId, Platform);

-- Team table
CREATE INDEX IX_Teams_OrganisationId ON inventory.Teams(OrganisationId);
CREATE INDEX IX_Teams_OrganisationId_Platform ON inventory.Teams(OrganisationId, Platform);
CREATE UNIQUE INDEX IX_Teams_Url ON inventory.Teams(Url);
```

### Bulk Operations

- Use EF Core `AddRange()` for bulk inserts
- Consider batching updates (500-1000 entities per transaction)
- Use `ExecuteUpdate()` for bulk timestamp updates
- Profile and optimize N+1 query patterns

### Caching Strategy (Optional Future Enhancement)

- Consider second-level caching (Redis) for frequently accessed discovery data
- Cache invalidation on discovery completion
- TTL-based cache expiry aligned with discovery schedule

## Testing Strategy

### Unit Tests

**New Test Files:**
- `RepositoryRepositoryTests.cs` - Test bulk upsert, organization filtering
- `PipelineRepositoryTests.cs` - Test bulk upsert, platform filtering
- `PullRequestRepositoryTests.cs` - Test bulk upsert, status filtering
- `WorkItemRepositoryTests.cs` - Test bulk upsert
- `CloudResourceRepositoryTests.cs` - Test bulk upsert
- `TeamRepositoryTests.cs` - Test bulk upsert, organization/platform filtering

**Updated Test Files:**
- `GitHubDiscoveryServiceTests.cs` - Mock repositories instead of cache
- `AzureDevOpsDiscoveryServiceTests.cs` - Mock repositories instead of cache
- `GitHubGitQueryServiceTests.cs` - Mock repository queries
- `AzureDevOpsGitQueryServiceTests.cs` - Mock repository queries

### Integration Tests

- End-to-end discovery run with database persistence
- Multi-organization isolation verification
- Concurrent discovery runs (if multi-instance support required)

### Performance Tests

- Benchmark bulk upsert with 1000+ repositories
- Measure query performance with large datasets
- Monitor database connection pool usage

## Rollback Plan

If issues arise post-deployment:

1. **Immediate:** Revert code to use cache (deploy previous version)
2. **Data:** No data loss risk (cache was ephemeral anyway)
3. **Database:** Roll back migration if FK constraints cause issues
4. **Gradual Rollout:** Deploy discovery persistence per platform (GitHub first, then AzureDevOps, etc.)

## Open Questions

1. **Historical Tracking**: Should we keep a history of discovered items (e.g., track when a repository was deleted)?
2. **Discovery Run Metadata**: Should we create a `DiscoveryRun` entity to track each execution (start time, end time, items discovered, errors)?
3. **Soft Deletes**: If a repository is not discovered in a subsequent run, should we soft-delete or hard-delete?
4. **Pagination**: Should repository interfaces support pagination from day one, or add later?
5. **Caching Layer**: Should we add distributed caching (Redis) after DB persistence, or rely on DB queries?
6. **Team Relationships**: Should we add explicit relationships between Teams and Repositories/Pipelines they own (TeamId FK on Repository/Pipeline)?

## Success Criteria

- [ ] All discovery data persists across application restarts
- [ ] Query services return data from database, not cache
- [ ] Multi-instance deployments share persisted discovery data
- [ ] Discovery runs complete without performance degradation (<2x current runtime)
- [ ] Organization isolation verified (org A cannot see org B's data)
- [ ] Upsert logic prevents duplicate entries (URL uniqueness enforced)
- [ ] Zero data loss during migration
- [ ] All existing tests pass with updated persistence layer

## Timeline Estimate

- **Phase 1 (Domain & Persistence):** 2-3 days
- **Phase 2 (Discovery Services):** 2-3 days
- **Phase 3 (Query Services):** 1-2 days
- **Phase 4 (Testing):** 2-3 days
- **Phase 5 (Cleanup):** 0.5-1 day

**Total:** 8-12 days (individual developer)

## Related Documentation

- `DISCOVERY_CREDENTIAL_INTEGRATION_PLAN.md` - Discovery service architecture
- `INVENTORY_INTEGRATION_CONFIG_PLAN.md` - Integration configuration design
- `HIGH_LEVEL_ARCHITECTURE.md` - Overall system architecture
- `INFRASTRUCTURE_STATE_MANAGEMENT.md` - State management patterns
