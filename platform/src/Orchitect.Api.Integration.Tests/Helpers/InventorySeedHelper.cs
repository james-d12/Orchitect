using System.Collections.Immutable;
using AutoFixture;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Services;
using Orchitect.Domain.Inventory.Identity;
using Orchitect.Domain.Inventory.Identity.Services;
using Orchitect.Domain.Inventory.Issue;
using Orchitect.Domain.Inventory.Issue.Services;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.Pipeline.Services;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Services;

namespace Orchitect.Api.Integration.Tests.Helpers;

public static class InventorySeedHelper
{
    private static readonly Fixture Fixture = new();

    public static async Task<User> SeedUserAsync(
        this WebApplicationFactoryWithPostgres factory,
        OrganisationId organisationId,
        UserPlatform? platform = null)
    {
        var user = new User
        {
            Id = new UserId(Fixture.Create<string>()),
            OrganisationId = organisationId,
            Name = Fixture.Create<string>(),
            Description = Fixture.Create<string>(),
            Url = new Uri($"https://example.com/users/{Fixture.Create<string>()}"),
            Platform = platform ?? Fixture.Create<UserPlatform>(),
            DiscoveredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        await repository.UpsertAsync(user);

        return user;
    }

    public static async Task<Team> SeedTeamAsync(
        this WebApplicationFactoryWithPostgres factory,
        OrganisationId organisationId,
        TeamPlatform? platform = null)
    {
        var team = new Team
        {
            Id = new TeamId(Fixture.Create<string>()),
            OrganisationId = organisationId,
            Name = Fixture.Create<string>(),
            Description = Fixture.Create<string>(),
            Url = new Uri($"https://example.com/teams/{Fixture.Create<string>()}"),
            Platform = platform ?? Fixture.Create<TeamPlatform>(),
            DiscoveredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
        await repository.BulkUpsertAsync([team]);

        return team;
    }

    public static async Task<CloudResource> SeedCloudResourceAsync(
        this WebApplicationFactoryWithPostgres factory,
        OrganisationId organisationId,
        CloudPlatform? platform = null)
    {
        var cloudResource = new CloudResource
        {
            Id = new CloudResourceId(Fixture.Create<string>()),
            OrganisationId = organisationId,
            Name = Fixture.Create<string>(),
            Description = Fixture.Create<string>(),
            Url = new Uri($"https://cloud.example.com/{Fixture.Create<string>()}"),
            Type = Fixture.Create<string>(),
            Platform = platform ?? Fixture.Create<CloudPlatform>(),
            DiscoveredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICloudResourceRepository>();
        await repository.BulkUpsertAsync([cloudResource]);

        return cloudResource;
    }

    public static async Task<CloudSecret> SeedCloudSecretAsync(
        this WebApplicationFactoryWithPostgres factory,
        OrganisationId organisationId,
        CloudSecretPlatform? platform = null)
    {
        var cloudSecret = new CloudSecret
        {
            Id = new CloudSecretId(Fixture.Create<string>()),
            OrganisationId = organisationId,
            Name = Fixture.Create<string>(),
            Location = Fixture.Create<string>(),
            Url = new Uri($"https://secrets.example.com/{Fixture.Create<string>()}"),
            Platform = platform ?? Fixture.Create<CloudSecretPlatform>(),
            DiscoveredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICloudSecretRepository>();
        await repository.BulkUpsertAsync([cloudSecret]);

        return cloudSecret;
    }

    public static async Task<Pipeline> SeedPipelineAsync(
        this WebApplicationFactoryWithPostgres factory,
        OrganisationId organisationId,
        User? owner = null,
        PipelinePlatform? platform = null)
    {
        owner ??= await factory.SeedUserAsync(organisationId);

        var pipeline = new Pipeline
        {
            Id = new PipelineId(Fixture.Create<string>()),
            OrganisationId = organisationId,
            Name = Fixture.Create<string>(),
            Url = new Uri($"https://pipelines.example.com/{Fixture.Create<string>()}"),
            User = owner,
            Platform = platform ?? Fixture.Create<PipelinePlatform>(),
            DiscoveredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPipelineRepository>();
        await repository.BulkUpsertAsync([pipeline]);

        return pipeline;
    }

    public static async Task<Repository> SeedRepositoryAsync(
        this WebApplicationFactoryWithPostgres factory,
        OrganisationId organisationId,
        User? owner = null,
        RepositoryPlatform? platform = null)
    {
        owner ??= await factory.SeedUserAsync(organisationId);

        var repository = new Repository
        {
            Id = new RepositoryId(Fixture.Create<string>()),
            OrganisationId = organisationId,
            Name = Fixture.Create<string>(),
            Url = new Uri($"https://github.com/{Fixture.Create<string>()}"),
            DefaultBranch = "main",
            User = owner,
            Platform = platform ?? Fixture.Create<RepositoryPlatform>(),
            DiscoveredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositoryRepository>();
        await repo.BulkUpsertAsync([repository]);

        return repository;
    }

    public static async Task<PullRequest> SeedPullRequestAsync(
        this WebApplicationFactoryWithPostgres factory,
        OrganisationId organisationId,
        Uri? repositoryUrl = null,
        PullRequestPlatform? platform = null)
    {
        repositoryUrl ??= new Uri($"https://github.com/{Fixture.Create<string>()}");

        var pullRequest = new PullRequest
        {
            Id = new PullRequestId(Fixture.Create<string>()),
            OrganisationId = organisationId,
            Name = Fixture.Create<string>(),
            Description = Fixture.Create<string>(),
            Url = new Uri($"https://github.com/pulls/{Fixture.Create<string>()}"),
            Labels = ImmutableHashSet<string>.Empty,
            Reviewers = ImmutableHashSet<string>.Empty,
            Status = PullRequestStatus.Active,
            Platform = platform ?? Fixture.Create<PullRequestPlatform>(),
            LastCommit = null,
            RepositoryUrl = repositoryUrl,
            RepositoryName = Fixture.Create<string>(),
            CreatedOnDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DiscoveredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPullRequestRepository>();
        await repository.BulkUpsertAsync([pullRequest]);

        return pullRequest;
    }

    public static async Task<Issue> SeedIssueAsync(
        this WebApplicationFactoryWithPostgres factory,
        OrganisationId organisationId,
        IssuePlatform? platform = null)
    {
        var issue = new Issue
        {
            Id = new IssueId(Fixture.Create<string>()),
            OrganisationId = organisationId,
            Title = Fixture.Create<string>(),
            Description = Fixture.Create<string>(),
            Url = new Uri($"https://issues.example.com/{Fixture.Create<string>()}"),
            Type = Fixture.Create<string>(),
            State = Fixture.Create<string>(),
            Platform = platform ?? Fixture.Create<IssuePlatform>(),
            DiscoveredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIssueRepository>();
        await repository.BulkUpsertAsync([issue]);

        return issue;
    }
}
