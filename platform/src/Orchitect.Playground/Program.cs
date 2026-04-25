using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure;
using Orchitect.Infrastructure.Engine.Resources;
using Orchitect.Persistence;
using Orchitect.ServiceDefaults;
using Environment = Orchitect.Domain.Engine.Environment.Environment;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.AddServiceDefaults();

builder.Services
    .AddPersistenceServices()
    .AddInfrastructureServices();
builder.Configuration.AddUserSecrets<Program>();

await builder.Services.ApplyMigrations();

using IHost host = builder.Build();

// =============================================================================
// Step 1 — Resource Templates
// =============================================================================

var organisation = Organisation.Create("ecommerce-platform");

// Indirect: shared VNet already exists — orchestrator registers it but never provisions/destroys it.
var vnetTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var aksSubnetTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var dataSubnetTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var keyVaultTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var acrTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var aksTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var cosmosOrdersTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var serviceBusTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var redisCacheTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
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

var organisationRepository = host.Services.GetRequiredService<IOrganisationRepository>();
await organisationRepository.CreateAsync(organisation);

var resourceTemplateRepository = host.Services.GetRequiredService<IResourceTemplateRepository>();
await resourceTemplateRepository.CreateAsync(vnetTemplate);
await resourceTemplateRepository.CreateAsync(aksSubnetTemplate);
await resourceTemplateRepository.CreateAsync(dataSubnetTemplate);
await resourceTemplateRepository.CreateAsync(keyVaultTemplate);
await resourceTemplateRepository.CreateAsync(acrTemplate);
await resourceTemplateRepository.CreateAsync(aksTemplate);
await resourceTemplateRepository.CreateAsync(cosmosOrdersTemplate);
await resourceTemplateRepository.CreateAsync(serviceBusTemplate);
await resourceTemplateRepository.CreateAsync(redisCacheTemplate);

// =============================================================================
// Step 2 — Applications and Environment
// =============================================================================

var production = Environment.Create("production", "Live customer-facing environment. PCI-DSS scope.", organisation.Id);

var orderService   = Application.Create("order-service",         new Repository { Name = "order-service",         Url = new Uri("https://github.com/acme/order-service.git"),         Provider = RepositoryProvider.GitHub }, organisation.Id);
var paymentService = Application.Create("payment-service",        new Repository { Name = "payment-service",       Url = new Uri("https://github.com/acme/payment-service.git"),       Provider = RepositoryProvider.GitHub }, organisation.Id);
var notifService   = Application.Create("notification-service",   new Repository { Name = "notification-service",  Url = new Uri("https://github.com/acme/notification-service.git"),  Provider = RepositoryProvider.GitHub }, organisation.Id);
var catalogService = Application.Create("product-catalog",        new Repository { Name = "product-catalog",       Url = new Uri("https://github.com/acme/product-catalog.git"),       Provider = RepositoryProvider.GitHub }, organisation.Id);

var commitId   = new CommitId("7b926d5c23d0e806c62d4c86e25fc73564efb8a1");
var deployment = Deployment.Create(orderService.Id, production.Id, commitId);

// =============================================================================
// Step 3 — Declare Resources (desired state)
// =============================================================================

// Indirect: pre-existing VNet — orchestrator never provisions or destroys it.
var vnetResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-hub-vnet",
    Description:       "Shared hub VNet /16 in East US. Contains all AKS, data, and management subnets.",
    ResourceTemplateId: vnetTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Indirect));

var aksSubnetResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-subnet-aks",
    Description:       "/22 address space 10.240.0.0/22 for AKS node pools.",
    ResourceTemplateId: aksSubnetTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Direct));

var dataSubnetResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-subnet-data",
    Description:       "/24 address space 10.240.8.0/24 for private endpoints only.",
    ResourceTemplateId: dataSubnetTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Direct));

var keyVaultResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-keyvault-prod",
    Description:       "Production Key Vault. Holds all service connection strings, ACR admin creds, and TLS certs.",
    ResourceTemplateId: keyVaultTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Direct));

var acrResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-acr-prod",
    Description:       "Container registry serving all four microservice images. Geo-replicated to West Europe.",
    ResourceTemplateId: acrTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Direct));

var aksResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-aks-prod",
    Description:       "Production AKS cluster. System pool: 3×Standard_D4s_v5 zone-redundant. User pool: 2–10×Standard_D8s_v5 autoscaled.",
    ResourceTemplateId: aksTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Direct));

// ApplicationId set — order-service is the sole writer/owner of this CosmosDB account.
var cosmosOrdersResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-cosmos-orders",
    Description:       "CosmosDB account for order documents. 4000 RU/s autoscale. Session consistency.",
    ResourceTemplateId: cosmosOrdersTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Direct,
    ApplicationId:     orderService.Id));

var serviceBusResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-servicebus-prod",
    Description:       "Service Bus namespace. Topics: order-placed, payment-processed, notification-requested.",
    ResourceTemplateId: serviceBusTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Direct));

// ApplicationId set — product-catalog is the sole consumer of this Redis instance.
var redisCacheResource = Resource.Create(new CreateResourceRequest(
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-redis-catalog",
    Description:       "Redis cache for product catalog. TTL 300s on hot paths.",
    ResourceTemplateId: redisCacheTemplate.Id,
    EnvironmentId:     production.Id,
    Kind:              ResourceKind.Direct,
    ApplicationId:     catalogService.Id));

// =============================================================================
// Step 4 — Build the Dependency Graph
// =============================================================================

// One graph per environment — ResolveOrder() only considers this environment's resources.
var graph = ResourceDependencyGraph.Create(organisation.Id, production.Id);

graph.AddResource(vnetResource.Id);
graph.AddResource(aksSubnetResource.Id);
graph.AddResource(dataSubnetResource.Id);
graph.AddResource(keyVaultResource.Id);
graph.AddResource(acrResource.Id);
graph.AddResource(aksResource.Id);
graph.AddResource(cosmosOrdersResource.Id);
graph.AddResource(serviceBusResource.Id);
graph.AddResource(redisCacheResource.Id);

// Subnets depend on the VNet existing first.
graph.AddDependency(aksSubnetResource.Id,    vnetResource.Id);
graph.AddDependency(dataSubnetResource.Id,   vnetResource.Id);

// Key Vault private endpoint lands in the data subnet.
graph.AddDependency(keyVaultResource.Id,     dataSubnetResource.Id);

// ACR needs Key Vault to write admin credentials to on creation.
graph.AddDependency(acrResource.Id,          keyVaultResource.Id);

// AKS needs: its subnet, ACR to pull images, and Key Vault for the CSI driver.
graph.AddDependency(aksResource.Id,          aksSubnetResource.Id);
graph.AddDependency(aksResource.Id,          acrResource.Id);
graph.AddDependency(aksResource.Id,          keyVaultResource.Id);

// Data-tier resources need the data subnet for their private endpoints.
graph.AddDependency(cosmosOrdersResource.Id, dataSubnetResource.Id);
graph.AddDependency(serviceBusResource.Id,   dataSubnetResource.Id);
graph.AddDependency(redisCacheResource.Id,   dataSubnetResource.Id);

// Kahn's topological sort — Indirect nodes (VNet) appear first but the provisioner
// skips IaC apply for them based on the Kind check.
var provisionOrder = graph.ResolveOrder();

Console.WriteLine("=== Provision Order ===");
foreach (var id in provisionOrder)
{
    Console.WriteLine($"  {id.Value}");
}
// Expected sequence (one valid linearisation):
//   1. ecommerce-hub-vnet        (Indirect — import state only)
//   2. ecommerce-subnet-aks
//   3. ecommerce-subnet-data
//   4. ecommerce-keyvault-prod
//   5. ecommerce-acr-prod
//   6. ecommerce-cosmos-orders
//   7. ecommerce-servicebus-prod
//   8. ecommerce-redis-catalog
//   9. ecommerce-aks-prod        (last — needs everything above)

// =============================================================================
// Step 5 — Provision via ResourceInstances with Real Parameters
// =============================================================================

var aksSubnetVersion  = aksSubnetTemplate.GetLatestVersion()!;
var aksSubnetInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        aksSubnetResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-subnet-aks-prod-instance",
    TemplateVersionId: aksSubnetVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, JsonElement>
    {
        ["resource_group_name"]  = JsonSerializer.SerializeToElement("rg-ecommerce-networking-prod"),
        ["virtual_network_name"] = JsonSerializer.SerializeToElement("vnet-ecommerce-hub-prod"),
        ["address_prefixes"]     = JsonSerializer.SerializeToElement("10.240.0.0/22"),
        ["service_endpoints"]    = JsonSerializer.SerializeToElement("Microsoft.ContainerService,Microsoft.KeyVault"),
        // Disabling network policies is required for private endpoints to attach.
        ["private_endpoint_network_policies"] = JsonSerializer.SerializeToElement("Disabled")
    }));

var kvVersion  = keyVaultTemplate.GetLatestVersion()!;
var kvInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        keyVaultResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-keyvault-prod-instance",
    TemplateVersionId: kvVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, JsonElement>
    {
        ["resource_group_name"]           = JsonSerializer.SerializeToElement("rg-ecommerce-secrets-prod"),
        ["location"]                      = JsonSerializer.SerializeToElement("eastus"),
        ["sku_name"]                      = JsonSerializer.SerializeToElement("standard"),
        // Required for PCI-DSS: once enabled, a vault cannot be immediately purged.
        ["soft_delete_retention_days"]    = JsonSerializer.SerializeToElement("90"),
        ["purge_protection_enabled"]      = JsonSerializer.SerializeToElement("true"),
        // RBAC model only — no legacy access policies.
        ["enable_rbac_authorization"]     = JsonSerializer.SerializeToElement("true"),
        ["public_network_access_enabled"] = JsonSerializer.SerializeToElement("false"),
        ["private_endpoint_subnet_id"]    = JsonSerializer.SerializeToElement("$(ref:ecommerce-subnet-data.subnet_id)")
    }));

var acrVersion  = acrTemplate.GetLatestVersion()!;
var acrInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        acrResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-acr-prod-instance",
    TemplateVersionId: acrVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, JsonElement>
    {
        ["resource_group_name"]                = JsonSerializer.SerializeToElement("rg-ecommerce-containers-prod"),
        ["location"]                           = JsonSerializer.SerializeToElement("eastus"),
        ["sku"]                                = JsonSerializer.SerializeToElement("Premium"),
        // Content trust ensures only signed images can be deployed.
        ["content_trust_enabled"]              = JsonSerializer.SerializeToElement("true"),
        ["public_network_access_enabled"]      = JsonSerializer.SerializeToElement("false"),
        // Geo-replication to West Europe for DR.
        ["georeplications"]                    = JsonSerializer.SerializeToElement("westeurope"),
        ["admin_credentials_key_vault_secret"] = JsonSerializer.SerializeToElement("$(ref:ecommerce-keyvault-prod.vault_uri)/secrets/acr-admin-password")
    }));

var aksVersion  = aksTemplate.GetLatestVersion()!;
var aksInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        aksResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-aks-prod-instance",
    TemplateVersionId: aksVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, JsonElement>
    {
        ["resource_group_name"]        = JsonSerializer.SerializeToElement("rg-ecommerce-compute-prod"),
        ["location"]                   = JsonSerializer.SerializeToElement("eastus"),
        ["kubernetes_version"]         = JsonSerializer.SerializeToElement("1.30.3"),
        ["network_plugin"]             = JsonSerializer.SerializeToElement("azure"),
        ["network_plugin_mode"]        = JsonSerializer.SerializeToElement("overlay"),
        // System pool: fixed size, zone-redundant, cordoned from workloads.
        ["system_node_count"]          = JsonSerializer.SerializeToElement("3"),
        ["system_vm_size"]             = JsonSerializer.SerializeToElement("Standard_D4s_v5"),
        ["system_availability_zones"]  = JsonSerializer.SerializeToElement("1,2,3"),
        // User pool: autoscaled, spot-tolerant for batch jobs.
        ["user_node_min_count"]        = JsonSerializer.SerializeToElement("2"),
        ["user_node_max_count"]        = JsonSerializer.SerializeToElement("10"),
        ["user_vm_size"]               = JsonSerializer.SerializeToElement("Standard_D8s_v5"),
        ["oidc_issuer_enabled"]        = JsonSerializer.SerializeToElement("true"),
        ["workload_identity_enabled"]  = JsonSerializer.SerializeToElement("true"),
        // CSI driver mounts Key Vault secrets as volumes on pod startup.
        ["key_vault_secrets_provider"] = JsonSerializer.SerializeToElement("true"),
        ["acr_id"]                     = JsonSerializer.SerializeToElement("$(ref:ecommerce-acr-prod.registry_id)"),
        ["vnet_subnet_id"]             = JsonSerializer.SerializeToElement("$(ref:ecommerce-subnet-aks.subnet_id)")
    }));

var cosmosVersion  = cosmosOrdersTemplate.GetLatestVersion()!;
var cosmosInstance = ResourceInstance.Create(new CreateResourceInstanceRequest(
    ResourceId:        cosmosOrdersResource.Id,
    OrganisationId:    organisation.Id,
    Name:              "ecommerce-cosmos-orders-prod-instance",
    TemplateVersionId: cosmosVersion.Id,
    EnvironmentId:     production.Id,
    InputParameters: new Dictionary<string, JsonElement>
    {
        ["resource_group_name"]                = JsonSerializer.SerializeToElement("rg-ecommerce-data-prod"),
        ["location"]                           = JsonSerializer.SerializeToElement("eastus"),
        ["offer_type"]                         = JsonSerializer.SerializeToElement("Standard"),
        // Session consistency: strong enough for order workflows, avoids cross-region write latency.
        ["consistency_level"]                  = JsonSerializer.SerializeToElement("Session"),
        ["enable_automatic_failover"]          = JsonSerializer.SerializeToElement("true"),
        // 4000 RU/s autoscale handles order spikes without manual intervention.
        ["max_throughput"]                     = JsonSerializer.SerializeToElement("4000"),
        ["public_network_access_enabled"]      = JsonSerializer.SerializeToElement("false"),
        ["private_endpoint_subnet_id"]         = JsonSerializer.SerializeToElement("$(ref:ecommerce-subnet-data.subnet_id)"),
        // Connection string written here so AKS workloads retrieve it via KV CSI driver.
        ["connection_string_key_vault_secret"] = JsonSerializer.SerializeToElement("$(ref:ecommerce-keyvault-prod.vault_uri)/secrets/cosmos-orders-connstr")
    }));

// Consumers belong on the logical Resource, not the instance — they survive re-provisioning.
aksResource.AddConsumer(orderService.Id);
aksResource.AddConsumer(paymentService.Id);
aksResource.AddConsumer(notifService.Id);
aksResource.AddConsumer(catalogService.Id);

cosmosOrdersResource.AddConsumer(orderService.Id);

// =============================================================================
// Step 6 — Lifecycle Transitions
// =============================================================================

// Provisioner begins applying Terraform for each instance in ResolveOrder() sequence.
aksSubnetInstance.Transition(ResourceInstanceStatus.Provisioning);
aksSubnetInstance.Transition(ResourceInstanceStatus.Active, new ResourceInstanceOutput
{
    Location  = new Uri("https://portal.azure.com/#resource/subscriptions/xxxxxxxx/resourceGroups/rg-ecommerce-networking-prod/providers/Microsoft.Network/virtualNetworks/vnet-ecommerce-hub-prod/subnets/snet-aks"),
    Workspace = "ecommerce-prod"
});

acrInstance.Transition(ResourceInstanceStatus.Provisioning);
acrInstance.Transition(ResourceInstanceStatus.Active, new ResourceInstanceOutput
{
    Location  = new Uri("https://ecommerce-acr-prod.azurecr.io"),
    Workspace = "ecommerce-prod"
});

aksInstance.Transition(ResourceInstanceStatus.Provisioning);
aksInstance.Transition(ResourceInstanceStatus.Active, new ResourceInstanceOutput
{
    Location  = new Uri("https://ecommerce-aks-prod.hcp.eastus.azmk8s.io"),
    Workspace = "ecommerce-prod"
});

// A failed provision: KV firewall misconfigured — private endpoint rejected.
kvInstance.Transition(ResourceInstanceStatus.Provisioning);
kvInstance.Transition(ResourceInstanceStatus.Failed);
// Operator fixes firewall rule, retries — re-enters Pending then Provisioning (not an amend).
kvInstance.Transition(ResourceInstanceStatus.Pending);
kvInstance.Transition(ResourceInstanceStatus.Provisioning);
kvInstance.Transition(ResourceInstanceStatus.Active, new ResourceInstanceOutput
{
    Location  = new Uri("https://ecommerce-keyvault-prod.vault.azure.net"),
    Workspace = "ecommerce-prod"
});

// =============================================================================
// Step 7 — Safe Removal Check
// =============================================================================

// Attempt to decommission the data subnet. The graph prevents this because
// CosmosDB, Service Bus, and Redis all have private endpoints inside it.
int dependentCount = graph.DependentCount(dataSubnetResource.Id);

if (dependentCount > 0)
{
    Console.WriteLine($"Cannot remove data subnet: {dependentCount} resources still depend on it.");
    Console.WriteLine("Must first remove: cosmos-orders, servicebus-prod, redis-catalog.");
}
else
{
    cosmosInstance.Transition(ResourceInstanceStatus.PendingRemoval);
    cosmosInstance.Transition(ResourceInstanceStatus.Removing);
    cosmosInstance.Transition(ResourceInstanceStatus.Removed);
    graph.RemoveResource(cosmosOrdersResource.Id);
}

// Check whether a direct path exists between two resources — used to explain why
// you cannot update the VNet without also re-creating all subnets downstream.
bool aksBlockedByVnet = graph.HasDependencyPath(aksResource.Id, vnetResource.Id);
Console.WriteLine($"AKS transitively blocked by VNet change: {aksBlockedByVnet}");

// Demonstrate the ResourceProvisioner flow via a score.yaml-driven deployment.
var resourceProvisioner = host.Services.GetRequiredService<IResourceProvisioner>();
await resourceProvisioner.StartAsync(orderService, deployment, CancellationToken.None);
