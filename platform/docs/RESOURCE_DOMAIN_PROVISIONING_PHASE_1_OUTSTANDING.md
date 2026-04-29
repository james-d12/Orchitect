# Outstanding Work: Resource Domain — Persistence & Domain Gaps (Phase 1)

## Context

The `RESOURCE_DOMAIN_REFACTOR_PLAN.md` refactored the Engine domain models (`Resource`, `ResourceInstance`, `ResourceDependencyGraph`) but scoped out the persistence layer intentionally. This document captures everything that must be built before any of those aggregates can be read from or written to the database.

---

## Gap Summary

| Area | Item | Status |
|---|---|---|
| DbContext | `DbSet<ResourceInstance>` | Missing |
| DbContext | `DbSet<ResourceDependencyGraph>` | Missing |
| EF Configuration | `ResourceInstanceConfiguration` | Missing |
| EF Configuration | `ResourceDependencyGraphConfiguration` | Missing |
| Repository impl | `ResourceRepository` | Missing |
| Repository impl | `ResourceInstanceRepository` | Missing |
| Repository impl | `ResourceDependencyGraphRepository` | Missing |
| DI registration | `IResourceRepository` | Missing |
| DI registration | `IResourceInstanceRepository` | Missing |
| DI registration | `IResourceDependencyGraphRepository` | Missing |
| Migration | Tables for new aggregates | Missing |
| Domain | `Application` factory pattern inconsistency | Inconsistent |
| Domain | `IApplicationRepository` missing org-scoped query | Missing |
| Pre-existing | `Service` has no `DbSet` or repository | Pre-existing gap |

---

## 1. `OrchitectDbContext` — Missing DbSet Properties

**File:** `src/Orchitect.Persistence/OrchitectDbContext.cs`

Add the following two `DbSet` properties alongside the existing Engine sets:

```csharp
public DbSet<ResourceInstance> ResourceInstances { get; init; } = null!;
public DbSet<ResourceDependencyGraph> ResourceDependencyGraphs { get; init; } = null!;
```

> **Note:** `Service` also has no `DbSet` (pre-existing gap, not introduced by the refactor). Include `DbSet<Service> Services { get; init; } = null!;` if Service is to be persisted in this phase, otherwise leave for a separate ticket.

---

## 2. EF Configuration — `ResourceInstanceConfiguration`

**File to create:** `src/Orchitect.Persistence/Configurations/Engine/ResourceInstanceConfiguration.cs`

Key mapping decisions:

- `Id`, `OrganisationId`, `ResourceId`, `EnvironmentId`, `TemplateVersionId` — standard `HasConversion` from typed ID struct to `Guid`, all `IsRequired()`
- `Status` — store as `string`: `.HasConversion<string>().IsRequired()`
- `Output` — nullable owned entity (`OwnsOne`); `Location` maps as `text` (Uri → string conversion), `Workspace` as nullable `text`
- `InputParameters` — JSONB column: `.HasColumnType("jsonb").IsRequired()`; EF accesses via backing field since the property type is `IReadOnlyDictionary<string, JsonElement>`
- `Name`, `CreatedAt`, `UpdatedAt` — standard `IsRequired()`
- Index on `(ResourceId, EnvironmentId)` to support the `GetByResourceAsync` and `GetByEnvironmentAsync` query methods

```csharp
internal sealed class ResourceInstanceConfiguration : IEntityTypeConfiguration<ResourceInstance>
{
    public void Configure(EntityTypeBuilder<ResourceInstance> builder)
    {
        builder.ToTable("ResourceInstances");
        builder.HasKey(ri => ri.Id);
        builder.HasIndex(ri => new { ri.ResourceId, ri.EnvironmentId });

        // Strongly-typed ID conversions
        builder.Property(ri => ri.Id)
            .HasConversion(id => id.Value, value => new ResourceInstanceId(value)).IsRequired();
        builder.Property(ri => ri.OrganisationId)
            .HasConversion(id => id.Value, value => new OrganisationId(value)).IsRequired();
        builder.Property(ri => ri.ResourceId)
            .HasConversion(id => id.Value, value => new ResourceId(value)).IsRequired();
        builder.Property(ri => ri.EnvironmentId)
            .HasConversion(id => id.Value, value => new EnvironmentId(value)).IsRequired();
        builder.Property(ri => ri.TemplateVersionId)
            .HasConversion(id => id.Value, value => new ResourceTemplateVersionId(value)).IsRequired();

        // Scalar properties
        builder.Property(ri => ri.Name).IsRequired();
        builder.Property(ri => ri.Status).HasConversion<string>().IsRequired();
        builder.Property(ri => ri.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(ri => ri.UpdatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");

        // InputParameters stored as JSONB
        builder.Property(ri => ri.InputParameters)
            .HasColumnType("jsonb")
            .IsRequired();

        // Output as nullable owned entity (columns inline on ResourceInstances table)
        builder.OwnsOne(ri => ri.Output, output =>
        {
            output.Property(o => o.Location)
                .HasConversion(uri => uri.ToString(), value => new Uri(value))
                .HasColumnName("OutputLocation");
            output.Property(o => o.Workspace).HasColumnName("OutputWorkspace");
        });
    }
}
```

---

## 3. EF Configuration — `ResourceDependencyGraphConfiguration`

**File to create:** `src/Orchitect.Persistence/Configurations/Engine/ResourceDependencyGraphConfiguration.cs`

`ResourceDependencyGraph` holds its nodes and edges in a private `Dictionary<ResourceId, ResourceDependencyNode>` field. Two persistence approaches are available:

### Option A — JSONB column (recommended for Phase 1)

Store the entire node/edge structure as a single `jsonb` column on the `ResourceDependencyGraphs` table. Simple to implement; avoids a separate edges table. Acceptable for Phase 1 since graphs are loaded and operated on as a whole unit (no need to query individual edges from SQL).

- Add a serialisable DTO that maps `_nodes` to/from JSON
- Configure via `HasConversion` on a string property backed by the private field, or use `ToJson()` (EF Core 8+)

### Option B — Normalised edges table (recommended for Phase 2)

Create a `ResourceDependencyEdges` table: `(GraphId UUID, FromResourceId UUID, ToResourceId UUID, PRIMARY KEY (GraphId, FromResourceId, ToResourceId))`. EF Core owned collection with `OwnsMany`. Enables per-edge queries and proper FK constraints to `Resources`.

**For Phase 1 use Option A.** Document the migration to Option B as a future item.

```csharp
internal sealed class ResourceDependencyGraphConfiguration : IEntityTypeConfiguration<ResourceDependencyGraph>
{
    public void Configure(EntityTypeBuilder<ResourceDependencyGraph> builder)
    {
        builder.ToTable("ResourceDependencyGraphs");
        builder.HasKey(g => g.Id);
        builder.HasIndex(g => new { g.OrganisationId, g.EnvironmentId }).IsUnique();

        builder.Property(g => g.Id)
            .HasConversion(id => id.Value, value => new ResourceDependencyGraphId(value)).IsRequired();
        builder.Property(g => g.OrganisationId)
            .HasConversion(id => id.Value, value => new OrganisationId(value)).IsRequired();
        builder.Property(g => g.EnvironmentId)
            .HasConversion(id => id.Value, value => new EnvironmentId(value)).IsRequired();

        // Phase 1: serialise nodes/edges as JSONB (see Option B note above for Phase 2 path)
        // Requires either a conversion on a shadow property or exposing nodes via a DTO.
        // Design decision needed: see implementation notes below.
    }
}
```

> **Implementation note:** `ResourceDependencyGraph._nodes` is a private field. To persist it, either:
> (a) Add an internal/package-visible property that exposes a serialisable snapshot for EF use only, or
> (b) Use EF Core's `UsePropertyAccessMode(PropertyAccessMode.Field)` with a backing field named `_nodes` and a custom value converter to/from `string` (JSONB).
> Pick one approach and document it consistently.

---

## 4. Repository Implementations

All three follow the same pattern as `EnvironmentRepository`. Create under `src/Orchitect.Persistence/Repositories/Engine/`.

### 4a. `ResourceRepository`

**Interface:** `IResourceRepository` (in `Orchitect.Domain.Engine.Resource`)

Methods beyond base `IRepository<Resource, ResourceId>`:
- `UpdateAsync` — `_dbContext.Resources.Update(resource); await SaveChanges()`
- `DeleteAsync` — find by Id, remove, save
- `GetByEnvironmentAsync` — `_dbContext.Resources.AsNoTracking().Where(r => r.EnvironmentId == environmentId).ToListAsync()`

### 4b. `ResourceInstanceRepository`

**Interface:** `IResourceInstanceRepository` (in `Orchitect.Domain.Engine.ResourceInstance`)

Methods beyond base:
- `UpdateAsync` — `_dbContext.ResourceInstances.Update(instance); await SaveChanges()`
- `DeleteAsync` — find, remove, save
- `GetByResourceAsync` — filter on `ResourceId`
- `GetByEnvironmentAsync` — filter on `EnvironmentId`

### 4c. `ResourceDependencyGraphRepository`

**Interface:** `IResourceDependencyGraphRepository` (in `Orchitect.Domain.Engine.ResourceDependency`)

Methods beyond base:
- `UpdateAsync` — `_dbContext.ResourceDependencyGraphs.Update(graph); await SaveChanges()`
- `GetByEnvironmentAsync` — filter on `EnvironmentId`; there is at most one graph per environment (the unique index enforces this)

---

## 5. DI Registration

**File:** `src/Orchitect.Persistence/OrchitectPersistenceExtensions.cs`

Add the three new registrations alongside the existing Engine block:

```csharp
// Add required usings:
// using Orchitect.Domain.Engine.Resource;
// using Orchitect.Domain.Engine.ResourceInstance;
// using Orchitect.Domain.Engine.ResourceDependency;

services.TryAddScoped<IResourceRepository, ResourceRepository>();
services.TryAddScoped<IResourceInstanceRepository, ResourceInstanceRepository>();
services.TryAddScoped<IResourceDependencyGraphRepository, ResourceDependencyGraphRepository>();
```

---

## 6. EF Migration

Once items 1–4 are complete, generate a single migration:

```bash
./scripts/efm.sh add_resource_instance_and_dependency_graph
```

Expected `Up()` operations:
- `CreateTable("ResourceInstances", ...)` — with `OutputLocation`, `OutputWorkspace` inline columns and a `InputParameters jsonb` column
- `CreateTable("ResourceDependencyGraphs", ...)` — with `Nodes jsonb` column (Phase 1) or `CreateTable("ResourceDependencyEdges", ...)` (Phase 2)
- `CreateIndex("IX_ResourceInstances_ResourceId_EnvironmentId", ...)`
- `CreateIndex("IX_ResourceDependencyGraphs_OrganisationId_EnvironmentId", ..., unique: true)`

---

## 7. Application Domain Inconsistencies

These are not blocking the persistence work above but should be addressed for consistency.

### 7a. Factory pattern — `Application` vs `Resource`

`Application` uses `required init` (fully immutable record; updates use `with` expression returning a new instance). `Resource` uses `private init` / `private set` (partially mutable in-place). Neither is wrong, but mixing patterns across the same bounded context makes the intent unclear.

**Decision needed:** Align `Application` to one pattern:
- **Option A:** Keep `required init` on `Application` — it has no in-place mutation today, so `with` is correct. Document that `required init` = immutable aggregate, `private set` = aggregate with guarded in-place mutation.
- **Option B:** Migrate `Application` to `private init` + `private set` + factory, matching `Resource` exactly.

Recommend **Option A** — no code change required, just document the convention.

### 7b. `IApplicationRepository` missing org-scoped query

`IResourceRepository` has `GetByEnvironmentAsync`. `IApplicationRepository` has no domain-specific query. Add:

```csharp
Task<IReadOnlyList<Application>> GetByOrganisationAsync(
    OrganisationId organisationId,
    CancellationToken cancellationToken = default);
```

And implement in `ApplicationRepository` using `_dbContext.Applications.AsNoTracking().Where(a => a.OrganisationId == organisationId).ToListAsync()`.

---

## 8. Pre-existing Gap — `Service`

`Service` has a domain entity and a `ServiceConfiguration` EF file but no `DbSet` in `OrchitectDbContext`, no repository interface, and no repository implementation. This was not introduced by the refactor. Scope into a separate ticket.

---

## Implementation Order

1. `OrchitectDbContext` — add two `DbSet` properties
2. `ResourceInstanceConfiguration` — EF config (decisions on `InputParameters` and `Output` mapping)
3. `ResourceDependencyGraphConfiguration` — EF config (decide Phase 1 JSONB vs Phase 2 edges table)
4. Three repository implementations
5. DI registrations in `OrchitectPersistenceExtensions`
6. `dotnet ef migrations add` — one migration covering all new tables
7. (Optional, non-blocking) `IApplicationRepository.GetByOrganisationAsync` + implementation
