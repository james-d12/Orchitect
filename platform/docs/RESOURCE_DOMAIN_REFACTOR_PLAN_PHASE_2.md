# Phase 2: Resource Domain — Requirements, Resolution, Deployments & Deltas

## Context

Phase 1 delivered a solid core: `Resource` as declared desired state, `ResourceInstance` with guarded lifecycle transitions, and `ResourceDependencyGraph` keyed on `ResourceId`. Phase 2 evolves this into a real platform orchestrator by adding the layer above the resource model — intent capture, resolution, change planning, and execution tracking.

**The goal:** an application expresses what it needs → the platform resolves it to a concrete resource → a delta is computed → a deployment executes it in graph order → a snapshot records the result.

---

## Core Shift in Thinking

### Phase 1 modelled:
- Resources
- Instances
- Dependencies

### Phase 2 adds:
- **Intent** (ResourceRequirement)
- **Resolution** (ResourceBinding, IResourceResolver)
- **Change Planning** (DeploymentDelta)
- **Deployment Execution** (Deployment — enriched)
- **State History** (EnvironmentStateSnapshot)

---

## Pre-existing State to Reconcile

### Existing `Deployment` (evolve, do not replace)

`src/Orchitect.Domain/Engine/Deployment/Deployment.cs` already exists with:
- Fields: `ApplicationId`, `EnvironmentId`, `CommitId`, `Status (Pending/Deployed/Failed/RolledBack)`
- EF config at `src/Orchitect.Persistence/Configurations/Engine/DeploymentConfiguration.cs`
- Used in `Orchitect.Playground/Program.cs`

Phase 2 enriches this aggregate — add `DeploymentDeltaId?`, `string RequestedBy`, `DateTime? StartedAt`, `DateTime? CompletedAt`, `string? ErrorSummary`, and a guarded `Transition` method. Expand `DeploymentStatus` to: `Pending, Planning, Running, Succeeded, Failed, Cancelled` (replacing `Deployed` / `RolledBack`).

### Existing `Requirement` stub (replace entirely)

`src/Orchitect.Domain/Engine/Requirement/` contains `Requirement.cs`, `RequirementResource.cs`, `RequirementResult.cs` — thin value objects with no ID, no `OrganisationId`, and no aggregate pattern. Replace entirely with a proper `ResourceRequirement` aggregate. Rename the folder to `ResourceRequirement/`.

---

## New Domain Areas

### 1. ResourceRequirement

Represents what an application needs, not how it is implemented.

Example:
```
postgres
shared
uk
pci
```

Fields:

| Field | Purpose |
|---|---|
| `Id` | identity |
| `OrganisationId` | tenant scope |
| `ApplicationId` | owner |
| `EnvironmentId` | deployment scope |
| `Type` | `azure.cosmosdb.orders` / `azure.redis-cache` etc. |
| `Class` | `direct` / `indirect` / `implicit` |
| `Constraints` | `Dictionary<string, string>` — label-style tags (region, compliance, tier) |
| `Parameters` | `Dictionary<string, JsonElement>` — optional Terraform/Helm config values |

### 2. ResourceBinding & IResourceResolver

`ResourceBinding` maps Requirement → Resource. `IResourceResolver` is a domain strategy interface — the implementation lives in Infrastructure (not in scope for Phase 2 domain work).

Fields:

| Field | Purpose |
|---|---|
| `Id` | identity |
| `OrganisationId` | tenant scope |
| `EnvironmentId` | scope |
| `ResourceRequirementId` | what was asked |
| `ResourceId` | what was chosen |

### 3. Deployment (enriched)

A first-class record of change execution. Existing `Deployment` gains:

| Added Field | Purpose |
|---|---|
| `DeploymentDeltaId?` | planned change (nullable until `Planning`) |
| `RequestedBy` | user email or `"system"` |
| `StartedAt?` | timing |
| `CompletedAt?` | timing |
| `ErrorSummary?` | auditability |

New `DeploymentStatus` values:

| Status | Meaning |
|---|---|
| `Pending` | Created, not yet planning |
| `Planning` | Computing delta |
| `Running` | IaC apply in progress |
| `Succeeded` | All resources active |
| `Failed` | One or more resources failed |
| `Cancelled` | Stopped before completion |

Valid transitions:
```
Pending   → Planning
Planning  → Running | Failed | Cancelled
Running   → Succeeded | Failed | Cancelled
Failed    → Pending   (retry)
Succeeded → []
Cancelled → []
```

### 4. DeploymentDelta

Represents what will change before execution.

Fields:

| Field | Purpose |
|---|---|
| `Id` | identity |
| `OrganisationId` | tenant scope |
| `EnvironmentId` | target |
| `AddedResourceIds` | new resources |
| `UpdatedResourceIds` | changed resources |
| `RemovedResourceIds` | resources to tear down |

Example delta:
```
+ Add Kafka topic
~ Upgrade payment-api image
- Remove old cache
```

### 5. EnvironmentStateSnapshot

Stores current known environment state after a successful deployment. Used as the baseline for computing the next delta.

```
Snapshot A (current) vs Desired B = DeploymentDelta
```

Fields:

| Field | Purpose |
|---|---|
| `Id` | identity |
| `OrganisationId` | tenant scope |
| `EnvironmentId` | which environment |
| `DeploymentId` | which deployment produced this snapshot |
| `ResourceIds` | all active resources at capture time |
| `CapturedAt` | timestamp |

---

## Folder Structure

```
Engine/
  ResourceRequirement/
    ResourceRequirementId.cs
    ResourceRequirement.cs
    CreateResourceRequirementRequest.cs
    IResourceRequirementRepository.cs
  ResourceResolution/
    ResourceBindingId.cs
    ResourceBinding.cs
    IResourceResolver.cs
    IResourceBindingRepository.cs
  Deployment/           (existing — enriched)
    Deployment.cs
    DeploymentId.cs
    DeploymentStatus.cs
    CommitId.cs
    CreateDeploymentRequest.cs
    IDeploymentRepository.cs
  DeploymentDelta/
    DeploymentDeltaId.cs
    DeploymentDelta.cs
    IDeploymentDeltaRepository.cs
  EnvironmentState/
    EnvironmentStateSnapshotId.cs
    EnvironmentStateSnapshot.cs
    IEnvironmentStateSnapshotRepository.cs
```

---

## Implementation Steps

### Step 1 — ResourceRequirement

Delete the three existing stubs in `Engine/Requirement/` and create the proper aggregate in `Engine/ResourceRequirement/`.

**`ResourceRequirementId.cs`:**
```csharp
public readonly record struct ResourceRequirementId(Guid Value)
{
    public ResourceRequirementId() : this(Guid.NewGuid()) { }
}
```

**`ResourceRequirement.cs`:**
```csharp
public sealed record ResourceRequirement
{
    public ResourceRequirementId Id { get; private init; }
    public OrganisationId OrganisationId { get; private init; }
    public ApplicationId ApplicationId { get; private init; }
    public EnvironmentId EnvironmentId { get; private init; }
    public string Type { get; private init; } = string.Empty;
    public string Class { get; private init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Constraints { get; private init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; private init; } = new Dictionary<string, JsonElement>();
    public DateTime CreatedAt { get; private init; }

    private ResourceRequirement() { }

    public static ResourceRequirement Create(CreateResourceRequirementRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.Type);
        return new ResourceRequirement
        {
            Id = new ResourceRequirementId(),
            OrganisationId = request.OrganisationId,
            ApplicationId = request.ApplicationId,
            EnvironmentId = request.EnvironmentId,
            Type = request.Type,
            Class = request.Class,
            Constraints = request.Constraints ?? new Dictionary<string, string>(),
            Parameters = request.Parameters ?? new Dictionary<string, JsonElement>(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

**`CreateResourceRequirementRequest.cs`:**
```csharp
public sealed record CreateResourceRequirementRequest(
    OrganisationId OrganisationId,
    ApplicationId ApplicationId,
    EnvironmentId EnvironmentId,
    string Type,
    string Class,
    IReadOnlyDictionary<string, string>? Constraints = null,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null);
```

**`IResourceRequirementRepository.cs`:**
```csharp
public interface IResourceRequirementRepository : IRepository<ResourceRequirement, ResourceRequirementId>
{
    Task<ResourceRequirement?> UpdateAsync(ResourceRequirement requirement, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ResourceRequirementId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceRequirement>> GetByApplicationAsync(ApplicationId applicationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceRequirement>> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
}
```

**Persistence:** `ResourceRequirementConfiguration.cs` — map all scalar properties with ID conversions, store `Constraints` and `Parameters` as `jsonb`. Add `DbSet<ResourceRequirement> ResourceRequirements` to `OrchitectDbContext`.

---

### Step 2 — ResourceBinding & IResourceResolver

New folder `Engine/ResourceResolution/`. No existing code to reconcile.

**`ResourceBindingId.cs`** — standard strongly-typed GUID struct.

**`ResourceBinding.cs`:**
```csharp
public sealed record ResourceBinding
{
    public ResourceBindingId Id { get; private init; }
    public OrganisationId OrganisationId { get; private init; }
    public EnvironmentId EnvironmentId { get; private init; }
    public ResourceRequirementId ResourceRequirementId { get; private init; }
    public ResourceId ResourceId { get; private init; }
    public DateTime CreatedAt { get; private init; }

    private ResourceBinding() { }

    public static ResourceBinding Create(
        OrganisationId organisationId,
        EnvironmentId environmentId,
        ResourceRequirementId requirementId,
        ResourceId resourceId)
        => new()
        {
            Id = new ResourceBindingId(),
            OrganisationId = organisationId,
            EnvironmentId = environmentId,
            ResourceRequirementId = requirementId,
            ResourceId = resourceId,
            CreatedAt = DateTime.UtcNow
        };
}
```

**`IResourceResolver.cs`** (domain strategy interface — implementation deferred to Infrastructure):
```csharp
public interface IResourceResolver
{
    Task<ResourceBinding> ResolveAsync(
        ResourceRequirement requirement,
        IReadOnlyList<Resource> existingResources,
        CancellationToken cancellationToken = default);
}
```

**`IResourceBindingRepository.cs`:**
```csharp
public interface IResourceBindingRepository : IRepository<ResourceBinding, ResourceBindingId>
{
    Task<ResourceBinding?> GetByRequirementAsync(ResourceRequirementId requirementId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceBinding>> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ResourceBindingId id, CancellationToken cancellationToken = default);
}
```

**Persistence:** `ResourceBindingConfiguration.cs`. Add `DbSet<ResourceBinding> ResourceBindings` to `OrchitectDbContext`.

---

### Step 3 — Deployment evolution

Enrich the existing aggregate. Keep `ApplicationId`, `EnvironmentId`, `CommitId`. Add the fields listed above. Add a private constructor and guard `Transition`:

**Updated `DeploymentStatus.cs`:**
```csharp
public enum DeploymentStatus
{
    Pending,     // Created, not yet planning
    Planning,    // Computing delta
    Running,     // IaC apply in progress
    Succeeded,   // All resources active
    Failed,      // One or more resources failed
    Cancelled    // Stopped before completion
}
```

**Updated `Deployment.cs` (key additions):**
```csharp
public DeploymentDeltaId? DeltaId { get; private set; }
public string RequestedBy { get; private init; } = string.Empty;
public DateTime? StartedAt { get; private set; }
public DateTime? CompletedAt { get; private set; }
public string? ErrorSummary { get; private set; }

private static readonly Dictionary<DeploymentStatus, HashSet<DeploymentStatus>> ValidTransitions = new()
{
    [DeploymentStatus.Pending]   = [DeploymentStatus.Planning],
    [DeploymentStatus.Planning]  = [DeploymentStatus.Running, DeploymentStatus.Failed, DeploymentStatus.Cancelled],
    [DeploymentStatus.Running]   = [DeploymentStatus.Succeeded, DeploymentStatus.Failed, DeploymentStatus.Cancelled],
    [DeploymentStatus.Failed]    = [DeploymentStatus.Pending],
    [DeploymentStatus.Succeeded] = [],
    [DeploymentStatus.Cancelled] = []
};

public void Transition(DeploymentStatus newStatus, string? errorSummary = null)
{
    if (!ValidTransitions[Status].Contains(newStatus))
        throw new InvalidOperationException($"Cannot transition from {Status} to {newStatus}.");
    Status = newStatus;
    ErrorSummary = errorSummary;
    if (newStatus == DeploymentStatus.Running) StartedAt = DateTime.UtcNow;
    if (newStatus is DeploymentStatus.Succeeded or DeploymentStatus.Failed or DeploymentStatus.Cancelled)
        CompletedAt = DateTime.UtcNow;
    UpdatedAt = DateTime.UtcNow;
}

public void SetDelta(DeploymentDeltaId deltaId) => DeltaId = deltaId;
```

**Update `CreateDeploymentRequest`** to include `string RequestedBy`.

**Persistence:** Update `DeploymentConfiguration.cs` to map the new nullable columns. Update the status enum string conversion to handle renamed values.

---

### Step 4 — DeploymentDelta

New folder `Engine/DeploymentDelta/`.

**`DeploymentDeltaId.cs`** — standard strongly-typed GUID struct.

**`DeploymentDelta.cs`:**
```csharp
public sealed class DeploymentDelta
{
    public DeploymentDeltaId Id { get; private init; }
    public OrganisationId OrganisationId { get; private init; }
    public EnvironmentId EnvironmentId { get; private init; }
    public DateTime CreatedAt { get; private init; }

    private readonly List<ResourceId> _addedResourceIds = [];
    private readonly List<ResourceId> _updatedResourceIds = [];
    private readonly List<ResourceId> _removedResourceIds = [];

    public IReadOnlyList<ResourceId> AddedResourceIds => _addedResourceIds.AsReadOnly();
    public IReadOnlyList<ResourceId> UpdatedResourceIds => _updatedResourceIds.AsReadOnly();
    public IReadOnlyList<ResourceId> RemovedResourceIds => _removedResourceIds.AsReadOnly();

    private DeploymentDelta() { }

    public static DeploymentDelta Create(OrganisationId organisationId, EnvironmentId environmentId)
        => new()
        {
            Id = new DeploymentDeltaId(),
            OrganisationId = organisationId,
            EnvironmentId = environmentId,
            CreatedAt = DateTime.UtcNow
        };

    public void AddResource(ResourceId id)    => _addedResourceIds.Add(id);
    public void UpdateResource(ResourceId id) => _updatedResourceIds.Add(id);
    public void RemoveResource(ResourceId id) => _removedResourceIds.Add(id);
}
```

**`IDeploymentDeltaRepository.cs`:**
```csharp
public interface IDeploymentDeltaRepository : IRepository<DeploymentDelta, DeploymentDeltaId>
{
    Task<DeploymentDelta?> UpdateAsync(DeploymentDelta delta, CancellationToken cancellationToken = default);
}
```

**Persistence:** `DeploymentDeltaConfiguration.cs` — store the three `ResourceId` lists as `jsonb` arrays of `Guid`. Add `DbSet<DeploymentDelta> DeploymentDeltas` to `OrchitectDbContext`.

---

### Step 5 — EnvironmentStateSnapshot

New folder `Engine/EnvironmentState/`.

**`EnvironmentStateSnapshotId.cs`** — standard strongly-typed GUID struct.

**`EnvironmentStateSnapshot.cs`:**
```csharp
public sealed class EnvironmentStateSnapshot
{
    public EnvironmentStateSnapshotId Id { get; private init; }
    public OrganisationId OrganisationId { get; private init; }
    public EnvironmentId EnvironmentId { get; private init; }
    public DeploymentId DeploymentId { get; private init; }
    public DateTime CapturedAt { get; private init; }

    private readonly List<ResourceId> _resourceIds = [];
    public IReadOnlyList<ResourceId> ResourceIds => _resourceIds.AsReadOnly();

    private EnvironmentStateSnapshot() { }

    public static EnvironmentStateSnapshot Capture(
        OrganisationId organisationId,
        EnvironmentId environmentId,
        DeploymentId deploymentId,
        IEnumerable<ResourceId> activeResourceIds)
    {
        var snapshot = new EnvironmentStateSnapshot
        {
            Id = new EnvironmentStateSnapshotId(),
            OrganisationId = organisationId,
            EnvironmentId = environmentId,
            DeploymentId = deploymentId,
            CapturedAt = DateTime.UtcNow
        };
        snapshot._resourceIds.AddRange(activeResourceIds);
        return snapshot;
    }
}
```

**`IEnvironmentStateSnapshotRepository.cs`:**
```csharp
public interface IEnvironmentStateSnapshotRepository : IRepository<EnvironmentStateSnapshot, EnvironmentStateSnapshotId>
{
    Task<EnvironmentStateSnapshot?> GetLatestByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
}
```

**Persistence:** `EnvironmentStateSnapshotConfiguration.cs` — store `ResourceIds` as `jsonb`. Add `DbSet<EnvironmentStateSnapshot> EnvironmentStateSnapshots` to `OrchitectDbContext`.

---

### Step 6 — Playground update

Extend `Orchitect.Playground/Program.cs` to demonstrate the full Phase 2 flow after the existing Phase 1 demo:

1. Create `ResourceRequirement` for each resource (type, class, constraints, parameters)
2. Create `ResourceBinding` for each (requirement → resource direct mapping)
3. Create `DeploymentDelta` (mark all resources as added)
4. Create enriched `Deployment` with `RequestedBy = "system"`
5. Transition: `Pending → Planning`, call `SetDelta`, then `Planning → Running → Succeeded`
6. Capture `EnvironmentStateSnapshot`
7. Print a summary of the full provision flow

---

## Example Real Flow

```
payment-api deploy
score.yaml
  ↓
Needs postgres + kafka + vault
  ↓
ResourceRequirements created (type, class, constraints)
  ↓
Resolver binds:
  postgres → payment-db-prod
  kafka    → eventbus-prod
  vault    → kv-prod
  ↓
DeploymentDelta computed:
  + Upgrade image v5→v6
  + Add topic payment-refunds
  ↓
Deployment executes in graph order
  ↓
EnvironmentStateSnapshot stored
```

---

## File Summary

| File | Action |
|---|---|
| `Engine/Requirement/Requirement.cs` | **Delete** — replaced by ResourceRequirement |
| `Engine/Requirement/RequirementResource.cs` | **Delete** |
| `Engine/Requirement/RequirementResult.cs` | **Delete** |
| `Engine/ResourceRequirement/ResourceRequirementId.cs` | **Create** |
| `Engine/ResourceRequirement/ResourceRequirement.cs` | **Create** |
| `Engine/ResourceRequirement/CreateResourceRequirementRequest.cs` | **Create** |
| `Engine/ResourceRequirement/IResourceRequirementRepository.cs` | **Create** |
| `Engine/ResourceResolution/ResourceBindingId.cs` | **Create** |
| `Engine/ResourceResolution/ResourceBinding.cs` | **Create** |
| `Engine/ResourceResolution/IResourceResolver.cs` | **Create** |
| `Engine/ResourceResolution/IResourceBindingRepository.cs` | **Create** |
| `Engine/Deployment/Deployment.cs` | **Modify** — add `DeltaId?`, `RequestedBy`, `StartedAt`, `CompletedAt`, `ErrorSummary`, `Transition()`, `SetDelta()` |
| `Engine/Deployment/DeploymentStatus.cs` | **Modify** — replace with `Pending/Planning/Running/Succeeded/Failed/Cancelled` |
| `Engine/Deployment/CreateDeploymentRequest.cs` | **Modify** — add `RequestedBy` parameter |
| `Engine/DeploymentDelta/DeploymentDeltaId.cs` | **Create** |
| `Engine/DeploymentDelta/DeploymentDelta.cs` | **Create** |
| `Engine/DeploymentDelta/IDeploymentDeltaRepository.cs` | **Create** |
| `Engine/EnvironmentState/EnvironmentStateSnapshotId.cs` | **Create** |
| `Engine/EnvironmentState/EnvironmentStateSnapshot.cs` | **Create** |
| `Engine/EnvironmentState/IEnvironmentStateSnapshotRepository.cs` | **Create** |
| `Persistence/Configurations/Engine/ResourceRequirementConfiguration.cs` | **Create** |
| `Persistence/Configurations/Engine/ResourceBindingConfiguration.cs` | **Create** |
| `Persistence/Configurations/Engine/DeploymentConfiguration.cs` | **Modify** — add new nullable columns |
| `Persistence/Configurations/Engine/DeploymentDeltaConfiguration.cs` | **Create** |
| `Persistence/Configurations/Engine/EnvironmentStateSnapshotConfiguration.cs` | **Create** |
| `Persistence/OrchitectDbContext.cs` | **Modify** — add 4 new `DbSet`s |
| `Orchitect.Playground/Program.cs` | **Modify** — extend with Phase 2 demo flow |

---

## Design Decisions

- **`Constraints` uses `Dictionary<string, string>`** — constraints are label-style tags (region=uk, compliance=pci) that don't need complex values. `Parameters` uses `Dictionary<string, JsonElement>` for full Terraform variable parity.
- **`RequestedBy` is `string`** — avoids coupling `Deployment` to a specific identity type. Accepts a user email or `"system"`.
- **`DeploymentDelta` is `sealed class` not `record`** — mutable list fields (`Add*` mutators) are safer on a class. Matches the `ResourceDependencyGraph` precedent.
- **`EnvironmentStateSnapshot.ResourceIds` stored as `jsonb`** — avoids a join table for a read-only historical record. Querying individual IDs within snapshots is not a primary use case.
- **`IResourceResolver` is a domain interface only** — the actual resolution strategy (slug match, policy evaluator, etc.) lives in Infrastructure. Phase 2 only defines the contract.
- **`Deployment.DeltaId` is nullable** — a deployment is created at `Pending` before the delta is computed. `SetDelta()` is called once the `DeploymentDelta` is persisted and the deployment transitions to `Planning`.

---

## What Reuses from Phase 1

| Phase 1 Asset | Used in Phase 2 |
|---|---|
| `Resource` | concrete targets for bindings and deltas |
| `ResourceInstance` | runtime result, transitions drive deployment status |
| `ResourceDependencyGraph` | deploy ordering for delta execution |
| `ResourceInstanceStatus` | execution states feeding `Deployment.Status` |

---

## Risks to Avoid

1. **Don't merge requirements into Resource** — `Resource` is what the platform owns; `ResourceRequirement` is what an app asks for. Keep them separate.
2. **Don't let Deployment own resources directly** — `Deployment` references `DeploymentDeltaId` + `EnvironmentId`. Resources are in the delta.
3. **Don't hardcode resolver logic in controllers/services** — `IResourceResolver` is a domain strategy; its implementation belongs in Infrastructure.

---

## Must Have (Phase 2)

- `ResourceRequirement`
- `ResourceBinding`
- `IResourceResolver` (interface only)
- `Deployment` enriched
- `DeploymentDelta`
- `EnvironmentStateSnapshot`

## Nice Later (out of scope)

- Rollback
- Drift detection
- Cost optimisation
- Policy DSL
- Full resolver implementation

---

## Verification

```bash
dotnet build src/Orchitect.Domain          # domain compiles cleanly
dotnet build                               # full solution — catches all callsite breakages
dotnet test                                # no existing tests broken
dotnet run --project src/Orchitect.Playground  # playground runs end-to-end
```

After domain build passes, run the migration script:
```bash
./scripts/efm.sh Phase2_ResourceRequirements_Bindings_Deltas_Snapshots
```
