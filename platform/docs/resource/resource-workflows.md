---
title: "Resource lifecycle workflows"
status: reference
workstream: resource
milestone: "Resource Domain"
issues: [151, 152, 153, 154, 155, 156]
superseded_by: null
last_reviewed: 2026-10-08
---

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
- **Today**: Terraform keeps one root module per application and environment, so a provision whose score no longer
  has a resource destroys its `module.<template>_<key>` without warning. The run planner only looks at the
  resources in the score, so the removed resource's `Resource` and instance stay `Active` after the infrastructure
  is gone.
- **Target design (#153)**:
    - **Detect.** On a provision, the run planner finds the application's recorded resources that still have a live
      instance (anything but `Removed`) and whose slug no longer matches a resource in the score. These are the
      run's removals.
    - **Block by default.** If there are removals and the run wasn't queued with `AllowRemovals`, the plan fails with
      `RunPlanFailure.RemovalsNotConfirmed` and lists each resource it would destroy (slug and template type).
      Nothing is recorded and the runner never reaches `terraform apply`.
    - **Confirm per run.** `AllowRemovals` is set on the request that queues the provision (`POST /deployments`) and
      stored on `DeploymentRun`. It applies to that run only, not to the deployment or later runs.
    - **Clean up.** With `AllowRemovals`, the planner moves the removed instances to `Removing` in the same
      transaction that moves the score's instances to `Provisioning`, and the run's completion moves them to
      `Removed`. The application is dropped from each removed resource's consumers, and the resource is dropped
      from the dependency graph. The run records what it removed, so a confirmed removal stays visible.
    - **Shared resources.** A removed resource that other applications still consume is blocked even with
      `AllowRemovals`, because its module lives in this application's Terraform state and would still be destroyed
      (see #214).

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
- **Decision (#153)**: a rename is a removal of the old key plus an addition of the new one, handled as in §3. The
  removal is blocked until the run is queued with `AllowRemovals`, so a rename never destroys the old resource
  without someone confirming it. The score has no rename or alias metadata: a "one removed, one added of the same
  type" match is ambiguous, and keeping state across a rename (Terraform `moved` blocks) isn't worth a
  rename-specific field in `score.yaml`.

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
