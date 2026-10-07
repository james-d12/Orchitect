using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

namespace Orchitect.Api.Endpoints.Inventory.Discovery;

public sealed class GetDiscoveryConfigurationEndpoint : IEndpoint
{
    public sealed record GetDiscoveryConfigurationResponse(
        Guid Id,
        Guid OrganisationId,
        Guid CredentialId,
        DiscoveryPlatform Platform,
        bool IsEnabled,
        Dictionary<string, string> PlatformConfig,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DiscoveryRunResponse? LatestRun);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}", HandleAsync)
        .RequireOrganisationAccess<DiscoveryConfiguration, DiscoveryConfigurationId>(
            id => new DiscoveryConfigurationId(id))
        .WithName("GetDiscoveryConfiguration")
        .WithSummary("Gets a discovery configuration and its latest run")
        .Produces<GetDiscoveryConfigurationResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<GetDiscoveryConfigurationResponse>, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IDiscoveryConfigurationRepository configRepository,
        [FromServices]
        IDiscoveryRunRepository runRepository,
        CancellationToken cancellationToken)
    {
        var config = await configRepository.GetByIdAsync(new DiscoveryConfigurationId(id), cancellationToken);

        if (config is null)
        {
            return TypedResults.NotFound();
        }

        var run = await runRepository.GetLatestAsync(config.Id, cancellationToken);

        return TypedResults.Ok(new GetDiscoveryConfigurationResponse(
            config.Id.Value,
            config.OrganisationId.Value,
            config.CredentialId.Value,
            config.Platform,
            config.IsEnabled,
            config.PlatformConfig,
            config.CreatedAt,
            config.UpdatedAt,
            run is null ? null : DiscoveryRunResponse.From(run)));
    }
}
