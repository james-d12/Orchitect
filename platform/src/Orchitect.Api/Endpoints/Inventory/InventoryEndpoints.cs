using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Endpoints.Inventory.Cloud;
using Orchitect.Api.Endpoints.Inventory.Discovery;
using Orchitect.Api.Endpoints.Inventory.Issue;
using Orchitect.Api.Endpoints.Inventory.Pipeline;
using Orchitect.Api.Endpoints.Inventory.SourceControl;
using Orchitect.Api.Shared;

namespace Orchitect.Api.Endpoints.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCloudEndpoints();
        endpoints.MapDiscoveryConfigurationEndpoints();
        endpoints.MapIssueEndpoints();
        endpoints.MapPipelineEndpoints();
        endpoints.MapSourceControlEndpoints();
    }

    private static void MapDiscoveryConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var discoveryGroup = endpoints.MapGroup("/discovery")
            .RequireAuthorization()
            .WithTags("Discovery");

        discoveryGroup.MapEndpoint<CreateDiscoveryConfigurationEndpoint>();
        discoveryGroup.MapEndpoint<ListDiscoveryConfigurationsEndpoint>();
        discoveryGroup.MapEndpoint<UpdateDiscoveryConfigurationEndpoint>();
        discoveryGroup.MapEndpoint<DeleteDiscoveryConfigurationEndpoint>();
        discoveryGroup.MapEndpoint<TriggerDiscoveryEndpoint>();
    }

    private static void MapCloudEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var cloudResourcesGroup = endpoints.MapGroup("/cloud/resources")
            .RequireAuthorization()
            .WithTags("Cloud");

        cloudResourcesGroup.MapEndpoint<GetAllCloudResourcesEndpoint>();
        cloudResourcesGroup.MapEndpoint<GetCloudResourceEndpoint>();

        var cloudSecretsGroup = endpoints.MapGroup("/cloud/secrets")
            .RequireAuthorization()
            .WithTags("Cloud");

        cloudSecretsGroup.MapEndpoint<GetAllCloudSecretsEndpoint>();
        cloudSecretsGroup.MapEndpoint<GetCloudSecretEndpoint>();
    }

    private static void MapIssueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var issuesGroup = endpoints.MapGroup("/issues")
            .RequireAuthorization()
            .WithTags("Issues");

        issuesGroup.MapEndpoint<GetAllIssuesEndpoint>();
        issuesGroup.MapEndpoint<GetIssueEndpoint>();
    }

    private static void MapPipelineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pipelinesGroup = endpoints.MapGroup("/pipelines")
            .RequireAuthorization()
            .WithTags("Pipelines");

        pipelinesGroup.MapEndpoint<GetAllPipelinesEndpoint>();
        pipelinesGroup.MapEndpoint<GetPipelineEndpoint>();
    }

    private static void MapSourceControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var repositoriesGroup = endpoints.MapGroup("/repositories")
            .RequireAuthorization()
            .WithTags("Source Control");

        repositoriesGroup.MapEndpoint<GetAllRepositoriesEndpoint>();
        repositoriesGroup.MapEndpoint<GetRepositoryEndpoint>();

        var pullRequestsGroup = endpoints.MapGroup("/pull-requests")
            .RequireAuthorization()
            .WithTags("Source Control");

        pullRequestsGroup.MapEndpoint<GetAllPullRequestsEndpoint>();
        pullRequestsGroup.MapEndpoint<GetPullRequestEndpoint>();
    }
}
