# Domain Models

## Resource

Represents a provisioned resource instance (e.g., CosmosDB, VNet, Helm release, Kubernetes Pod).

```csharp
using Orchitect.Domain.Environment;
using Orchitect.Domain.ResourceTemplate;

namespace Orchitect.Domain.Resource;

public sealed record Resource
{
    public required ResourceId Id { get; init; }
    public required string Name { get; init; }

    // The blueprint this resource is based on
    public required ResourceTemplateId ResourceTemplateId { get; init; }

    // The scope in which this resource lives (app, env, or global)
    public required ResourceScope Scope { get; init; }

    // Lifecycle metadata
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    // Flexible "instance details" (runtime values, identifiers, outputs)
    public IDictionary<string, object>? Properties { get; init; }

    // Current state of the resource
    public ResourceState State { get; init; } = ResourceState.Pending;
}

public enum ResourceState
{
    Pending,
    Provisioning,
    Active,
    Updating,
    Failed,
    PendingDeletion,
    Deleted
}

public sealed record ResourceScope
{
    public ApplicationId? ApplicationId { get; init; }
    public required EnvironmentId EnvironmentId { get; init; }
}

```

# Resource Workflows in the Orchestrator

## 1. Create New Service
- **Trigger**: First commit with a new `score.yaml`.
- **Steps**:
    - Parse desired resources.
    - Create `Resource` entries (`State = Pending`).
    - Insert into dependency DAG.
    - Provision in correct order.
    - Update each `Resource` with state and properties.

---

## 2. Add a New Resource
- **Trigger**: Existing service, new resource added to `score.yaml`.
- **Steps**:
    - Detect missing resource vs. current state.
    - Create new `Resource`.
    - Add to DAG and resolve dependencies.
    - Provision only the delta.

---

## 3. Remove a Resource
- **Trigger**: Resource removed from `score.yaml`.
- **Steps**:
    - Detect resource present in state but absent in desired config.
    - Mark `Resource` as `PendingDeletion`.
    - Delete safely in DAG order (dependents first).
    - Mark as `Deleted`.

---

## 4. Modify an Existing Resource
- **Trigger**: Resource still exists, but configuration changed.
- **Steps**:
    - Diff current vs. desired properties.
    - If immutable change (e.g., region): re-provision (delete + create).
    - If mutable change (e.g., SKU, replicas): update in place.
    - Update `Resource.UpdatedAt` and record version history.

---

## 5. Resource Rename / Alias
- **Trigger**: Developer renames a resource in `score.yaml`.
- **Steps**:
    - Detect rename vs. delete + create.
    - Without alias support → treat as delete + create.
    - With alias support → link to existing resource by `ResourceExternalId`.

---

## 6. Failed Provisioning
- **Trigger**: Provisioner (Terraform/Helm/etc.) reports failure.
- **Steps**:
    - Keep `Resource` in `Failed` state.
    - Allow retry of provisioning.
    - Block dependents until resolved.

---

## 7. Reconciliation (Drift Detection)
- **Trigger**: Actual cloud state != orchestrator state.
- **Steps**:
    - Compare stored `Properties` with live state.
    - If drift detected → repair or alert.
    - Ensure orchestrator remains source of truth.

---

## 8. Shared Resource Reuse
- **Trigger**: Multiple apps request same shared resource (e.g., VNet).
- **Steps**:
    - Detect existing environment/global resource.
    - Attach new dependency instead of creating duplicate.
    - Track references to prevent premature deletion.

---

## 9. Environment Teardown
- **Trigger**: Environment deleted or cleanup requested.
- **Steps**:
    - Delete all resources in DAG order.
    - Mark resources as `Deleted`.
    - Preserve shared/global resources unless no dependents remain.

# Resource State Machine

```mermaid
stateDiagram-v2
    [*] --> Pending

    Pending --> Provisioning: Start provisioning
    Provisioning --> Active: Success
    Provisioning --> Failed: Error

    Failed --> Provisioning: Retry
    Failed --> Deleted: Clean up

    Active --> Updating: Config change
    Updating --> Active: Success
    Updating --> Failed: Error

    Active --> PendingDeletion: Mark for removal
    PendingDeletion --> Deleted: Remove in DAG order

    Deleted --> [*]
