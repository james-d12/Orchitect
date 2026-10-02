using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AutoFixture;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Api.Endpoints.Core.Credential;
using Orchitect.Api.Endpoints.Core.Organisation;
using Orchitect.Api.Endpoints.Engine.Application;
using Orchitect.Api.Endpoints.Engine.Environment;
using Orchitect.Api.Endpoints.Engine.ResourceTemplate;
using Orchitect.Api.Endpoints.Inventory.Discovery;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Domain.Inventory.Discovery;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class OrganisationMembershipIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private readonly Fixture _fixture = new();

    private static readonly JsonElement SamplePayload =
        JsonDocument.Parse("""{"token":"test-token-value"}""").RootElement;

    private async Task<(HttpClient Member, HttpClient Outsider, Guid OrganisationId)> CreateOrganisationWithOutsiderAsync()
    {
        var member = await factory.CreateClient().AddAuthorisationHeader();
        var outsider = await factory.CreateClient().AddAuthorisationHeaderForNewUser();
        var organisation = await member.CreateOrganisationAsync();
        return (member, outsider, organisation.Id);
    }

    private async Task<CredentialResponse> CreateCredentialAsync(HttpClient client, Guid organisationId)
    {
        var response = await client.PostAsJsonAsync("/credentials", new CreateCredentialEndpoint.CreateCredentialRequest(
            _fixture.Create<string>(), organisationId, CredentialType.PersonalAccessToken, CredentialPlatform.GitHub,
            SamplePayload));
        var credential = await response.ReadFromJsonAsync<CredentialResponse>();
        ArgumentNullException.ThrowIfNull(credential);
        return credential;
    }

    private async Task<Guid> CreateEnvironmentAsync(HttpClient client, Guid organisationId)
    {
        var response = await client.PostAsJsonAsync("/environments", new CreateEnvironmentRequest(
            _fixture.Create<string>(), _fixture.Create<string>(), new OrganisationId(organisationId)));
        var environment = await response.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        ArgumentNullException.ThrowIfNull(environment);
        return environment.Id;
    }

    private CreateResourceTemplateRequest BuildResourceTemplateRequest(Guid organisationId) =>
        new()
        {
            OrganisationId = new OrganisationId(organisationId),
            Name = _fixture.Create<string>(),
            Type = _fixture.Create<string>(),
            Description = _fixture.Create<string>(),
            Provider = _fixture.Create<ResourceTemplateProvider>()
        };

    private async Task<Guid> CreateResourceTemplateAsync(HttpClient client, Guid organisationId)
    {
        var response = await client.PostAsJsonAsync("/resource-templates", BuildResourceTemplateRequest(organisationId));
        var template = await response.ReadFromJsonAsync<CreateResourceTemplateEndpoint.CreateResourceTemplateResponse>();
        ArgumentNullException.ThrowIfNull(template);
        return template.Id;
    }

    private async Task<Deployment> CreateDeploymentAsync(HttpClient client, Guid organisationId)
    {
        var application = await client.CreateApplicationAsync(organisationId);
        var environmentId = await CreateEnvironmentAsync(client, organisationId);

        using var scope = factory.Services.CreateScope();
        var deployment = await scope.ServiceProvider.GetRequiredService<IDeploymentRepository>().CreateAsync(
            Deployment.Create(new ApplicationId(application.Id), new EnvironmentId(environmentId), NewCommitId()));
        ArgumentNullException.ThrowIfNull(deployment);
        return deployment;
    }

    private static CommitId NewCommitId() => new(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20)));

    [Fact]
    public async Task OrganisationApi_WhenCreatingOrganisation_ShouldMakeCreatorAMember()
    {
        var (member, _, organisationId) = await CreateOrganisationWithOutsiderAsync();

        var response = await member.GetAsync($"/organisations/{organisationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrganisationApi_WhenNotAMember_ShouldReturn404NotFound()
    {
        var (_, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();

        var get = await outsider.GetAsync($"/organisations/{organisationId}");
        var update = await outsider.PutAsJsonAsync($"/organisations/{organisationId}",
            new UpdateOrganisationEndpoint.UpdateOrganisationRequest(_fixture.Create<string>()));
        var delete = await outsider.DeleteAsync($"/organisations/{organisationId}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task OrganisationApi_WhenGettingAllOrganisations_ShouldOnlyReturnMemberOrganisations()
    {
        var (member, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();
        var outsiderOrganisation = await outsider.CreateOrganisationAsync();

        var response = await member.GetAsync("/organisations");
        var body = await response.ReadFromJsonAsync<GetAllOrganisationsEndpoint.GetAllOrganisationsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body.Organisations, o => o.Id == organisationId);
        Assert.DoesNotContain(body.Organisations, o => o.Id == outsiderOrganisation.Id);
    }

    [Theory]
    [InlineData("/credentials")]
    [InlineData("/discovery")]
    [InlineData("/cloud/resources")]
    [InlineData("/cloud/secrets")]
    [InlineData("/issues")]
    [InlineData("/pipelines")]
    [InlineData("/repositories")]
    [InlineData("/pull-requests")]
    public async Task OrganisationScopedListApi_WhenNotAMember_ShouldReturn403Forbidden(string url)
    {
        var (_, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();

        var response = await outsider.GetAsync($"{url}?organisationId={organisationId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateApi_WhenNotAMember_ShouldReturn403Forbidden()
    {
        var (_, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();

        var credential = await outsider.PostAsJsonAsync("/credentials", new CreateCredentialEndpoint.CreateCredentialRequest(
            _fixture.Create<string>(), organisationId, CredentialType.PersonalAccessToken, CredentialPlatform.GitHub,
            SamplePayload));
        var application = await outsider.PostAsJsonAsync("/applications", new Domain.Engine.Application.CreateApplicationRequest(
            _fixture.Create<string>(), organisationId.ToString(),
            new Domain.Engine.Application.CreateRepositoryRequest(_fixture.Create<string>(),
                new Uri("https://github.com/test/repo"), Domain.Engine.Application.RepositoryProvider.GitHub)));
        var environment = await outsider.PostAsJsonAsync("/environments", new CreateEnvironmentRequest(
            _fixture.Create<string>(), _fixture.Create<string>(), new OrganisationId(organisationId)));
        var template = await outsider.PostAsJsonAsync("/resource-templates", BuildResourceTemplateRequest(organisationId));

        Assert.Equal(HttpStatusCode.Forbidden, credential.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, application.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, environment.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, template.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenNotAMember_ShouldReturn403Forbidden()
    {
        var (member, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();
        var credential = await CreateCredentialAsync(member, organisationId);
        var createRequest = new CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationRequest(
            organisationId.ToString(), credential.Id, DiscoveryPlatform.GitHub, true, null);
        var created = await member.PostAsJsonAsync("/discovery", createRequest);
        var configuration =
            await created.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        ArgumentNullException.ThrowIfNull(configuration);
        var configurationId = configuration.Id.Value;

        var create = await outsider.PostAsJsonAsync("/discovery", createRequest);
        var update = await outsider.PutAsJsonAsync($"/discovery/{configurationId}",
            new UpdateDiscoveryConfigurationEndpoint.UpdateDiscoveryConfigurationRequest(
                organisationId.ToString(), false, null));
        var trigger = await outsider.PostAsync($"/discovery/{configurationId}/trigger?organisationId={organisationId}",
            null);
        var delete = await outsider.DeleteAsync($"/discovery/{configurationId}?organisationId={organisationId}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, trigger.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task OrganisationScopedApi_WhenOrganisationIdIsInvalid_ShouldReturn400BadRequest()
    {
        var client = await factory.CreateClient().AddAuthorisationHeader();

        var response = await client.GetAsync("/discovery?organisationId=not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CredentialApi_WhenNotAMember_ShouldReturn404NotFound()
    {
        var (member, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();
        var credential = await CreateCredentialAsync(member, organisationId);

        var get = await outsider.GetAsync($"/credentials/{credential.Id}");
        var update = await outsider.PutAsJsonAsync($"/credentials/{credential.Id}",
            new UpdateCredentialEndpoint.UpdateCredentialRequest(_fixture.Create<string>(),
                CredentialType.PersonalAccessToken, CredentialPlatform.GitHub, SamplePayload));
        var delete = await outsider.DeleteAsync($"/credentials/{credential.Id}");
        var stillThere = await member.GetAsync($"/credentials/{credential.Id}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task ApplicationApi_WhenNotAMember_ShouldHideApplication()
    {
        var (member, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();
        var application = await member.CreateApplicationAsync(organisationId);

        var get = await outsider.GetAsync($"/applications/{application.Id}");
        var update = await outsider.PutAsJsonAsync($"/applications/{application.Id}",
            new UpdateApplicationEndpoint.UpdateApplicationRequest(_fixture.Create<string>(),
                new UpdateApplicationEndpoint.UpdateRepositoryRequest(_fixture.Create<string>(),
                    new Uri("https://github.com/test/repo"), Domain.Engine.Application.RepositoryProvider.GitHub)));
        var delete = await outsider.DeleteAsync($"/applications/{application.Id}");
        var all = await outsider.GetAsync("/applications");
        var allBody = await all.ReadFromJsonAsync<GetAllApplicationsEndpoint.GetAllApplicationsResponse>();

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.NotNull(allBody);
        Assert.DoesNotContain(allBody.Applications, a => a.Id == application.Id);
    }

    [Fact]
    public async Task EnvironmentApi_WhenNotAMember_ShouldHideEnvironment()
    {
        var (member, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();
        var environmentId = await CreateEnvironmentAsync(member, organisationId);

        var get = await outsider.GetAsync($"/environments/{environmentId}");
        var update = await outsider.PutAsJsonAsync($"/environments/{environmentId}",
            new UpdateEnvironmentEndpoint.UpdateEnvironmentRequest(_fixture.Create<string>(), _fixture.Create<string>()));
        var delete = await outsider.DeleteAsync($"/environments/{environmentId}");
        var all = await outsider.GetAsync("/environments");
        var allBody = await all.ReadFromJsonAsync<GetAllEnvironmentsEndpoint.GetAllEnvironmentsResponse>();

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.NotNull(allBody);
        Assert.DoesNotContain(allBody.Environments, e => e.Id == environmentId);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenNotAMember_ShouldHideResourceTemplate()
    {
        var (member, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();
        var templateId = await CreateResourceTemplateAsync(member, organisationId);

        var get = await outsider.GetAsync($"/resource-templates/{templateId}");
        var update = await outsider.PutAsJsonAsync($"/resource-templates/{templateId}",
            new UpdateResourceTemplateEndpoint.UpdateResourceTemplateRequest(_fixture.Create<string>(),
                _fixture.Create<string>(), _fixture.Create<string>(), _fixture.Create<ResourceTemplateProvider>()));
        var delete = await outsider.DeleteAsync($"/resource-templates/{templateId}");
        var all = await outsider.GetAsync("/resource-templates");
        var allBody = await all.ReadFromJsonAsync<GetAllResourceTemplatesEndpoint.GetAllResourceTemplatesResponse>();

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.NotNull(allBody);
        Assert.DoesNotContain(allBody.ResourceTemplates, t => t.Id == templateId);
    }

    [Fact]
    public async Task DeploymentApi_WhenNotAMember_ShouldHideDeployment()
    {
        var (member, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();
        var deployment = await CreateDeploymentAsync(member, organisationId);

        var get = await outsider.GetAsync($"/deployments/{deployment.Id.Value}");
        var destroy = await outsider.DeleteAsync($"/deployments/{deployment.Id.Value}");
        var create = await outsider.PostAsJsonAsync("/deployments",
            new CreateDeploymentRequest(deployment.ApplicationId, deployment.EnvironmentId, NewCommitId()));

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, destroy.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task InventoryApi_WhenNotAMember_ShouldReturn404NotFound()
    {
        var (_, outsider, organisationId) = await CreateOrganisationWithOutsiderAsync();
        var id = new OrganisationId(organisationId);
        var cloudResource = await factory.SeedCloudResourceAsync(id);
        var cloudSecret = await factory.SeedCloudSecretAsync(id);
        var issue = await factory.SeedIssueAsync(id);
        var repository = await factory.SeedRepositoryAsync(id);
        var pullRequest = await factory.SeedPullRequestAsync(id);

        var responses = new[]
        {
            await outsider.GetAsync($"/cloud/resources/{cloudResource.Id.Value}"),
            await outsider.GetAsync($"/cloud/secrets/{cloudSecret.Id.Value}"),
            await outsider.GetAsync($"/issues/{issue.Id.Value}"),
            await outsider.GetAsync($"/repositories/{repository.Id.Value}"),
            await outsider.GetAsync($"/pull-requests/{pullRequest.Id.Value}")
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }
}