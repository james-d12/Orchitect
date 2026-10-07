using System.Net;
using System.Text.Json;
using AutoFixture;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Api.Endpoints.Engine.Environment;
using Orchitect.Api.Endpoints.Engine.Resource;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Api.Integration.Tests;

public sealed class ResourceIntegrationTests(WebApplicationFactoryWithPostgres factory) : IClassFixture<WebApplicationFactoryWithPostgres>
{
    private const string ResourcesUrl = "/resources";
    private readonly Fixture _fixture = new();

    private sealed record Scope(
        OrganisationId OrganisationId,
        EnvironmentId EnvironmentId,
        ResourceTemplate Template);

    [Fact]
    public async Task ResourceApi_WhenGettingResourceById_ShouldReturn200OkWithInstances()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var scope = await CreateScopeAsync(client);
        var application = await client.CreateApplicationAsync(scope.OrganisationId.Value);
        var resource = await SeedResourceAsync(scope, "orders-db", new ApplicationId(application.Id));
        var instance = await SeedInstanceAsync(scope, resource);

        // Act
        var response = await client.GetAsync($"{ResourcesUrl}/{resource.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetResourceEndpoint.GetResourceResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(resource.Id.Value, body.Resource.Id);
        Assert.Equal(scope.OrganisationId.Value, body.Resource.OrganisationId);
        Assert.Equal("orders-db", body.Resource.Name);
        Assert.Equal(resource.Slug, body.Resource.Slug);
        Assert.Equal(nameof(ResourceKind.Direct), body.Resource.Kind);
        Assert.Equal(scope.Template.Id.Value, body.Resource.ResourceTemplateId);
        Assert.Equal(scope.EnvironmentId.Value, body.Resource.EnvironmentId);
        Assert.Equal(application.Id, body.Resource.ApplicationId);
        Assert.Equal([application.Id], body.Resource.Consumers);
        var instanceResponse = Assert.Single(body.Instances);
        Assert.Equal(instance.Id.Value, instanceResponse.Id);
        Assert.Equal(instance.TemplateVersionId.Value, instanceResponse.TemplateVersionId);
        Assert.Equal(nameof(ResourceInstanceStatus.Pending), instanceResponse.Status);
    }

    [Fact]
    public async Task ResourceApi_WhenGettingResourceById_ShouldNotReturnInputParametersOrOutputs()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var scope = await CreateScopeAsync(client);
        var resource = await SeedResourceAsync(scope, "orders-db");
        await SeedInstanceAsync(scope, resource);

        // Act
        var response = await client.GetAsync($"{ResourcesUrl}/{resource.Id.Value}");
        var json = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("super-secret", json);
        Assert.DoesNotContain("inputParameters", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("output", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResourceApi_WhenGettingResourceByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{ResourcesUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResourceApi_WhenNotAMember_ShouldReturn404NotFound()
    {
        // Arrange
        var member = await factory.CreateClient().AddAuthorisationHeader();
        var outsider = await factory.CreateClient().AddAuthorisationHeaderForNewUser();
        var scope = await CreateScopeAsync(member);
        var resource = await SeedResourceAsync(scope, "orders-db");

        // Act
        var get = await outsider.GetAsync($"{ResourcesUrl}/{resource.Id.Value}");
        var dependencies = await outsider.GetAsync($"{ResourcesUrl}/{resource.Id.Value}/dependencies");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, dependencies.StatusCode);
    }

    [Fact]
    public async Task ResourceApi_WhenGettingAllResources_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var scope = await CreateScopeAsync(client);
        var resource = await SeedResourceAsync(scope, "orders-db");

        // Act
        var response = await client.GetAsync(ResourcesUrl);
        var body = await response.ReadFromJsonAsync<GetAllResourcesEndpoint.GetAllResourcesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body.Resources, r => r.Id == resource.Id.Value);
    }

    [Fact]
    public async Task ResourceApi_WhenGettingAllResources_ShouldFilterByEnvironment()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var scope = await CreateScopeAsync(client);
        var otherScope = await CreateScopeAsync(client);
        var resource = await SeedResourceAsync(scope, "orders-db");
        await SeedResourceAsync(otherScope, "orders-db");

        // Act
        var response = await client.GetAsync($"{ResourcesUrl}?environmentId={scope.EnvironmentId.Value}");
        var body = await response.ReadFromJsonAsync<GetAllResourcesEndpoint.GetAllResourcesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(resource.Id.Value, Assert.Single(body.Resources).Id);
    }

    [Fact]
    public async Task ResourceApi_WhenGettingAllResources_ShouldNotReturnResourcesFromOtherOrganisations()
    {
        // Arrange
        var member = await factory.CreateClient().AddAuthorisationHeader();
        var outsider = await factory.CreateClient().AddAuthorisationHeaderForNewUser();
        var scope = await CreateScopeAsync(member);
        await SeedResourceAsync(scope, "orders-db");

        // Act
        var all = await outsider.GetAsync(ResourcesUrl);
        var filtered = await outsider.GetAsync($"{ResourcesUrl}?environmentId={scope.EnvironmentId.Value}");
        var allBody = await all.ReadFromJsonAsync<GetAllResourcesEndpoint.GetAllResourcesResponse>();
        var filteredBody = await filtered.ReadFromJsonAsync<GetAllResourcesEndpoint.GetAllResourcesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.NotNull(allBody);
        Assert.NotNull(filteredBody);
        Assert.Empty(allBody.Resources);
        Assert.Empty(filteredBody.Resources);
    }

    [Fact]
    public async Task ResourceApi_WhenGettingDependencies_ShouldReturnDirectDependenciesAndDependents()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var scope = await CreateScopeAsync(client);
        var vault = await SeedResourceAsync(scope, "vault");
        var storage = await SeedResourceAsync(scope, "storage");
        var database = await SeedResourceAsync(scope, "database");
        await SeedGraphAsync(scope, graph =>
        {
            graph.AddDependency(storage.Id, vault.Id);
            graph.AddDependency(database.Id, storage.Id);
        }, vault, storage, database);

        // Act
        var response = await client.GetAsync($"{ResourcesUrl}/{storage.Id.Value}/dependencies");
        var body = await response.ReadFromJsonAsync<GetResourceDependenciesEndpoint.GetResourceDependenciesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(storage.Id.Value, body.ResourceId);
        Assert.Equal(vault.Id.Value, Assert.Single(body.DependsOn).Id);
        Assert.Equal(database.Id.Value, Assert.Single(body.Dependents).Id);
    }

    [Fact]
    public async Task ResourceApi_WhenGettingDependenciesWithoutAGraph_ShouldReturnEmptyLists()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var scope = await CreateScopeAsync(client);
        var resource = await SeedResourceAsync(scope, "orders-db");

        // Act
        var response = await client.GetAsync($"{ResourcesUrl}/{resource.Id.Value}/dependencies");
        var body = await response.ReadFromJsonAsync<GetResourceDependenciesEndpoint.GetResourceDependenciesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Empty(body.DependsOn);
        Assert.Empty(body.Dependents);
    }

    [Fact]
    public async Task ResourceApi_WhenGettingDependenciesOfNonExistentResource_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{ResourcesUrl}/{Guid.NewGuid()}/dependencies");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Scope> CreateScopeAsync(HttpClient client)
    {
        var organisation = await client.CreateOrganisationAsync();
        var organisationId = new OrganisationId(organisation.Id);
        var environmentResponse = await client.PostAsJsonAsync("/environments",
            new CreateEnvironmentRequest(_fixture.Create<string>(), _fixture.Create<string>(), organisationId));
        var environment =
            await environmentResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        ArgumentNullException.ThrowIfNull(environment);

        using var serviceScope = factory.Services.CreateScope();
        var type = $"azure-postgres-{Guid.NewGuid():N}";
        var template = await serviceScope.ServiceProvider.GetRequiredService<IResourceTemplateRepository>()
                           .CreateAsync(ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
                           {
                               OrganisationId = organisationId,
                               Name = type,
                               Type = type,
                               Description = $"A {type}.",
                               Provider = ResourceTemplateProvider.Terraform,
                               Version = "1.0.0",
                               Source = new ResourceTemplateVersionSource
                               {
                                   BaseUrl = new Uri("https://example.com/modules.git"),
                                   FolderPath = type,
                                   Tag = "v1.0.0"
                               },
                               Notes = "Initial version.",
                               State = ResourceTemplateVersionState.Active
                           }))
                       ?? throw new InvalidOperationException("The resource template was not created.");

        return new Scope(organisationId, new EnvironmentId(environment.Id), template);
    }

    private async Task<Resource> SeedResourceAsync(Scope scope, string name, ApplicationId? applicationId = null)
    {
        var resource = Resource.Create(new CreateResourceRequest(scope.OrganisationId, name,
            _fixture.Create<string>(), scope.Template.Id, scope.EnvironmentId, ResourceKind.Direct, applicationId));

        if (applicationId is not null)
        {
            resource.AddConsumer(applicationId.Value);
        }

        using var serviceScope = factory.Services.CreateScope();
        return await serviceScope.ServiceProvider.GetRequiredService<IResourceRepository>().CreateAsync(resource)
               ?? throw new InvalidOperationException("The resource was not created.");
    }

    private async Task<ResourceInstance> SeedInstanceAsync(Scope scope, Resource resource)
    {
        var instance = ResourceInstance.Create(new CreateResourceInstanceRequest(resource.Id, scope.OrganisationId,
            resource.Slug, scope.Template.Versions.Single().Id, scope.EnvironmentId,
            new Dictionary<string, JsonElement>
            {
                ["password"] = JsonSerializer.SerializeToElement("super-secret")
            }));

        using var serviceScope = factory.Services.CreateScope();
        return await serviceScope.ServiceProvider.GetRequiredService<IResourceInstanceRepository>()
                   .CreateAsync(instance)
               ?? throw new InvalidOperationException("The resource instance was not created.");
    }

    private async Task SeedGraphAsync(Scope scope, Action<ResourceDependencyGraph> addDependencies,
        params Resource[] resources)
    {
        var graph = ResourceDependencyGraph.Create(scope.OrganisationId, scope.EnvironmentId);

        foreach (var resource in resources)
        {
            graph.AddResource(resource.Id);
        }

        addDependencies(graph);

        using var serviceScope = factory.Services.CreateScope();
        await serviceScope.ServiceProvider.GetRequiredService<IResourceDependencyGraphRepository>()
            .CreateAsync(graph);
    }
}
