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

## Why ResourceRequirement, ResourceBinding, and IResourceResolver

Phase 1 has the platform team creating `Resource` objects directly and wiring instances by hand. That works when one team controls everything top-down. The real world is different — app teams express what they need via `score.yaml`, and the platform decides how to satisfy it.

**Three concrete problems these models solve:**

### 1. Shared resources with an audit trail

Both `order-service` and `payment-service` need `azure.service-bus`. Without the requirement layer, whoever creates the `Resource` object wins — the second app silently shares it with no record of why. With `ResourceRequirement` + `ResourceBinding`, both teams independently declare their need. The resolver binds both to `ecommerce-servicebus-prod`, and the binding table records: "payment-service needed service-bus → resolved to ecommerce-servicebus-prod". You can now answer: which apps depend on this resource? What happens if I remove it?

### 2. Intent vs. allocation live at different lifecycles

`Resource` is what the platform *owns*. `ResourceRequirement` is what an app *asks for*. They belong to different actors and have different lifecycles. If `notification-service` is decommissioned, its requirements are deleted — but `ecommerce-servicebus-prod` remains because `order-service` and `payment-service` still depend on it. If you had app intent baked into `Resource` ownership directly, decommissioning an app that happens to own a shared resource would cascade incorrectly.

### 3. Delta computation needs a declared desired state

`DeploymentDelta` is computed by diffing the current `EnvironmentStateSnapshot` (what exists now) against the new desired set (what the requirements say should exist). Without requirements as a first-class concept, there is nothing to diff against — you'd compare raw `Resource` lists with no knowledge of *why* each resource exists or whether a resource that disappeared was intentionally removed or is a bug.

**In one sentence:** `ResourceRequirement` is the app's voice. `ResourceBinding` is the platform's answer. `IResourceResolver` is the decision logic. Together they answer: given what this app says it needs, which concrete resource satisfies it, and is that the same resource another app is already using?

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

## Playground Examples

These examples extend the Phase 1 ecommerce scenario directly. At the point Phase 2 starts, you have:
- 9 declared `Resource` objects (VNet, subnets, Key Vault, ACR, AKS, CosmosDB-orders, Service Bus, Redis)
- 5 `ResourceInstance` objects provisioned and `Active`
- A `ResourceDependencyGraph` with all 9 nodes and correct edges
- 4 applications: `order-service`, `payment-service`, `notification-service`, `product-catalog`

Phase 2 adds the intent layer on top of this.

---

### Step 8 — Parse score.yaml and Create ResourceRequirements

**The score.yaml is the API.** App teams write a score.yaml at the root of their repository. The platform clones the repo at the deployed commit, parses the file via `IScoreDriver`, and creates `ResourceRequirement` objects from each entry under `resources:`. The platform never expects app teams to interact with domain objects directly.

Two score files for the ecommerce scenario live in `src/Orchitect.Playground/`:

**`payment-service.score.yaml`** — references three shared/pre-existing resources by `id:`:
```yaml
apiVersion: score.dev/v1b1
metadata:
  name: payment-service
resources:
  service-bus:
    type: azure.service-bus
    class: direct
    id: ecommerce-servicebus-prod      # slug-match to existing resource
    parameters:
      topic: payment-processed
      sas_policy: listen,send

  keyvault:
    type: azure.key-vault
    class: indirect
    id: ecommerce-keyvault-prod
    parameters:
      secret_names: stripe-api-key,stripe-webhook-secret,cosmos-orders-connstr

  aks:
    type: azure.aks
    class: direct
    id: ecommerce-aks-prod
    parameters:
      namespace: payment-service
      network_policy: deny-all
      allowed_namespaces: order-service
```

**`product-catalog.score.yaml`** — two resources have no `id:`, so the resolver will provision new ones:
```yaml
apiVersion: score.dev/v1b1
metadata:
  name: product-catalog
resources:
  cosmos-products:
    type: azure.cosmosdb
    class: direct                      # no id: → resolver creates new resource
    parameters:
      consistency_level: BoundedStaleness
      max_throughput: "8000"
      public_network_access_enabled: "false"

  redis-catalog:
    type: azure.redis-cache
    class: direct                      # no id: → resolver creates new resource
    parameters:
      sku: Standard
      capacity: "2"
      redis_version: "7.2"

  aks:
    type: azure.aks
    class: direct
    id: ecommerce-aks-prod             # shared — slug-match to existing
    parameters:
      namespace: product-catalog
      min_replicas: "1"
      max_replicas: "10"
```

The playground reads these files directly. In production `IScoreDriver.ParseAsync(deployment, application, ct)` clones the repo at `deployment.CommitId` and returns the same `ScoreFile` object.

```csharp
// In production this call clones the app repo at the deployed commit:
//   var scoreFile = await scoreDriver.ParseAsync(deployment, application, ct);
// In the playground we read the local file directly.

static async Task<ScoreFile> LoadScore(string path)
{
    var yaml = await File.ReadAllTextAsync(path);
    return new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build()
        .Deserialize<ScoreFile>(yaml);
}

// ── Map ScoreFile → ResourceRequirements ──────────────────────────────────

// ScoreResource.Id becomes Constraints["id"] so the resolver can slug-match.
// ScoreResource.Parameters is Dictionary<string,string>; we convert to JsonElement
// because ResourceRequirement.Parameters supports full Terraform variable parity.
static List<ResourceRequirement> BuildRequirements(
    ScoreFile scoreFile,
    ApplicationId applicationId,
    OrganisationId organisationId,
    EnvironmentId environmentId)
{
    var requirements = new List<ResourceRequirement>();

    foreach (var (resourceKey, scoreResource) in scoreFile.Resources ?? [])
    {
        var constraints = new Dictionary<string, string>();
        if (scoreResource.Id is not null)
            constraints["id"] = scoreResource.Id;   // slug hint for the resolver

        var parameters = scoreResource.Parameters?
            .ToDictionary(
                kvp => kvp.Key,
                kvp => JsonSerializer.SerializeToElement(kvp.Value))
            ?? new Dictionary<string, JsonElement>();

        requirements.Add(ResourceRequirement.Create(new CreateResourceRequirementRequest(
            OrganisationId: organisationId,
            ApplicationId:  applicationId,
            EnvironmentId:  environmentId,
            Type:  scoreResource.Type,
            Class: scoreResource.Class ?? "direct",
            Constraints: constraints,
            Parameters:  parameters)));
    }

    return requirements;
}

// Parse and build requirements for both apps.
var paymentScoreFile  = await LoadScore("payment-service.score.yaml");
var catalogScoreFile  = await LoadScore("product-catalog.score.yaml");

var paymentRequirements = BuildRequirements(paymentScoreFile,  paymentService.Id,  organisation.Id, production.Id);
var catalogRequirements = BuildRequirements(catalogScoreFile,  catalogService.Id,  organisation.Id, production.Id);

// Give each requirement a name we can reference in Step 9.
var paymentServiceBusReq = paymentRequirements.Single(r => r.Type == "azure.service-bus");
var paymentKeyVaultReq   = paymentRequirements.Single(r => r.Type == "azure.key-vault");
var paymentAksReq        = paymentRequirements.Single(r => r.Type == "azure.aks");

var catalogCosmosReq = catalogRequirements.Single(r => r.Type == "azure.cosmosdb");
var catalogRedisReq  = catalogRequirements.Single(r => r.Type == "azure.redis-cache");
var catalogAksReq    = catalogRequirements.Single(r => r.Type == "azure.aks");

Console.WriteLine("=== Requirements parsed from score.yaml ===");
Console.WriteLine($"  payment-service ({paymentRequirements.Count} requirements):");
foreach (var req in paymentRequirements)
{
    var idHint = req.Constraints.TryGetValue("id", out var slug) ? $" → id: {slug}" : " → (new resource)";
    Console.WriteLine($"    [{req.Class}] {req.Type}{idHint}");
}
Console.WriteLine($"  product-catalog ({catalogRequirements.Count} requirements):");
foreach (var req in catalogRequirements)
{
    var idHint = req.Constraints.TryGetValue("id", out var slug) ? $" → id: {slug}" : " → (new resource)";
    Console.WriteLine($"    [{req.Class}] {req.Type}{idHint}");
}

// Expected output:
//   payment-service (3 requirements):
//     [direct]   azure.service-bus   → id: ecommerce-servicebus-prod
//     [indirect] azure.key-vault     → id: ecommerce-keyvault-prod
//     [direct]   azure.aks           → id: ecommerce-aks-prod
//   product-catalog (3 requirements):
//     [direct]   azure.cosmosdb      → (new resource)
//     [direct]   azure.redis-cache   → (new resource)
//     [direct]   azure.aks           → id: ecommerce-aks-prod
```

The presence or absence of `id:` in the score.yaml is the signal the resolver uses: if `id` is in `Constraints`, slug-match an existing resource; if absent, check ownership by type and provision a new one if needed.

Note that `order-service` would produce a `service-bus` requirement with `id: ecommerce-servicebus-prod` — the same slug as `payment-service`'s. Both apps write `id: ecommerce-servicebus-prod` in their own score.yaml, both produce a `ResourceRequirement`, and the resolver binds both to the same `ecommerce-servicebus-prod` Resource. That double-binding is the record that payment-service and order-service are both consumers of this shared resource.

---

### Step 8 (expanded) — What the Requirements Contain

The loop above produces requirements equivalent to these (shown here to make the field values explicit):

```csharp
// ── order-service requirements ──────────────────────────────────────────────

// CosmosDB is dedicated to order-service — no id: in score.yaml, so the
// resolver will create a new resource if one doesn't already belong to this app.
var orderCosmosReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: orderService.Id,
    EnvironmentId: production.Id,
    Type: "azure.cosmosdb.orders",
    Class: "direct",
    Constraints: new Dictionary<string, string>
    {
        ["region"]     = "eastus",
        ["compliance"] = "pci"
    },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["consistency_level"] = JsonSerializer.SerializeToElement("Session"),
        ["max_throughput"]    = JsonSerializer.SerializeToElement(4000),
        ["public_network_access_enabled"] = JsonSerializer.SerializeToElement(false)
    }));

// Service Bus is shared — id: "ecommerce-servicebus-prod" in score.yaml.
// The resolver will slug-match this to the existing ecommerce-servicebus-prod resource.
var orderServiceBusReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: orderService.Id,
    EnvironmentId: production.Id,
    Type: "azure.service-bus",
    Class: "direct",
    Constraints: new Dictionary<string, string>
    {
        ["id"]     = "ecommerce-servicebus-prod",   // maps to Resource.Slug
        ["region"] = "eastus"
    },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["topic"]      = JsonSerializer.SerializeToElement("order-placed"),
        ["sas_policy"] = JsonSerializer.SerializeToElement("send")
    }));

var orderAksReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: orderService.Id,
    EnvironmentId: production.Id,
    Type: "azure.aks",
    Class: "direct",
    Constraints: new Dictionary<string, string> { ["id"] = "ecommerce-aks-prod" },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["namespace"]       = JsonSerializer.SerializeToElement("order-service"),
        ["service_account"] = JsonSerializer.SerializeToElement("order-service-sa")
    }));

// ── payment-service requirements ────────────────────────────────────────────

// Payment-service ALSO needs service-bus — same slug constraint.
// Two separate requirements from two separate apps, same target resource.
var paymentServiceBusReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: paymentService.Id,
    EnvironmentId: production.Id,
    Type: "azure.service-bus",
    Class: "direct",
    Constraints: new Dictionary<string, string>
    {
        ["id"]         = "ecommerce-servicebus-prod",
        ["region"]     = "eastus",
        ["compliance"] = "pci"
    },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["subscriptions"] = JsonSerializer.SerializeToElement(new[]
        {
            new { topic = "order-placed", subscription = "payment-service-sub" }
        }),
        ["topic"]      = JsonSerializer.SerializeToElement("payment-processed"),
        ["sas_policy"] = JsonSerializer.SerializeToElement("listen,send")
    }));

var paymentKeyVaultReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: paymentService.Id,
    EnvironmentId: production.Id,
    Type: "azure.key-vault",
    Class: "indirect",
    Constraints: new Dictionary<string, string> { ["id"] = "ecommerce-keyvault-prod" },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["secret_names"] = JsonSerializer.SerializeToElement(new[]
        {
            "stripe-api-key", "stripe-webhook-secret", "cosmos-orders-connstr"
        })
    }));

var paymentAksReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: paymentService.Id,
    EnvironmentId: production.Id,
    Type: "azure.aks",
    Class: "direct",
    Constraints: new Dictionary<string, string>
    {
        ["id"]         = "ecommerce-aks-prod",
        ["compliance"] = "pci"
    },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["namespace"]       = JsonSerializer.SerializeToElement("payment-service"),
        ["service_account"] = JsonSerializer.SerializeToElement("payment-service-sa"),
        ["network_policy"]  = JsonSerializer.SerializeToElement("deny-all"),
        ["allowed_namespaces"] = JsonSerializer.SerializeToElement(new[] { "order-service" })
    }));

// ── notification-service requirements ───────────────────────────────────────

var notifServiceBusReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: notifService.Id,
    EnvironmentId: production.Id,
    Type: "azure.service-bus",
    Class: "direct",
    Constraints: new Dictionary<string, string> { ["id"] = "ecommerce-servicebus-prod" },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["subscriptions"] = JsonSerializer.SerializeToElement(new[]
        {
            new { topic = "payment-processed", subscription = "notification-payment-sub", max_delivery_count = 10 },
            new { topic = "order-placed",      subscription = "notification-order-sub",   max_delivery_count = 5  }
        }),
        ["sas_policy"] = JsonSerializer.SerializeToElement("listen")
    }));

var notifKeyVaultReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: notifService.Id,
    EnvironmentId: production.Id,
    Type: "azure.key-vault",
    Class: "indirect",
    Constraints: new Dictionary<string, string> { ["id"] = "ecommerce-keyvault-prod" },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["secret_names"] = JsonSerializer.SerializeToElement(new[]
        {
            "sendgrid-api-key", "twilio-account-sid", "twilio-auth-token"
        })
    }));

// ── product-catalog requirements ─────────────────────────────────────────────

// No id: — catalog needs its OWN CosmosDB account, separate from order-service's.
// The resolver will find no existing resource matching this type for this app,
// so a new Resource will be created: ecommerce-cosmos-products.
var catalogCosmosReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: catalogService.Id,
    EnvironmentId: production.Id,
    Type: "azure.cosmosdb.orders",   // same template type, separate account
    Class: "direct",
    Constraints: new Dictionary<string, string>
    {
        ["region"]              = "eastus",
        ["data-classification"] = "internal"
    },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["consistency_level"] = JsonSerializer.SerializeToElement("BoundedStaleness"),
        ["max_throughput"]    = JsonSerializer.SerializeToElement(8000),
        ["public_network_access_enabled"] = JsonSerializer.SerializeToElement(false)
    }));

// Redis is dedicated to product-catalog — no id:, resolver creates new resource.
var catalogRedisReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: catalogService.Id,
    EnvironmentId: production.Id,
    Type: "azure.redis-cache",
    Class: "direct",
    Parameters: new Dictionary<string, JsonElement>
    {
        ["sku"]      = JsonSerializer.SerializeToElement("Standard"),
        ["capacity"] = JsonSerializer.SerializeToElement(2),
        ["enable_non_ssl_port"] = JsonSerializer.SerializeToElement(false),
        ["redis_version"]       = JsonSerializer.SerializeToElement("7.2")
    }));

var catalogAksReq = ResourceRequirement.Create(new CreateResourceRequirementRequest(
    OrganisationId: organisation.Id,
    ApplicationId: catalogService.Id,
    EnvironmentId: production.Id,
    Type: "azure.aks",
    Class: "direct",
    Constraints: new Dictionary<string, string> { ["id"] = "ecommerce-aks-prod" },
    Parameters: new Dictionary<string, JsonElement>
    {
        ["namespace"]       = JsonSerializer.SerializeToElement("product-catalog"),
        ["min_replicas"]    = JsonSerializer.SerializeToElement(1),
        ["max_replicas"]    = JsonSerializer.SerializeToElement(10)
    }));

Console.WriteLine("=== Requirements declared ===");
Console.WriteLine($"  order-service:        3 requirements (cosmos, service-bus, aks)");
Console.WriteLine($"  payment-service:      3 requirements (service-bus, key-vault, aks)");
Console.WriteLine($"  notification-service: 3 requirements (service-bus, key-vault, aks)");
Console.WriteLine($"  product-catalog:      3 requirements (cosmos-NEW, redis, aks)");
Console.WriteLine($"  service-bus demanded by 3 apps — resolver must converge all to same resource");
```

---

### Step 9 — Resolve Requirements to Resources

`IResourceResolver` is a domain interface only — the playground simulates resolution inline using slug-matching, which is the simplest strategy a real resolver would use. Requirements with a `Constraints["id"]` are slug-matched to existing resources. Requirements without `id` are matched by type within the app's ownership (no match → new resource needed).

```csharp
// Simulate the resolver: slug-match or type-match against known resources.
// A real IResourceResolver implementation lives in Infrastructure, not here.
var allResources = new[]
{
    vnetResource, aksSubnetResource, dataSubnetResource, keyVaultResource,
    acrResource, aksResource, cosmosOrdersResource, serviceBusResource, redisCacheResource
};

ResourceId ResolveBySlug(string slug) =>
    allResources.Single(r => r.Slug == slug).Id;

// ── Bind order-service ───────────────────────────────────────────────────────

// CosmosDB: no slug constraint — resolver finds ecommerce-cosmos-orders
// already declared for order-service (ApplicationId matches). Bind to it.
var orderCosmosBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  orderCosmosReq.Id,
    resourceId:     cosmosOrdersResource.Id);   // existing resource — no delta entry needed

// Service Bus: slug match "ecommerce-servicebus-prod" → serviceBusResource.
var orderServiceBusBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  orderServiceBusReq.Id,
    resourceId:     ResolveBySlug("ecommerce-servicebus-prod"));

// AKS: slug match.
var orderAksBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  orderAksReq.Id,
    resourceId:     ResolveBySlug("ecommerce-aks-prod"));

// ── Bind payment-service ─────────────────────────────────────────────────────

// Service Bus: SAME slug → SAME resource. Two requirements, one resource, two bindings.
// This is the audit trail: both payment-service and order-service are recorded
// as having required and been given ecommerce-servicebus-prod.
var paymentServiceBusBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  paymentServiceBusReq.Id,
    resourceId:     ResolveBySlug("ecommerce-servicebus-prod")); // same resource as order's

var paymentKeyVaultBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  paymentKeyVaultReq.Id,
    resourceId:     ResolveBySlug("ecommerce-keyvault-prod"));

var paymentAksBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  paymentAksReq.Id,
    resourceId:     ResolveBySlug("ecommerce-aks-prod"));

// ── Bind notification-service ────────────────────────────────────────────────

var notifServiceBusBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  notifServiceBusReq.Id,
    resourceId:     ResolveBySlug("ecommerce-servicebus-prod")); // third app on same resource

var notifKeyVaultBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  notifKeyVaultReq.Id,
    resourceId:     ResolveBySlug("ecommerce-keyvault-prod"));

// ── Bind product-catalog ─────────────────────────────────────────────────────

// CosmosDB: no slug, no existing resource owned by catalog-service for type
// azure.cosmosdb.orders → resolver signals: provision new resource.
var cosmosProductsResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:     organisation.Id,
    Name:               "ecommerce-cosmos-products",
    Description:        "CosmosDB for product catalog documents. 8000 RU/s autoscale. BoundedStaleness.",
    ResourceTemplateId: cosmosOrdersTemplate.Id,   // same template, new instance
    EnvironmentId:      production.Id,
    Kind:               ResourceTemplateKind.Direct,
    ApplicationId:      catalogService.Id));

var catalogCosmosBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  catalogCosmosReq.Id,
    resourceId:     cosmosProductsResource.Id);   // NEW resource — will appear in delta

// Redis: no slug, no existing redis for catalog → new resource (already exists as redisCacheResource).
// The resolver finds redisCacheResource was created in Phase 1 for catalogService.Id.
var catalogRedisBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  catalogRedisReq.Id,
    resourceId:     redisCacheResource.Id);

var catalogAksBinding = ResourceBinding.Create(
    organisationId: organisation.Id,
    environmentId:  production.Id,
    requirementId:  catalogAksReq.Id,
    resourceId:     ResolveBySlug("ecommerce-aks-prod"));

Console.WriteLine("\n=== Resolution summary ===");
Console.WriteLine($"  service-bus demanded by 3 apps → all bound to ecommerce-servicebus-prod");
Console.WriteLine($"    order-service    binding: {orderServiceBusBinding.Id.Value}");
Console.WriteLine($"    payment-service  binding: {paymentServiceBusBinding.Id.Value}");
Console.WriteLine($"    notif-service    binding: {notifServiceBusBinding.Id.Value}");
Console.WriteLine($"  product-catalog cosmos: no existing resource → new resource created");
Console.WriteLine($"    new resource: {cosmosProductsResource.Slug} ({cosmosProductsResource.Id.Value})");
```

---

### Step 10 — Compute DeploymentDelta

The delta is computed by comparing the current `EnvironmentStateSnapshot` (the 9 resources provisioned in Phase 1) against the resolved binding set. The only new resource the bindings require that the snapshot doesn't contain is `ecommerce-cosmos-products`.

```csharp
// Phase 1 snapshot: 9 known active resources.
var phase1ResourceIds = new[]
{
    vnetResource.Id, aksSubnetResource.Id, dataSubnetResource.Id,
    keyVaultResource.Id, acrResource.Id, aksResource.Id,
    cosmosOrdersResource.Id, serviceBusResource.Id, redisCacheResource.Id
};

// New desired set: all bound resources. Diff against snapshot.
var desiredResourceIds = new HashSet<ResourceId>
{
    cosmosOrdersResource.Id, serviceBusResource.Id, aksResource.Id,  // order-service
    keyVaultResource.Id,                                              // payment + notif
    redisCacheResource.Id,                                           // catalog redis (existing)
    cosmosProductsResource.Id                                        // catalog cosmos (NEW)
};

var delta = DeploymentDelta.Create(organisation.Id, production.Id);

foreach (var id in desiredResourceIds)
{
    if (!phase1ResourceIds.Contains(id))
        delta.AddResource(id);   // net-new resource — needs provisioning
}
// No updates or removals in this scenario — all existing resources are unchanged.

Console.WriteLine("\n=== Deployment Delta ===");
Console.WriteLine($"  + Add: {cosmosProductsResource.Slug} (product-catalog CosmosDB)");
Console.WriteLine($"  ~ Update: (none)");
Console.WriteLine($"  - Remove: (none)");
Console.WriteLine($"  Total: {delta.AddedResourceIds.Count} added, {delta.UpdatedResourceIds.Count} updated, {delta.RemovedResourceIds.Count} removed");
```

---

### Step 11 — Enriched Deployment Lifecycle

The deployment tracks who requested it, when each phase started, and why it failed (if it does). The guarded `Transition` prevents invalid state jumps — the same discipline as `ResourceInstance`.

```csharp
// product-catalog deploy — adding its new cosmos resource.
var catalogDeploy = Deployment.Create(new CreateDeploymentRequest(
    ApplicationId: catalogService.Id,
    EnvironmentId: production.Id,
    CommitId:      new CommitId("3e8a91f"),
    RequestedBy:   "james@acme.com"));    // could be "system" for automated deploys

Console.WriteLine($"\n=== Deployment: {catalogDeploy.Id.Value} ===");
Console.WriteLine($"  Status:      {catalogDeploy.Status}");        // Pending
Console.WriteLine($"  Requested by: {catalogDeploy.RequestedBy}");

// Platform begins computing the delta.
catalogDeploy.Transition(DeploymentStatus.Planning);
Console.WriteLine($"  → {catalogDeploy.Status}");   // Planning

// Delta computed and persisted — attach it to the deployment.
catalogDeploy.SetDelta(delta.Id);
Console.WriteLine($"  DeltaId attached: {catalogDeploy.DeltaId}");

// IaC apply begins. StartedAt is set automatically on transition to Running.
catalogDeploy.Transition(DeploymentStatus.Running);
Console.WriteLine($"  → {catalogDeploy.Status}");
Console.WriteLine($"  StartedAt: {catalogDeploy.StartedAt}");

// Terraform apply completes — cosmos-products provisioned successfully.
catalogDeploy.Transition(DeploymentStatus.Succeeded);
Console.WriteLine($"  → {catalogDeploy.Status}");
Console.WriteLine($"  CompletedAt: {catalogDeploy.CompletedAt}");


// ── Failure and retry scenario ───────────────────────────────────────────────
// notification-service deploys independently; it hits a quota issue on Service Bus.

var notifDeploy = Deployment.Create(new CreateDeploymentRequest(
    ApplicationId: notifService.Id,
    EnvironmentId: production.Id,
    CommitId:      new CommitId("c2d44b0"),
    RequestedBy:   "system"));

notifDeploy.Transition(DeploymentStatus.Planning);
notifDeploy.SetDelta(DeploymentDelta.Create(organisation.Id, production.Id).Id);  // empty delta — no new resources
notifDeploy.Transition(DeploymentStatus.Running);

// Terraform apply fails: Service Bus topic quota exceeded.
notifDeploy.Transition(
    DeploymentStatus.Failed,
    errorSummary: "Terraform apply failed: azure.service-bus topic quota (10/10) exceeded in eastus");

Console.WriteLine($"\n=== notification-service deploy failed ===");
Console.WriteLine($"  Status:  {notifDeploy.Status}");
Console.WriteLine($"  Error:   {notifDeploy.ErrorSummary}");
Console.WriteLine($"  Completed: {notifDeploy.CompletedAt}");

// Operator raises quota via Azure portal. Deployment retries from Pending.
// Failed → Pending is the only valid retry path (no direct Failed → Running shortcut).
notifDeploy.Transition(DeploymentStatus.Pending);
notifDeploy.Transition(DeploymentStatus.Planning);
notifDeploy.Transition(DeploymentStatus.Running);
notifDeploy.Transition(DeploymentStatus.Succeeded);

Console.WriteLine($"  → Retried and {notifDeploy.Status}");
Console.WriteLine($"  Final CompletedAt: {notifDeploy.CompletedAt}");

// Invalid transition guard — cannot go from Succeeded back to Running.
try
{
    notifDeploy.Transition(DeploymentStatus.Running);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"\n  Guard enforced: {ex.Message}");
    // "Cannot transition from Succeeded to Running."
}
```

---

### Step 12 — Capture EnvironmentStateSnapshot

After successful deployment, record the full set of active resources as the new baseline. The next delta computation will diff against this snapshot.

```csharp
// Provision the new cosmos-products instance (matches the Phase 1 pattern).
var cosmosProductsVersion = cosmosOrdersTemplate.GetLatestVersion()!;
var cosmosProductsInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        cosmosProductsResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-cosmos-products-instance",
    TemplateVersionId: cosmosProductsVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters:   new Dictionary<string, JsonElement>
    {
        ["consistency_level"] = JsonSerializer.SerializeToElement("BoundedStaleness"),
        ["max_throughput"]    = JsonSerializer.SerializeToElement(8000),
        ["private_endpoint_subnet_id"] = JsonSerializer.SerializeToElement("$(ref:ecommerce-subnet-data.subnet_id)")
    }));

cosmosProductsInstance.Transition(ResourceInstanceStatus.Provisioning);
cosmosProductsInstance.Transition(ResourceInstanceStatus.Active, new ResourceInstanceOutput
{
    Location  = new Uri("https://portal.azure.com/#resource/.../ecommerce-cosmos-products"),
    Workspace = "ecommerce-prod"
});

// Snapshot captures all 10 active resources — this becomes the new baseline.
var activeResourceIds = new[]
{
    vnetResource.Id, aksSubnetResource.Id, dataSubnetResource.Id,
    keyVaultResource.Id, acrResource.Id, aksResource.Id,
    cosmosOrdersResource.Id, serviceBusResource.Id, redisCacheResource.Id,
    cosmosProductsResource.Id   // newly added in this deployment
};

var snapshot = EnvironmentStateSnapshot.Capture(
    organisationId:   organisation.Id,
    environmentId:    production.Id,
    deploymentId:     catalogDeploy.Id,
    activeResourceIds: activeResourceIds);

Console.WriteLine($"\n=== Environment State Snapshot ===");
Console.WriteLine($"  SnapshotId:  {snapshot.Id.Value}");
Console.WriteLine($"  Environment: production");
Console.WriteLine($"  DeploymentId: {snapshot.DeploymentId.Value}");
Console.WriteLine($"  CapturedAt:   {snapshot.CapturedAt:u}");
Console.WriteLine($"  Resources ({snapshot.ResourceIds.Count}):");
foreach (var id in snapshot.ResourceIds)
    Console.WriteLine($"    {id.Value}");
```

---

### Step 13 — Full Provision Summary

```csharp
Console.WriteLine("\n╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║              Phase 2 Provision Summary                  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");

Console.WriteLine("\n── Requirements ──────────────────────────────────────────");
var allRequirements = new[]
{
    (orderService.Name,   orderCosmosReq.Type),
    (orderService.Name,   orderServiceBusReq.Type),
    (orderService.Name,   orderAksReq.Type),
    (paymentService.Name, paymentServiceBusReq.Type),
    (paymentService.Name, paymentKeyVaultReq.Type),
    (paymentService.Name, paymentAksReq.Type),
    (notifService.Name,   notifServiceBusReq.Type),
    (notifService.Name,   notifKeyVaultReq.Type),
    (catalogService.Name, catalogCosmosReq.Type),
    (catalogService.Name, catalogRedisReq.Type),
    (catalogService.Name, catalogAksReq.Type)
};
foreach (var (app, type) in allRequirements)
    Console.WriteLine($"  {app,-28} → {type}");

Console.WriteLine("\n── Bindings (requirement → resource slug) ────────────────");
var allBindings = new[]
{
    (orderServiceBusBinding,   "azure.service-bus",        "ecommerce-servicebus-prod"),
    (paymentServiceBusBinding, "azure.service-bus",        "ecommerce-servicebus-prod"),  // SAME resource
    (notifServiceBusBinding,   "azure.service-bus",        "ecommerce-servicebus-prod"),  // SAME resource
    (paymentKeyVaultBinding,   "azure.key-vault",          "ecommerce-keyvault-prod"),
    (notifKeyVaultBinding,     "azure.key-vault",          "ecommerce-keyvault-prod"),
    (catalogCosmosBinding,     "azure.cosmosdb.orders",    "ecommerce-cosmos-products"),  // NEW
    (catalogRedisBinding,      "azure.redis-cache",        "ecommerce-redis-catalog")
};
foreach (var (binding, type, slug) in allBindings)
    Console.WriteLine($"  {binding.Id.Value} │ {type,-28} → {slug}");

Console.WriteLine("\n── Delta ─────────────────────────────────────────────────");
Console.WriteLine($"  + ecommerce-cosmos-products (product-catalog CosmosDB, 8000 RU/s)");
Console.WriteLine($"  (no updates, no removals)");

Console.WriteLine("\n── Deployment Timeline ───────────────────────────────────");
Console.WriteLine($"  {catalogDeploy.Id.Value}");
Console.WriteLine($"  Requested by: {catalogDeploy.RequestedBy}");
Console.WriteLine($"  Started:   {catalogDeploy.StartedAt:u}");
Console.WriteLine($"  Completed: {catalogDeploy.CompletedAt:u}");
Console.WriteLine($"  Status:    {catalogDeploy.Status}");

Console.WriteLine("\n── Snapshot ──────────────────────────────────────────────");
Console.WriteLine($"  {snapshot.ResourceIds.Count} active resources at {snapshot.CapturedAt:u}");
Console.WriteLine($"  Next delta will diff against this snapshot.");
```

---

## Example Real Flow

```
product-catalog deploy
score.yaml
  ↓
ResourceRequirements created (type, class, constraints)
  ↓
Resolver binds:
  azure.cosmosdb.orders (no id:) → NEW ecommerce-cosmos-products  [delta: + Add]
  azure.redis-cache     (no id:) → ecommerce-redis-catalog         [existing, no delta]
  azure.aks             (id: ecommerce-aks-prod) → ecommerce-aks-prod [existing, no delta]
  ↓
DeploymentDelta computed:
  + Add ecommerce-cosmos-products
  ↓
Deployment executes in graph order (cosmos after data subnet)
  ↓
EnvironmentStateSnapshot stored (10 resources)
  ↓
Next deploy diffs against this snapshot
```

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
