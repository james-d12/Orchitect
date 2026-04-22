# Plan: Refactor Engine Domain Models for Platform Orchestrator

## Context

The Engine domain has four areas that partially model a platform orchestrator but have gaps that make them unusable in production:

- `Resource` is a thin struct with no factory method, no OrganisationId, and no repository interface.
- `ResourceInstance` has no lifecycle status enum — `ResourceInstanceState` is a sealed record holding `Location` and `Workspace`, not a state machine. There's no back-reference to `Resource`, no input parameters, and no OrganisationId.
- `ResourceDependency` is disconnected from the domain — it uses a `string Identifier` instead of a real `ResourceId`, making the graph an orphaned in-memory algorithm with no persistence story.
- `ResourceTemplateKind` is defined but never wired into `ResourceTemplate`.

The goal is to make these models accurately reflect: declare a Resource → create it from a ResourceTemplate → deploy it as a ResourceInstance to an Environment → track provisioning order and removal safety via a scoped dependency DAG.

---

## Changes

### 1. `Engine/Resource/Resource.cs` — Add factory method + OrganisationId + Description

Replace the bare record with a factory-method aggregate following the `Environment` pattern:

```csharp
public sealed record Resource
{
    public ResourceId Id { get; private init; }
    public OrganisationId OrganisationId { get; private init; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ResourceTemplateId ResourceTemplateId { get; private init; }
    public ApplicationId ApplicationId { get; private init; }
    public EnvironmentId EnvironmentId { get; private init; }
    public DateTime CreatedAt { get; private init; }
    public DateTime UpdatedAt { get; private set; }

    private Resource() { }

    public static Resource Create(CreateResourceRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.Name);
        return new Resource
        {
            Id = new ResourceId(),
            OrganisationId = request.OrganisationId,
            Name = request.Name,
            Description = request.Description,
            ResourceTemplateId = request.ResourceTemplateId,
            ApplicationId = request.ApplicationId,
            EnvironmentId = request.EnvironmentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
```

### 2. `Engine/Resource/CreateResourceRequest.cs` — New file

```csharp
public sealed record CreateResourceRequest(
    OrganisationId OrganisationId,
    string Name,
    string Description,
    ResourceTemplateId ResourceTemplateId,
    ApplicationId ApplicationId,
    EnvironmentId EnvironmentId);
```

### 3. `Engine/Resource/IResourceRepository.cs` — New file

```csharp
public interface IResourceRepository : IRepository<Resource, ResourceId>
{
    Task<Resource?> UpdateAsync(Resource resource, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ResourceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Resource>> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
}
```

---

### 4. `Engine/ResourceInstance/ResourceInstanceStatus.cs` — New file

```csharp
public enum ResourceInstanceStatus
{
    Pending,       // Requested, not yet started
    Provisioning,  // IaC apply in progress
    Active,        // Live and healthy
    Failed,        // Provisioning or update failed
    PendingRemoval,
    Removing,
    Removed
}
```

`Pending`/`Provisioning` are separate to prevent double-dispatch. `PendingRemoval`/`Removing`/`Removed` enable safe DAG teardown. No `Updating` — an update re-enters `Provisioning` (consistent with Terraform apply semantics).

### 5. `Engine/ResourceInstance/ResourceInstanceState.cs` — Rename type to `ResourceInstanceLocation`

File renamed to `ResourceInstanceLocation.cs`. Same two properties (`Uri Location`, `string? Workspace`), new name only. The name `State` was misleading — this record holds location metadata, not a lifecycle state.

### 6. `Engine/ResourceInstance/ResourceInstance.cs` — Full replacement

Key changes:
- Add `OrganisationId`, `ResourceId` (back-reference to declared Resource)
- Replace `ResourceInstanceState State` with `ResourceInstanceStatus Status` + `ResourceInstanceLocation? Location` (nullable — only populated on `Active`)
- Remove `string? ExistingResourceId` (replaced by `ResourceId`)
- Add `IReadOnlyDictionary<string, string> InputParameters` (Terraform/Helm variable values at provision time)
- Add static `Create` factory
- Add `Transition(ResourceInstanceStatus, ResourceInstanceLocation?)` — enforces that `Active` requires a location
- Keep `Consumers` / `AddConsumer` unchanged

```csharp
public static ResourceInstance Create(CreateResourceInstanceRequest request) { ... }

public void Transition(ResourceInstanceStatus newStatus, ResourceInstanceLocation? location = null)
{
    if (newStatus == ResourceInstanceStatus.Active)
        ArgumentNullException.ThrowIfNull(location);
    Status = newStatus;
    if (location is not null) Location = location;
    UpdatedAt = DateTime.UtcNow;
}
```

### 7. `Engine/ResourceInstance/CreateResourceInstanceRequest.cs` — New file

```csharp
public sealed record CreateResourceInstanceRequest(
    ResourceId ResourceId,
    OrganisationId OrganisationId,
    string Name,
    ResourceTemplateVersionId TemplateVersionId,
    EnvironmentId EnvironmentId,
    IReadOnlyDictionary<string, string>? InputParameters = null);
```

### 8. `Engine/ResourceInstance/IResourceInstanceRepository.cs` — New file

```csharp
public interface IResourceInstanceRepository : IRepository<ResourceInstance, ResourceInstanceId>
{
    Task<ResourceInstance?> UpdateAsync(ResourceInstance instance, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ResourceInstanceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceInstance>> GetByResourceAsync(ResourceId resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceInstance>> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
}
```

---

### 9. `Engine/ResourceDependency/ResourceDependency.cs` — Replace `string Identifier` with `ResourceId`

The `ResourceDependencyId` struct is unchanged. `ResourceDependency` gets a `ResourceId ResourceId` property in place of `string Identifier`:

```csharp
public sealed record ResourceDependency
{
    public ResourceDependencyId Id { get; init; }
    public ResourceId ResourceId { get; init; }

    public ResourceDependency(ResourceId resourceId)
    {
        Id = new ResourceDependencyId();
        ResourceId = resourceId;
    }

    private ResourceDependency() { }  // for persistence
}
```

### 10. `Engine/ResourceDependency/ResourceDependencyGraphId.cs` — New file

Standard strongly-typed ID struct (same pattern as all other ID types).

### 11. `Engine/ResourceDependency/ResourceDependencyGraph.cs` — Add aggregate identity + secondary index

The Kahn's sort and cycle-detection algorithm bodies are **not touched**. Structural additions only:

- Add `ResourceDependencyGraphId Id`, `OrganisationId OrganisationId`, `EnvironmentId EnvironmentId` (private init)
- Add `private readonly Dictionary<ResourceId, ResourceDependencyId> _resourceIndex` secondary index
- Add `private ResourceDependencyGraph() { }` + `static Create(OrganisationId, EnvironmentId)` factory
- Override `AddResource` to also insert into `_resourceIndex`
- Override `RemoveResource` to also remove from `_resourceIndex`

The dual-index approach (internal `_nodes` keyed on `ResourceDependencyId`, secondary `_resourceIndex` keyed on `ResourceId`) lets callers look up by `ResourceId` without touching the algorithm.

### 12. `Engine/ResourceDependency/IResourceDependencyGraph.cs` — Add aggregate properties + ResourceId lookups

Add to interface:
- `ResourceDependencyGraphId Id { get; }`
- `OrganisationId OrganisationId { get; }`
- `EnvironmentId EnvironmentId { get; }`
- `bool TryGetNodeId(ResourceId resourceId, out ResourceDependencyId nodeId)`
- `bool ContainsResource(ResourceId resourceId)`

All existing method signatures (`DependentCount`, `DependencyCount`, `AddResource`, `RemoveResource`, `AddDependency`, `RemoveDependency`, `HasDependencyPath`, `ResolveOrder`) are unchanged.

### 13. `Engine/ResourceDependency/IResourceDependencyGraphRepository.cs` — New file

```csharp
public interface IResourceDependencyGraphRepository
    : IRepository<ResourceDependencyGraph, ResourceDependencyGraphId>
{
    Task<ResourceDependencyGraph?> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
    Task<ResourceDependencyGraph?> UpdateAsync(ResourceDependencyGraph graph, CancellationToken cancellationToken = default);
}
```

### 14. `Engine/ResourceTemplate/ResourceTemplate.cs` — Wire in `Kind`

Add `ResourceTemplateKind Kind { get; private set; }` to the record body. Add `Kind` to `CreateResourceTemplateRequest` and `CreateResourceTemplateWithVersionRequest`, wire through `Create`/`CreateWithVersion` factories, and add `kind` parameter to the `Update` method.

**Why Kind matters:** `Direct` = orchestrator fully owns the lifecycle; `Indirect` = externally managed, read-only to the orchestrator; `Implicit` = auto-resolved dependency. These distinctions control which nodes in `ResolveOrder()` trigger an actual IaC apply.

---

## File Summary

| File | Action |
|---|---|
| `Engine/Resource/Resource.cs` | Modify — add `OrganisationId`, `Description`, private constructor, `Create` factory |
| `Engine/Resource/CreateResourceRequest.cs` | Create |
| `Engine/Resource/IResourceRepository.cs` | Create |
| `Engine/ResourceInstance/ResourceInstanceStatus.cs` | Create — 7-value lifecycle enum |
| `Engine/ResourceInstance/ResourceInstanceState.cs` | Rename to `ResourceInstanceLocation.cs`, rename type |
| `Engine/ResourceInstance/ResourceInstance.cs` | Modify — add `ResourceId`, `OrganisationId`, `Status`, `Location`, `InputParameters`; remove `State`/`ExistingResourceId`; add factory + `Transition` |
| `Engine/ResourceInstance/CreateResourceInstanceRequest.cs` | Create |
| `Engine/ResourceInstance/IResourceInstanceRepository.cs` | Create |
| `Engine/ResourceDependency/ResourceDependency.cs` | Modify — replace `string Identifier` with `ResourceId ResourceId` |
| `Engine/ResourceDependency/ResourceDependencyGraphId.cs` | Create |
| `Engine/ResourceDependency/ResourceDependencyGraph.cs` | Modify — add `Id`, `OrganisationId`, `EnvironmentId`, `_resourceIndex`, `Create` factory; override `AddResource`/`RemoveResource` |
| `Engine/ResourceDependency/IResourceDependencyGraph.cs` | Modify — add aggregate properties + `TryGetNodeId`, `ContainsResource` |
| `Engine/ResourceDependency/IResourceDependencyGraphRepository.cs` | Create |
| `Engine/ResourceDependency/ResourceDependencyNode.cs` | No change |
| `Engine/ResourceTemplate/ResourceTemplate.cs` | Modify — add `Kind` property, wire through factories and `Update` |
| `Engine/ResourceTemplate/CreateResourceTemplateRequest.cs` | Modify — add `Kind` field |
| `Engine/ResourceTemplate/CreateResourceTemplateWithVersionRequest.cs` | Modify — add `Kind` field |

---

## Known Breakages

`src/Orchitect.Playground/Program.cs` (playground only, no production impact):
- `new ResourceDependency(paymentApi.Name)` × 3 → `new ResourceDependency(resource.Id)`
- `new ResourceDependencyGraph()` → `ResourceDependencyGraph.Create(orgId, envId)`

---

## How the Pieces Connect

```
Organisation
  └── Environment
        ├── ResourceDependencyGraph  (scoped to Org + Env)
        │     └── nodes: ResourceDependency { ResourceId }
        │
        ├── Resource  (declared desired state)
        │     ResourceTemplateId → ResourceTemplate { Kind }
        │     ApplicationId, EnvironmentId, OrganisationId
        │
        └── ResourceInstance  (actual provisioned deployment)
              ResourceId → Resource
              Status (enum), Location? (only populated on Active)
              InputParameters, Consumers
```

**Provision flow:** Declare Resources → add to graph → `ResolveOrder()` gives provisioning sequence → for each node, `ResourceInstance.Create(...)` → `Transition(Provisioning)` → `Transition(Active, location)`.

**Removal safety:** `DependentCount(nodeId) == 0` → `Transition(PendingRemoval)` → teardown → `Transition(Removed)` → `RemoveResource(nodeId)`.

---

## Score YAML Integration

The `ScoreDriver` clones each application's repository at the deployed commit and looks for `score.yaml`. It deserialises into `ScoreFile` using camelCase field names. Each key under `resources:` becomes a `ScoreResource` with these fields:

| YAML field | C# property | Maps to domain |
|---|---|---|
| `type` | `ScoreResource.Type` | `ResourceTemplate.Type` — used to look up which template to provision |
| `class` | `ScoreResource.Class` | `ResourceTemplateKind` — `direct` (owned), `indirect` (pre-existing), `implicit` (auto-resolved) |
| `id` | `ScoreResource.Id` | `Resource.Name` — references a shared resource by its declared name; if absent, a new resource is created |
| `metadata.annotations` | `ScoreResourceMetadata.Annotations` | Free-form labels carried through to the `Resource.Description` |
| `parameters` | `ScoreResource.Parameters` | `ResourceInstance.InputParameters` — passed verbatim to Terraform/Helm |

---

### `order-service/score.yaml`

The order service owns CosmosDB (it is the only writer), shares the Service Bus namespace, and runs on the shared AKS cluster. It references the cluster and bus by `id` so the orchestrator reuses existing resources rather than provisioning duplicates.

```yaml
apiVersion: score.dev/v1b1
metadata:
  name: order-service

resources:

  # Dedicated CosmosDB account — order-service is the sole writer.
  # No id: field → orchestrator creates a new Resource if one does not exist.
  cosmos-orders:
    type: azure.cosmosdb.orders
    class: direct
    metadata:
      annotations:
        owner: platform-team
        pci-scope: "true"
    parameters:
      resource_group_name: rg-ecommerce-data-prod
      location: eastus
      consistency_level: Session
      max_throughput: "4000"
      public_network_access_enabled: "false"
      private_endpoint_subnet_id: "$(ref:ecommerce-subnet-data.subnet_id)"
      connection_string_key_vault_secret: "$(ref:ecommerce-keyvault-prod.vault_uri)/secrets/cosmos-orders-connstr"

  # Shared Service Bus namespace — order-service publishes order-placed events.
  # id: matches Resource.Name declared by the platform team; no new resource is created.
  service-bus:
    type: azure.service-bus
    class: direct
    id: ecommerce-servicebus-prod
    parameters:
      # order-service only needs a producer SAS policy on this topic.
      topic: order-placed
      sas_policy: send

  # Shared AKS cluster — runs all four microservices.
  aks:
    type: azure.aks
    class: direct
    id: ecommerce-aks-prod
    parameters:
      namespace: order-service
      service_account: order-service-sa
      # Workload Identity binding for this service's managed identity.
      workload_identity_client_id: "$(env:ORDER_SERVICE_CLIENT_ID)"
```

---

### `payment-service/score.yaml`

Payment service is PCI-DSS in scope. It only consumes Service Bus (never writes to CosmosDB directly — order-service is the source of truth). It reads payment-processor credentials from Key Vault via Workload Identity.

```yaml
apiVersion: score.dev/v1b1
metadata:
  name: payment-service

resources:

  # Shared Service Bus — payment-service subscribes to order-placed, publishes payment-processed.
  service-bus:
    type: azure.service-bus
    class: direct
    id: ecommerce-servicebus-prod
    parameters:
      subscriptions:
        - topic: order-placed
          subscription: payment-service-sub
      topic: payment-processed
      sas_policy: listen,send

  # Key Vault — payment gateway API key and webhook signing secret stored here.
  # Indirect: Key Vault already exists, provisioned as a platform resource.
  # order-service score.yaml does not manage this — the platform team owns it.
  keyvault:
    type: azure.key-vault
    class: indirect
    id: ecommerce-keyvault-prod
    parameters:
      # Secrets this service will read. Used by the provisioner to grant RBAC Get permission.
      secret_names:
        - stripe-api-key
        - stripe-webhook-secret
        - cosmos-orders-connstr   # read-only: payment-service reads order state

  # Shared AKS cluster.
  aks:
    type: azure.aks
    class: direct
    id: ecommerce-aks-prod
    parameters:
      namespace: payment-service
      service_account: payment-service-sa
      workload_identity_client_id: "$(env:PAYMENT_SERVICE_CLIENT_ID)"
      # Network policy: deny all ingress except from order-service namespace.
      network_policy: deny-all
      allowed_namespaces:
        - order-service
```

---

### `notification-service/score.yaml`

Notification service is stateless — no database, no cache. It consumes two topics and calls external SMTP and SMS APIs. Its only infrastructure dependencies are the bus and the cluster.

```yaml
apiVersion: score.dev/v1b1
metadata:
  name: notification-service

resources:

  # Subscribes to both payment-processed and order-placed for confirmation emails.
  service-bus:
    type: azure.service-bus
    class: direct
    id: ecommerce-servicebus-prod
    parameters:
      subscriptions:
        - topic: payment-processed
          subscription: notification-payment-sub
          max_delivery_count: "10"
          lock_duration: PT1M
        - topic: order-placed
          subscription: notification-order-sub
          max_delivery_count: "5"
          lock_duration: PT30S
      sas_policy: listen

  keyvault:
    type: azure.key-vault
    class: indirect
    id: ecommerce-keyvault-prod
    parameters:
      secret_names:
        - sendgrid-api-key
        - twilio-account-sid
        - twilio-auth-token

  aks:
    type: azure.aks
    class: direct
    id: ecommerce-aks-prod
    parameters:
      namespace: notification-service
      service_account: notification-service-sa
      workload_identity_client_id: "$(env:NOTIFICATION_SERVICE_CLIENT_ID)"
      # Scale to zero during off-hours — notification volume is low.
      min_replicas: "0"
      max_replicas: "5"
```

---

### `product-catalog/score.yaml`

Product catalog has the most complex read path: CosmosDB for the source of truth, Redis in front of it for sub-millisecond reads on hot SKUs. The Redis instance is dedicated to this service — no `id:` is specified, so the orchestrator creates a new `Resource`.

```yaml
apiVersion: score.dev/v1b1
metadata:
  name: product-catalog

resources:

  # CosmosDB for product documents. Dedicated account — higher throughput than orders.
  cosmos-products:
    type: azure.cosmosdb.orders    # reuses the same template type; orchestrator provisions a separate account
    class: direct
    metadata:
      annotations:
        owner: catalog-team
        data-classification: internal
    parameters:
      resource_group_name: rg-ecommerce-data-prod
      location: eastus
      consistency_level: BoundedStaleness   # higher consistency for inventory accuracy
      max_throughput: "8000"                # products reads are 4× higher than orders
      public_network_access_enabled: "false"
      private_endpoint_subnet_id: "$(ref:ecommerce-subnet-data.subnet_id)"
      connection_string_key_vault_secret: "$(ref:ecommerce-keyvault-prod.vault_uri)/secrets/cosmos-products-connstr"

  # Dedicated Redis cache for hot product reads.
  # No id: — orchestrator provisions a fresh instance scoped to this service.
  redis-catalog:
    type: azure.redis-cache
    class: direct
    parameters:
      resource_group_name: rg-ecommerce-data-prod
      location: eastus
      sku: Standard
      family: C
      capacity: "2"
      enable_non_ssl_port: "false"
      redis_version: "7.2"
      private_endpoint_subnet_id: "$(ref:ecommerce-subnet-data.subnet_id)"
      # TTL strategy: hot SKUs cached 5 min, cold SKUs 30 s (set in app config, not Terraform).
      connection_string_key_vault_secret: "$(ref:ecommerce-keyvault-prod.vault_uri)/secrets/redis-catalog-connstr"

  aks:
    type: azure.aks
    class: direct
    id: ecommerce-aks-prod
    parameters:
      namespace: product-catalog
      service_account: product-catalog-sa
      workload_identity_client_id: "$(env:CATALOG_SERVICE_CLIENT_ID)"
```

---

### How the ScoreDriver Feeds the Domain

When `ScoreDriver.ParseAsync` returns a `ScoreFile`, the orchestrator loops over `scoreFile.Resources` and reconciles each entry against the domain:

```csharp
foreach (var (resourceKey, scoreResource) in scoreFile.Resources ?? [])
{
    // 1. Resolve the ResourceTemplate by type string.
    var template = await resourceTemplateRepository.GetByTypeAsync(scoreResource.Type, ct);
    if (template is null) throw new InvalidOperationException($"No template registered for type '{scoreResource.Type}'");

    // 2. If score.yaml provides an id, look up the existing shared Resource; else declare a new one.
    Resource resource;
    if (scoreResource.Id is not null)
    {
        resource = existingResources.Single(r => r.Name == scoreResource.Id);
    }
    else
    {
        resource = Resource.Create(new CreateResourceRequest(
            organisation.Id, $"{application.Name}-{resourceKey}",
            scoreResource.Metadata?.Annotations?["description"] ?? string.Empty,
            template.Id, application.Id, environment.Id));
    }

    // 3. Register the resource as a node in the environment's dependency graph.
    if (!graph.ContainsResource(resource.Id))
        graph.AddResource(new ResourceDependency(resource.Id));

    // 4. Create a ResourceInstance with the score.yaml parameters as InputParameters.
    var version = template.GetLatestVersion()!;
    var instance = ResourceInstance.Create(new CreateResourceInstanceRequest(
        resource.Id, organisation.Id,
        $"{resource.Name}-instance",
        version.Id, environment.Id,
        scoreResource.Parameters?.AsReadOnly()));

    instance.AddConsumer(application.Id);
}
```

---

## Verification

```bash
dotnet build src/Orchitect.Domain        # domain compiles cleanly
dotnet build                             # full solution catches all call-site breakages
dotnet test                              # no existing tests broken
```

---

## Playground Examples

The following shows how the refactored domain models would be exercised in `Orchitect.Playground/Program.cs`. The scenario is a real e-commerce platform deployed to Azure: four microservices, a full shared networking layer, and a layered infrastructure stack whose dependencies the orchestrator must understand and sequence correctly.

### Scenario: E-Commerce Platform on Azure (Production Environment)

**Applications:** `order-service`, `payment-service`, `notification-service`, `product-catalog`

**Infrastructure stack (deepest to shallowest):**

```
azure-vnet  (Indirect — already exists, orchestrator reads but does not own)
  └── azure-subnet-aks        (Direct)
  └── azure-subnet-data       (Direct)
        └── azure-cosmos-orders     (Direct — private endpoint in data subnet)
        └── azure-cosmos-products   (Direct — private endpoint in data subnet)
        └── azure-service-bus       (Direct — private endpoint in data subnet)
        └── azure-redis-cache       (Direct — private endpoint in data subnet)
  └── azure-key-vault               (Direct — private endpoint, referenced by AKS)
        └── azure-acr               (Direct — pulls admin creds from Key Vault)
              └── azure-aks-cluster (Direct — pulls images from ACR, mounts KV CSI driver)
                    └── order-service-app       (Implicit — AKS workload, auto-resolved)
                    └── payment-service-app     (Implicit)
                    └── notification-service-app (Implicit)
                    └── product-catalog-app     (Implicit)
```

`ResolveOrder()` on this graph produces the correct provision sequence automatically, and `DependentCount` gates safe removal.

---

### Step 1 — Resource Templates

```csharp
// Indirect: shared VNet already exists in Azure — orchestrator registers it but never provisions/destroys it.
// ResourceTemplateKind.Indirect tells the provisioner to skip IaC apply for this node.
var vnetTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Indirect,
    Name = "Azure Virtual Network",
    Type = "azure.virtual-network",
    Description = "Shared hub VNet. Pre-provisioned by the networking team. Orchestrator reads state only.",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "3.2.1",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/aztfm/terraform-azurerm-virtual-network.git"),
        FolderPath = string.Empty,
        Tag = "v3.2.1"
    },
    Notes = "Read-only registration. Terraform state imported, not managed.",
    State = ResourceTemplateVersionState.Active
});

// Direct: AKS subnet carved out of the hub VNet.
var aksSubnetTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Direct,
    Name = "Azure Subnet (AKS)",
    Type = "azure.subnet.aks",
    Description = "Dedicated /22 subnet for AKS node pools with service endpoint policies.",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "2.0.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/aztfm/terraform-azurerm-subnet.git"),
        FolderPath = string.Empty,
        Tag = "v2.0.0"
    },
    Notes = "Enables Microsoft.ContainerService and Microsoft.KeyVault service endpoints.",
    State = ResourceTemplateVersionState.Active
});

// Direct: separate /24 subnet isolating all data-tier private endpoints from compute.
var dataSubnetTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Direct,
    Name = "Azure Subnet (Data)",
    Type = "azure.subnet.data",
    Description = "Isolated /24 subnet for CosmosDB, Service Bus, Redis private endpoints. No NSG egress to internet.",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "2.0.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/aztfm/terraform-azurerm-subnet.git"),
        FolderPath = string.Empty,
        Tag = "v2.0.0"
    },
    Notes = "private_endpoint_network_policies = Disabled required for PE attachment.",
    State = ResourceTemplateVersionState.Active
});

// Direct: Key Vault, provisioned before AKS so the CSI driver can mount secrets on node startup.
var keyVaultTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Direct,
    Name = "Azure Key Vault",
    Type = "azure.key-vault",
    Description = "Centralised secret store. AKS workloads access via Azure Workload Identity + CSI driver.",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "4.1.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/Azure/terraform-azurerm-avm-res-keyvault-vault.git"),
        FolderPath = string.Empty,
        Tag = "v4.1.0"
    },
    Notes = "Soft-delete and purge-protection mandatory for production. RBAC auth model only — no access policies.",
    State = ResourceTemplateVersionState.Active
});

// Direct: Container Registry with geo-replication to the secondary region.
var acrTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Direct,
    Name = "Azure Container Registry",
    Type = "azure.container-registry",
    Description = "Premium SKU ACR with geo-replication, content trust, and private endpoint. Admin credentials stored in Key Vault.",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "1.3.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/Azure/terraform-azurerm-avm-res-containerregistry-registry.git"),
        FolderPath = string.Empty,
        Tag = "v1.3.0"
    },
    Notes = "Requires Key Vault to exist first — admin password written to KV secret on creation.",
    State = ResourceTemplateVersionState.Active
});

// Direct: AKS cluster with system + user node pools, OIDC issuer, Workload Identity, and Key Vault CSI driver.
var aksTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Direct,
    Name = "Azure Kubernetes Service",
    Type = "azure.aks",
    Description = "Production-grade AKS with zone-redundant system pool, user pool with autoscaler, OIDC + Workload Identity, Azure CNI Overlay.",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "7.5.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/Azure/terraform-azurerm-avm-res-containerservice-managedcluster.git"),
        FolderPath = string.Empty,
        Tag = "v7.5.0"
    },
    Notes = "azure_policy_enabled = true required for PCI compliance. Network plugin = azure, overlay mode.",
    State = ResourceTemplateVersionState.Active
});

// Direct: CosmosDB account for order-service. Serverless not used — this needs predictable latency.
var cosmosOrdersTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Direct,
    Name = "Azure CosmosDB (Orders)",
    Type = "azure.cosmosdb.orders",
    Description = "Autoscale provisioned CosmosDB with Session consistency. Private endpoint in data subnet. Multi-region writes disabled (cost control).",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "2.1.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/Azure/terraform-azurerm-avm-res-documentdb-databaseaccount.git"),
        FolderPath = string.Empty,
        Tag = "v2.1.0"
    },
    Notes = "Connection string written to Key Vault secret 'cosmos-orders-connstr' post-provision.",
    State = ResourceTemplateVersionState.Active
});

// Direct: Service Bus Premium for reliable async messaging between services.
var serviceBusTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Direct,
    Name = "Azure Service Bus",
    Type = "azure.service-bus",
    Description = "Premium tier, zone-redundant Service Bus namespace with private endpoint. Used for order-placed, payment-processed, and notification-requested events.",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "1.0.2",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/Azure/terraform-azurerm-avm-res-servicebus-namespace.git"),
        FolderPath = string.Empty,
        Tag = "v1.0.2"
    },
    Notes = "Premium required for private endpoints and 1 MB message size. Capacity = 1 messaging unit.",
    State = ResourceTemplateVersionState.Active
});

// Direct: Redis Cache for product catalog read-through caching.
var redisCacheTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Kind = ResourceTemplateKind.Direct,
    Name = "Azure Cache for Redis",
    Type = "azure.redis-cache",
    Description = "C2 Standard Redis with private endpoint and TLS-only. Used by product-catalog for sub-millisecond read latency on hot inventory data.",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "1.1.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/Azure/terraform-azurerm-avm-res-cache-redis.git"),
        FolderPath = string.Empty,
        Tag = "v1.1.0"
    },
    Notes = "enable_non_ssl_port = false enforced. Redis version 7.2.",
    State = ResourceTemplateVersionState.Active
});
```

---

### Step 2 — Applications and Environment

```csharp
var organisation = Organisation.Create("ecommerce-platform");
var production = Environment.Create("production", "Live customer-facing environment. PCI-DSS scope.", organisation.Id);

var orderService    = Application.Create("order-service",    new Repository { Name = "order-service",    Url = new Uri("https://github.com/acme/order-service.git"),    Provider = RepositoryProvider.GitHub }, organisation.Id);
var paymentService  = Application.Create("payment-service",  new Repository { Name = "payment-service",  Url = new Uri("https://github.com/acme/payment-service.git"),  Provider = RepositoryProvider.GitHub }, organisation.Id);
var notifService    = Application.Create("notification-service", new Repository { Name = "notification-service", Url = new Uri("https://github.com/acme/notification-service.git"), Provider = RepositoryProvider.GitHub }, organisation.Id);
var catalogService  = Application.Create("product-catalog",  new Repository { Name = "product-catalog",  Url = new Uri("https://github.com/acme/product-catalog.git"),  Provider = RepositoryProvider.GitHub }, organisation.Id);
```

---

### Step 3 — Declare Resources (desired state)

Each `Resource.Create` call says: "this application needs this infrastructure in this environment."

```csharp
var vnetResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-hub-vnet",
    "Shared hub VNet /16 in East US. Contains all AKS, data, and management subnets.",
    vnetTemplate.Id, orderService.Id, production.Id));

var aksSubnetResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-subnet-aks",
    "/22 address space 10.240.0.0/22 for AKS node pools.",
    aksSubnetTemplate.Id, orderService.Id, production.Id));

var dataSubnetResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-subnet-data",
    "/24 address space 10.240.8.0/24 for private endpoints only.",
    dataSubnetTemplate.Id, orderService.Id, production.Id));

var keyVaultResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-keyvault-prod",
    "Production Key Vault. Holds all service connection strings, ACR admin creds, and TLS certs.",
    keyVaultTemplate.Id, orderService.Id, production.Id));

var acrResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-acr-prod",
    "Container registry serving all four microservice images. Geo-replicated to West Europe.",
    acrTemplate.Id, orderService.Id, production.Id));

var aksResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-aks-prod",
    "Production AKS cluster. System pool: 3×Standard_D4s_v5 zone-redundant. User pool: 2–10×Standard_D8s_v5 autoscaled.",
    aksTemplate.Id, orderService.Id, production.Id));

var cosmosOrdersResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-cosmos-orders",
    "CosmosDB account for order documents. 4000 RU/s autoscale. Session consistency.",
    cosmosOrdersTemplate.Id, orderService.Id, production.Id));

var serviceBusResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-servicebus-prod",
    "Service Bus namespace. Topics: order-placed, payment-processed, notification-requested.",
    serviceBusTemplate.Id, orderService.Id, production.Id));

var redisCacheResource = Resource.Create(new CreateResourceRequest(
    organisation.Id, "ecommerce-redis-catalog",
    "Redis cache for product catalog. TTL 300s on hot paths. Used by product-catalog only.",
    redisCacheTemplate.Id, catalogService.Id, production.Id));
```

---

### Step 4 — Build the Dependency Graph

```csharp
// One graph per environment. Scoped so graph.ResolveOrder() only considers this environment's resources.
var graph = ResourceDependencyGraph.Create(organisation.Id, production.Id);

var vnetDep         = new ResourceDependency(vnetResource.Id);
var aksSubnetDep    = new ResourceDependency(aksSubnetResource.Id);
var dataSubnetDep   = new ResourceDependency(dataSubnetResource.Id);
var kvDep           = new ResourceDependency(keyVaultResource.Id);
var acrDep          = new ResourceDependency(acrResource.Id);
var aksDep          = new ResourceDependency(aksResource.Id);
var cosmosOrdersDep = new ResourceDependency(cosmosOrdersResource.Id);
var serviceBusDep   = new ResourceDependency(serviceBusResource.Id);
var redisDep        = new ResourceDependency(redisCacheResource.Id);

graph.AddResource(vnetDep);
graph.AddResource(aksSubnetDep);
graph.AddResource(dataSubnetDep);
graph.AddResource(kvDep);
graph.AddResource(acrDep);
graph.AddResource(aksDep);
graph.AddResource(cosmosOrdersDep);
graph.AddResource(serviceBusDep);
graph.AddResource(redisDep);

// Subnets depend on the VNet existing first.
graph.AddDependency(aksSubnetDep.Id,    vnetDep.Id);
graph.AddDependency(dataSubnetDep.Id,   vnetDep.Id);

// Key Vault private endpoint lands in the data subnet.
graph.AddDependency(kvDep.Id,           dataSubnetDep.Id);

// ACR needs Key Vault to write admin credentials to on creation.
graph.AddDependency(acrDep.Id,          kvDep.Id);

// AKS needs: its subnet, ACR to pull images, and Key Vault for the CSI driver.
graph.AddDependency(aksDep.Id,          aksSubnetDep.Id);
graph.AddDependency(aksDep.Id,          acrDep.Id);
graph.AddDependency(aksDep.Id,          kvDep.Id);

// Data-tier resources need the data subnet for their private endpoints.
graph.AddDependency(cosmosOrdersDep.Id, dataSubnetDep.Id);
graph.AddDependency(serviceBusDep.Id,   dataSubnetDep.Id);
graph.AddDependency(redisDep.Id,        dataSubnetDep.Id);

// Resolve provisioning order using Kahn's topological sort.
// Result guaranteed to respect all edges. Indirect nodes (VNet) appear first
// but are skipped by the provisioner (Kind check).
var provisionOrder = graph.ResolveOrder();

Console.WriteLine("=== Provision Order ===");
foreach (var dep in provisionOrder)
{
    Console.WriteLine($"  {dep.ResourceId.Value}");
}
// Expected sequence (one valid linearisation):
//   1. ecommerce-hub-vnet        (Indirect — skip IaC apply, just import state)
//   2. ecommerce-subnet-aks
//   3. ecommerce-subnet-data
//   4. ecommerce-keyvault-prod
//   5. ecommerce-acr-prod
//   6. ecommerce-cosmos-orders
//   7. ecommerce-servicebus-prod
//   8. ecommerce-redis-catalog
//   9. ecommerce-aks-prod        (last — needs everything above)
```

---

### Step 5 — Provision via ResourceInstances with Real Parameters

```csharp
var aksSubnetVersion = aksSubnetTemplate.GetLatestVersion()!;
var aksSubnetInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        aksSubnetResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-subnet-aks-prod-instance",
    TemplateVersionId: aksSubnetVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, string>
    {
        ["resource_group_name"]  = "rg-ecommerce-networking-prod",
        ["virtual_network_name"] = "vnet-ecommerce-hub-prod",
        ["address_prefixes"]     = "10.240.0.0/22",
        ["service_endpoints"]    = "Microsoft.ContainerService,Microsoft.KeyVault",
        // Disabling network policies is required for private endpoints to attach.
        ["private_endpoint_network_policies"] = "Disabled"
    }));

var kvVersion = keyVaultTemplate.GetLatestVersion()!;
var kvInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        keyVaultResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-keyvault-prod-instance",
    TemplateVersionId: kvVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, string>
    {
        ["resource_group_name"]          = "rg-ecommerce-secrets-prod",
        ["location"]                     = "eastus",
        ["sku_name"]                     = "standard",
        // Required for PCI-DSS: once enabled, a vault cannot be immediately purged.
        ["soft_delete_retention_days"]   = "90",
        ["purge_protection_enabled"]     = "true",
        // RBAC model only — no legacy access policies.
        ["enable_rbac_authorization"]    = "true",
        ["public_network_access_enabled"]= "false",
        ["private_endpoint_subnet_id"]   = "$(ref:ecommerce-subnet-data.subnet_id)"
    }));

var acrVersion = acrTemplate.GetLatestVersion()!;
var acrInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        acrResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-acr-prod-instance",
    TemplateVersionId: acrVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, string>
    {
        ["resource_group_name"]                = "rg-ecommerce-containers-prod",
        ["location"]                           = "eastus",
        ["sku"]                                = "Premium",
        // Content trust ensures only signed images can be deployed.
        ["content_trust_enabled"]              = "true",
        ["public_network_access_enabled"]      = "false",
        // Geo-replication to West Europe for DR.
        ["georeplications"]                    = "westeurope",
        ["admin_credentials_key_vault_secret"] = "$(ref:ecommerce-keyvault-prod.vault_uri)/secrets/acr-admin-password"
    }));

var aksVersion = aksTemplate.GetLatestVersion()!;
var aksInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        aksResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-aks-prod-instance",
    TemplateVersionId: aksVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, string>
    {
        ["resource_group_name"]        = "rg-ecommerce-compute-prod",
        ["location"]                   = "eastus",
        ["kubernetes_version"]         = "1.30.3",
        ["network_plugin"]             = "azure",
        ["network_plugin_mode"]        = "overlay",
        // System pool: fixed size, zone-redundant, cordoned from workloads.
        ["system_node_count"]          = "3",
        ["system_vm_size"]             = "Standard_D4s_v5",
        ["system_availability_zones"]  = "1,2,3",
        // User pool: autoscaled, spot-tolerant for batch jobs.
        ["user_node_min_count"]        = "2",
        ["user_node_max_count"]        = "10",
        ["user_vm_size"]               = "Standard_D8s_v5",
        ["oidc_issuer_enabled"]        = "true",
        ["workload_identity_enabled"]  = "true",
        // CSI driver mounts Key Vault secrets as volumes on pod startup.
        ["key_vault_secrets_provider"] = "true",
        ["acr_id"]                     = "$(ref:ecommerce-acr-prod.registry_id)",
        ["vnet_subnet_id"]             = "$(ref:ecommerce-subnet-aks.subnet_id)"
    }));

var cosmosVersion = cosmosOrdersTemplate.GetLatestVersion()!;
var cosmosInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        cosmosOrdersResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-cosmos-orders-prod-instance",
    TemplateVersionId: cosmosVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, string>
    {
        ["resource_group_name"]               = "rg-ecommerce-data-prod",
        ["location"]                          = "eastus",
        ["offer_type"]                        = "Standard",
        // Session consistency: strong enough for order workflows, avoids cross-region write latency.
        ["consistency_level"]                 = "Session",
        ["enable_automatic_failover"]         = "true",
        // 4000 RU/s autoscale handles order spikes without manual intervention.
        ["max_throughput"]                    = "4000",
        ["public_network_access_enabled"]     = "false",
        ["private_endpoint_subnet_id"]        = "$(ref:ecommerce-subnet-data.subnet_id)",
        // Connection string written here so AKS workloads retrieve it via KV CSI driver.
        ["connection_string_key_vault_secret"]= "$(ref:ecommerce-keyvault-prod.vault_uri)/secrets/cosmos-orders-connstr"
    }));

// Register all consumers on the AKS instance so the graph knows which apps depend on it.
aksInstance.AddConsumer(orderService.Id);
aksInstance.AddConsumer(paymentService.Id);
aksInstance.AddConsumer(notifService.Id);
aksInstance.AddConsumer(catalogService.Id);

cosmosInstance.AddConsumer(orderService.Id);
```

---

### Step 6 — Lifecycle Transitions

```csharp
// Provisioner begins applying Terraform for each instance in ResolveOrder() sequence.
aksSubnetInstance.Transition(ResourceInstanceStatus.Provisioning);
// ... Terraform apply completes ...
aksSubnetInstance.Transition(ResourceInstanceStatus.Active, new ResourceInstanceLocation
{
    Location = new Uri("https://portal.azure.com/#resource/subscriptions/xxxxxxxx/resourceGroups/rg-ecommerce-networking-prod/providers/Microsoft.Network/virtualNetworks/vnet-ecommerce-hub-prod/subnets/snet-aks"),
    Workspace = "ecommerce-prod"   // Terraform workspace used for state isolation
});

// A failed provision: KV firewall misconfigured — private endpoint rejected.
kvInstance.Transition(ResourceInstanceStatus.Provisioning);
kvInstance.Transition(ResourceInstanceStatus.Failed);
// Operator fixes firewall rule, retries — creates a new Provisioning transition, not an amend.
kvInstance.Transition(ResourceInstanceStatus.Provisioning);
kvInstance.Transition(ResourceInstanceStatus.Active, new ResourceInstanceLocation
{
    Location = new Uri("https://ecommerce-keyvault-prod.vault.azure.net"),
    Workspace = "ecommerce-prod"
});
```

---

### Step 7 — Safe Removal Check

```csharp
// Attempt to decommission the data subnet. The graph prevents this because
// CosmosDB, Service Bus, and Redis all have private endpoints inside it.
graph.TryGetNodeId(dataSubnetResource.Id, out var dataSubnetNodeId);
int dependentCount = graph.DependentCount(dataSubnetNodeId);  // returns 3

if (dependentCount > 0)
{
    Console.WriteLine($"Cannot remove data subnet: {dependentCount} resources still depend on it.");
    Console.WriteLine("Must first remove: cosmos-orders, servicebus-prod, redis-catalog.");
}
else
{
    cosmosInstance.Transition(ResourceInstanceStatus.PendingRemoval);
    // ... IaC destroy runs ...
    cosmosInstance.Transition(ResourceInstanceStatus.Removed);
    graph.RemoveResource(cosmosOrdersDep.Id);

    // Now the data subnet check passes — provision again if needed, or fully remove.
}

// Check whether a direct path exists between two resources — used to explain why
// you cannot update the VNet without also re-creating all subnets downstream.
bool aksBlockedByVnet = graph.HasDependencyPath(aksDep.Id, vnetDep.Id);  // true
Console.WriteLine($"AKS transitively blocked by VNet change: {aksBlockedByVnet}");
```
