using Orchitect.Engine.Infrastructure;
using Orchitect.Engine.Infrastructure.Resources;
using Orchitect.Engine.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchitect.Core.Domain.Organisation;
using Orchitect.Core.Persistence;
using Orchitect.Engine.Domain.Application;
using Orchitect.Engine.Domain.Deployment;
using Orchitect.Engine.Domain.Resource;
using Orchitect.Engine.Domain.ResourceDependency;
using Orchitect.Engine.Domain.ResourceTemplate;
using Orchitect.Inventory.Persistence;
using Environment = Orchitect.Engine.Domain.Environment.Environment;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.AddServiceDefaults();

builder.Services
    .AddCorePersistenceServices()
    .AddEnginePersistenceServices()
    .AddInventoryPersistenceServices()
    .AddEngineInfrastructureServices();
builder.Configuration.AddUserSecrets<Program>();

await builder.Services.ApplyCoreMigrations();
await builder.Services.ApplyEngineMigrations();
await builder.Services.ApplyInventoryMigrations();

using IHost host = builder.Build();

var organisation = Organisation.Create("my-organisation");

var azureStorageAccount = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Name = "Azure Storage Account",
    Type = "azure.storage-account",
    Description = "Azure Storage Account Terraform Module",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "1.0.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/aztfm/terraform-azurerm-storage-account.git"),
        FolderPath = string.Empty,
        Tag = string.Empty
    },
    Notes = string.Empty,
    State = ResourceTemplateVersionState.Active
});

var azureVirtualNetwork = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Name = "Azure Virtual Network",
    Type = "azure.virtual-network",
    Description = "Azure Virtual Network Terraform Module",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "1.0.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/aztfm/terraform-azurerm-virtual-network.git"),
        FolderPath = string.Empty,
        Tag = string.Empty
    },
    Notes = string.Empty,
    State = ResourceTemplateVersionState.Active
});

var azureContainerRegistry = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Name = "Azure Container Registry",
    Type = "azure.container-registry",
    Description = "Azure Container Registry Terraform Module",
    Provider = ResourceTemplateProvider.Terraform,
    Version = "1.0.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/Azure/terraform-azurerm-avm-res-containerregistry-registry.git"),
        FolderPath = string.Empty,
        Tag = string.Empty
    },
    Notes = string.Empty,
    State = ResourceTemplateVersionState.Active
});

var argoCdTemplate = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
{
    OrganisationId = organisation.Id,
    Name = "ArgoCD Helm Chart",
    Type = "helm.argocd",
    Description = "An ArgoCD Helm Chart",
    Provider = ResourceTemplateProvider.Helm,
    Version = "1.0",
    Source = new ResourceTemplateVersionSource
    {
        BaseUrl = new Uri("https://github.com/bitnami/charts.git"),
        FolderPath = "bitnami/argo-cd",
        Tag = string.Empty
    },
    Notes = string.Empty,
    State = ResourceTemplateVersionState.Active
});

var organisationRepository = host.Services.GetRequiredService<IOrganisationRepository>();
await organisationRepository.CreateAsync(organisation);

var resourceTemplateRepository = host.Services.GetRequiredService<IResourceTemplateRepository>();
await resourceTemplateRepository.CreateAsync(azureStorageAccount);
await resourceTemplateRepository.CreateAsync(azureVirtualNetwork);
await resourceTemplateRepository.CreateAsync(azureContainerRegistry);
await resourceTemplateRepository.CreateAsync(argoCdTemplate);
var paymentApi = Application.Create("payment-api", new Repository
{
    Name = "payment api repository",
    Url = new Uri("https://github.com/james-d12/Orchitect-Example.git"),
    Provider = RepositoryProvider.GitHub
}, organisation.Id);

var devEnvironment = Environment.Create("dev", "The Development Environment", organisation.Id);

var commitId = new CommitId("7b926d5c23d0e806c62d4c86e25fc73564efb8a1");

var deployment = Deployment.Create(paymentApi.Id, devEnvironment.Id, commitId);

var resource = new Resource
{
    Id = new ResourceId(),
    Name = "mypaymentstorage",
    ResourceTemplateId = azureStorageAccount.Id,
    ApplicationId = paymentApi.Id,
    EnvironmentId = devEnvironment.Id,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

var resource2 = new Resource
{
    Id = new ResourceId(),
    Name = "myotherpaymentstorage",
    ResourceTemplateId = azureStorageAccount.Id,
    ApplicationId = paymentApi.Id,
    EnvironmentId = devEnvironment.Id,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

var dependencies = new ResourceDependencyGraph();

var apiDependency = new ResourceDependency(paymentApi.Name);
var resource1Dependency = new ResourceDependency(resource.Name);
var resource2Dependency = new ResourceDependency(resource2.Name);

dependencies.AddResource(apiDependency);
dependencies.AddResource(resource1Dependency);
dependencies.AddResource(resource2Dependency);

dependencies.AddDependency(resource2Dependency.Id, resource1Dependency.Id);
dependencies.AddDependency(resource1Dependency.Id, apiDependency.Id);

var order = dependencies.ResolveOrder();

foreach (var item in order)
{
    Console.WriteLine(item.Identifier);
}

var resourceProvisioner = host.Services.GetRequiredService<IResourceProvisioner>();
await resourceProvisioner.StartAsync(paymentApi, deployment, CancellationToken.None);