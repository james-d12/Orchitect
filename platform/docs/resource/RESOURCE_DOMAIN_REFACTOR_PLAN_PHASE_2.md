---
title: "Resource domain – Phase 2"
status: active
workstream: resource
milestone: "Resource Domain"
issues: [141, 142, 143, 144, 146, 155, 187]
superseded_by: null
last_reviewed: 2026-10-08
---

# Phase 2: Resource Domain — Requirements, Resolution, Deployments & Deltas

## Context

Phase 1 delivered a solid core: `Resource` as declared desired state, `ResourceInstance` with guarded lifecycle transitions, and `ResourceDependencyGraph` keyed on `ResourceId`. Phase 2 evolves this into a real platform orchestrator by adding the layer above the resource model — intent capture, resolution, change planning, and execution tracking.

**The goal:** an application expresses what it needs → the platform resolves it to a concrete resource → a delta is computed → a deployment executes it in graph order → the resource instances record the result.

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

---

## Why ResourceRequirement, ResourceBinding, and IResourceResolver

Phase 1 has the platform team creating `Resource` objects directly and wiring instances by hand. That works when one team controls everything top-down. The real world is different — app teams express what they need via `score.yaml`, and the platform decides how to satisfy it.

**Three concrete problems these models solve:**

### 1. Shared resources with an audit trail

Both `order-service` and `payment-service` need `azure.service-bus`. Without the requirement layer, whoever creates the `Resource` object wins — the second app silently shares it with no record of why. With `ResourceRequirement` + `ResourceBinding`, both teams independently declare their need. The resolver binds both to `ecommerce-servicebus-prod`, and the binding table records: "payment-service needed service-bus → resolved to ecommerce-servicebus-prod". You can now answer: which apps depend on this resource? What happens if I remove it?

### 2. Intent vs. allocation live at different lifecycles

`Resource` is what the platform *owns*. `ResourceRequirement` is what an app *asks for*. They belong to different actors and have different lifecycles. If `notification-service` is decommissioned, its requirements are deleted — but `ecommerce-servicebus-prod` remains because `order-service` and `payment-service` still depend on it. If you had app intent baked into `Resource` ownership directly, decommissioning an app that happens to own a shared resource would cascade incorrectly.

### 3. Delta computation needs a declared desired state

`DeploymentDelta` is computed by diffing the application's current resource instances (what exists now) against the new desired set (what the requirements say should exist). Without requirements as a first-class concept, there is nothing to diff against — you'd compare raw `Resource` lists with no knowledge of *why* each resource exists or whether a resource that disappeared was intentionally removed or is a bug.

**In one sentence:** `ResourceRequirement` is the app's voice. `ResourceBinding` is the platform's answer. `IResourceResolver` is the decision logic. Together they answer: given what this app says it needs, which concrete resource satisfies it, and is that the same resource another app is already using?

---

## Pre-existing State to Reconcile

### Existing `Deployment` (evolve, do not replace)

`src/Orchitect.Domain/Engine/Deployment/Deployment.cs` already exists with:
- Fields: `ApplicationId`, `EnvironmentId`, `CommitId`, `Status (Pending/Deployed/Failed/RolledBack)`
- EF config at `src/Orchitect.Persistence/Configurations/Engine/DeploymentConfiguration.cs`
- Used in `Orchitect.Playground/Program.cs`

Phase 2 enriches this aggregate — add `string RequestedBy`, `DateTime? StartedAt`, `DateTime? CompletedAt`, `string? ErrorSummary`, and a guarded `Transition` method. Expand `DeploymentStatus` to: `Pending, Planning, Running, Succeeded, Failed, Cancelled` (replacing `Deployed` / `RolledBack`).

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

Represents what a run will change, worked out before execution. It belongs to a `DeploymentRun`, not to the `Deployment`: a deployment can have several runs (retries, destroy), and each run is planned on its own by `IRunPlanner`.

The delta is one change per resource instance the run touches:

| Change | Meaning |
|---|---|
| `Added` | No current instance: the resource or its instance is new |
| `Updated` | The current instance's template version or input parameters differ from the resolved input |
| `Unchanged` | The current instance matches the resolved input |
| `Removed` | The application consumed the resource before, and the score file no longer has it |

**Baseline (current state):** the resources in the environment whose `Consumers` include the application (later its `ResourceBinding`s, #143), each with its latest instance that isn't `Removed`. The baseline is scoped to the application, not the environment. Other applications' resources are never part of the delta.

Example delta:
```
+ ecommerce-cosmos-products   (Added)
~ ecommerce-redis-catalog     (Updated: max_memory 1gb → 2gb)
- ecommerce-legacy-cache      (Removed)
```

### 5. EnvironmentStateSnapshot (dropped)

Dropped in #145: "Not needed, as we store resource instances." `ResourceInstance` status already records what exists in an environment, so the delta's baseline comes from the instances (see §4).

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
    DeploymentDelta.cs          (new — computed delta)
    ResourceChange.cs           (new)
    PlannedResourceInstance.cs  (existing — gains ResourceId and Change)
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

> `DeltaId` and `SetDelta()` are dropped: the delta lives on the run plan (Step 4). The rest of this step is being rewritten to fit the current `Deploying`/`Destroying` lifecycle (#141).

**Updated `Deployment.cs` (key additions):**
```csharp
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
```

**Update `CreateDeploymentRequest`** to include `string RequestedBy`.

**Persistence:** Update `DeploymentConfiguration.cs` to map the new nullable columns. Update the status enum string conversion to handle renamed values.

---

### Step 4 — DeploymentDelta

The delta is computed by `RunPlanner` for each run and stored on that run's `DeploymentRunPlan`, in the same transaction as the plan. A repeat or concurrent `PlanAsync` returns the stored plan, and the stored delta with it. There is no separate aggregate, repository or table.

**`ResourceChange.cs`:**
```csharp
public enum ResourceChange
{
    Added,
    Updated,
    Unchanged,
    Removed
}
```

**`PlannedResourceInstance.cs`** gains the resource and the change. `Key` is the score key; a removed resource has no key, because it is no longer in the score file:
```csharp
public sealed record PlannedResourceInstance(
    ResourceInstanceId InstanceId,
    ResourceId ResourceId,
    string? Key,
    ResourceChange Change);
```

**`DeploymentDelta.cs`** — a pure computation, so it can be unit tested without the planner:
```csharp
public sealed record CurrentResourceInstance(
    ResourceInstanceId InstanceId,
    ResourceId ResourceId,
    ResourceTemplateVersionId TemplateVersionId,
    IReadOnlyDictionary<string, JsonElement> InputParameters);

public sealed record DesiredResource(
    string Key,
    ResourceId ResourceId,
    ResourceTemplateVersionId TemplateVersionId,
    IReadOnlyDictionary<string, JsonElement> InputParameters);

public static class DeploymentDelta
{
    /// <summary>
    /// Compares an application's current instances with the resources its score file resolves to. Each resource
    /// appears once: Added, Updated or Unchanged when it is desired, Removed when only the current state has it.
    /// </summary>
    public static IReadOnlyList<(ResourceId ResourceId, string? Key, ResourceChange Change)> Compute(
        IReadOnlyCollection<CurrentResourceInstance> current,
        IReadOnlyCollection<DesiredResource> desired);
}
```

**Rules:**
- **Baseline:** the environment's resources whose `Consumers` include the application, each with its latest instance that isn't `Removed`. It is scoped to the application, so other applications' resources never appear.
- **Compute first.** `GetOrCreateInstanceAsync` calls `Reconfigure` on the current instance, so the delta must be computed before `RecordResourcesAsync` changes anything.
- **Updated:** the template version differs, or the input parameters differ (compared by key and JSON value).
- **First deploy:** no baseline, so every resource is `Added`.
- **Retry:** an instance that is `Failed` or still `Provisioning` counts as `Updated` when its inputs changed, and as `Unchanged` otherwise. Its status is not part of the comparison; `BeginProvisioning` already handles it.
- **Removed:** the planner records the removed instances in the plan, but #144 does not change them. The removed instances' transitions (`PendingRemoval` → `Removing` → `Removed`), the graph, the consumers and the shared-resource rule are #187. Until then, `RunCompleter` skips them, because they are not `Provisioning` or `Removing`.
- **Destroy runs:** every planned instance is `Removed`.

**Persistence:** extend `DeploymentRunPlanConfiguration`'s stored instance (`StoredInstance`) with `ResourceId` and `Change`. Plans stored before this change deserialize with `Change = Added` for provision runs and `Removed` for destroy runs, so no data migration is needed.

**Exposure:** return the delta with the latest run on `GET /deployments/{id}`, as lists of added, updated and removed resource ids and slugs.

**Tests:**
- Unit tests for `DeploymentDelta.Compute` covering: first deploy, unchanged, version change, parameter change, removed, and a resource shared with another application that isn't touched.
- `RunPlannerTests` checking that the delta is computed before `Reconfigure` and that a replayed plan returns the stored delta.
- An integration test: deploy, then redeploy with one resource changed and one dropped, and assert the persisted delta.

---

### Step 5 — EnvironmentStateSnapshot (dropped)

Dropped in #145. The delta's baseline comes from `ResourceInstance` (Step 4).

---

### Step 6 — Playground update

Extend `Orchitect.Playground/Program.cs` to demonstrate the full Phase 2 flow after the existing Phase 1 demo:

1. Create `ResourceRequirement` for each resource (type, class, constraints, parameters)
2. Create `ResourceBinding` for each (requirement → resource direct mapping)
3. Compute the `DeploymentDelta` against the Phase 1 instances
4. Create enriched `Deployment` with `RequestedBy = "system"`
5. Run the deployment lifecycle (the exact transitions depend on #141)
6. Print a summary of the full provision flow

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
DeploymentDelta computed against payment-api's current instances:
  + Added    topic payment-refunds
  ~ Updated  payment-db-prod (sku changed)
  = Unchanged kv-prod
  ↓
Deployment executes in graph order
  ↓
Resource instances record the result
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
| `Engine/Deployment/Deployment.cs` | **Modify** — add `RequestedBy`, `StartedAt`, `CompletedAt`, `ErrorSummary`, `Transition()` (being reworked in #141) |
| `Engine/Deployment/DeploymentStatus.cs` | **Modify** — replace with `Pending/Planning/Running/Succeeded/Failed/Cancelled` |
| `Engine/Deployment/CreateDeploymentRequest.cs` | **Modify** — add `RequestedBy` parameter |
| `Engine/Deployment/DeploymentDelta.cs` | **Create** — `Compute` plus `CurrentResourceInstance`, `DesiredResource` |
| `Engine/Deployment/ResourceChange.cs` | **Create** |
| `Engine/Deployment/PlannedResourceInstance.cs` | **Modify** — add `ResourceId`, `Change`; `Key` nullable |
| `Engine.Dispatch/Plan/RunPlanner.cs` | **Modify** — compute the delta before recording, store it on the plan |
| `Persistence/Configurations/Engine/ResourceRequirementConfiguration.cs` | **Create** |
| `Persistence/Configurations/Engine/ResourceBindingConfiguration.cs` | **Create** |
| `Persistence/Configurations/Engine/DeploymentConfiguration.cs` | **Modify** — add new nullable columns |
| `Persistence/Configurations/Engine/DeploymentRunPlanConfiguration.cs` | **Modify** — store `ResourceId` and `Change` per planned instance |
| `Persistence/OrchitectDbContext.cs` | **Modify** — add 2 new `DbSet`s (requirements, bindings) |
| `Orchitect.Playground/Program.cs` | **Modify** — extend with Phase 2 demo flow |

---

## Design Decisions

- **`Constraints` uses `Dictionary<string, string>`** — constraints are label-style tags (region=uk, compliance=pci) that don't need complex values. `Parameters` uses `Dictionary<string, JsonElement>` for full Terraform variable parity.
- **`RequestedBy` is `string`** — avoids coupling `Deployment` to a specific identity type. Accepts a user email or `"system"`.
- **`DeploymentDelta` is a pure computation, not an aggregate** — `Compute(current, desired)` returns one change per resource, so the change kinds can't overlap and it is unit tested without a database. The result is stored on the run plan.
- **The delta belongs to a run, not a deployment** — each run (first attempt, retry, destroy) is planned separately and can see different current state.
- **No `EnvironmentStateSnapshot`** — `ResourceInstance` already records what exists, so the baseline is read from the instances (#145).
- **`IResourceResolver` is a domain interface only** — the actual resolution strategy (slug match, policy evaluator, etc.) lives in Infrastructure. Phase 2 only defines the contract.

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
2. **Don't let Deployment own resources directly** — each run's plan lists the instances it touches and how (the delta).
3. **Don't hardcode resolver logic in controllers/services** — `IResourceResolver` is a domain strategy; its implementation belongs in Infrastructure.

---

## Must Have (Phase 2)

- `ResourceRequirement`
- `ResourceBinding`
- `IResourceResolver` (interface only)
- `Deployment` enriched
- `DeploymentDelta`

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

The delta compares product-catalog's current instances with what its bindings resolve to. The baseline is scoped to product-catalog: the resources whose `Consumers` include it, with their latest instance that isn't `Removed`. After Phase 1, that is only AKS (`aksInstance`). The Redis resource was declared in Phase 1 but never provisioned, so it has no instance and counts as added. Other applications' resources (Service Bus, Key Vault, CosmosDB orders) are not part of this delta.

```csharp
// Baseline: product-catalog's consumed resources with a live instance.
var current = new[]
{
    new CurrentResourceInstance(aksInstance.Id, aksResource.Id,
        aksInstance.TemplateVersionId, aksInstance.InputParameters)
};

// Desired: what product-catalog's bindings resolve to, with the inputs the planner would use.
var desired = new[]
{
    new DesiredResource("aks", aksResource.Id,
        aksInstance.TemplateVersionId, aksInstance.InputParameters),            // same inputs → Unchanged
    new DesiredResource("cache", redisCacheResource.Id,
        redisCacheTemplate.GetLatestVersion()!.Id, new Dictionary<string, JsonElement>()), // no instance → Added
    new DesiredResource("db", cosmosProductsResource.Id,
        cosmosOrdersTemplate.GetLatestVersion()!.Id, new Dictionary<string, JsonElement>()) // new resource → Added
};

var delta = DeploymentDelta.Compute(current, desired);

Console.WriteLine("\n=== Deployment Delta (product-catalog) ===");
foreach (var (resourceId, key, change) in delta)
    Console.WriteLine($"  {change,-9} {key ?? "(dropped)"} {resourceId.Value}");
// Added     cache ...
// Added     db    ...
// Unchanged aks   ...
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

// The run's plan stores the delta computed in Step 10.

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

### Step 12 — Record the Result on the Instances

After a successful deployment, the instances become the next run's baseline. There is no separate snapshot (#145). The new cosmos-products instance goes to `Active`, and the Redis instance follows the same steps.

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

Console.WriteLine($"\n=== cosmos-products instance: {cosmosProductsInstance.Status} ===");
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
foreach (var (resourceId, key, change) in delta)
    Console.WriteLine($"  {change,-9} {key ?? "(dropped)"} {resourceId.Value}");

Console.WriteLine("\n── Deployment Timeline ───────────────────────────────────");
Console.WriteLine($"  {catalogDeploy.Id.Value}");
Console.WriteLine($"  Requested by: {catalogDeploy.RequestedBy}");
Console.WriteLine($"  Started:   {catalogDeploy.StartedAt:u}");
Console.WriteLine($"  Completed: {catalogDeploy.CompletedAt:u}");
Console.WriteLine($"  Status:    {catalogDeploy.Status}");

```

---

## Example Real Flow (playground)

```
product-catalog deploy
score.yaml
  ↓
ResourceRequirements created (type, class, constraints)
  ↓
Resolver binds:
  azure.cosmosdb.orders (no id:) → NEW ecommerce-cosmos-products
  azure.redis-cache     (no id:) → ecommerce-redis-catalog (declared, never provisioned)
  azure.aks             (id: ecommerce-aks-prod) → ecommerce-aks-prod
  ↓
DeploymentDelta computed against product-catalog's instances:
  + Added     ecommerce-cosmos-products
  + Added     ecommerce-redis-catalog
  = Unchanged ecommerce-aks-prod
  ↓
Deployment executes in graph order (cosmos after data subnet)
  ↓
Instances move to Active
  ↓
Next deploy diffs against product-catalog's instances
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
./scripts/efm.sh Phase2_ResourceRequirements_Bindings_Deltas
```
