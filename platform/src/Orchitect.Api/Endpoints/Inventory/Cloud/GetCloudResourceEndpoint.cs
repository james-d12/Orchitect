using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Services;

namespace Orchitect.Api.Endpoints.Inventory.Cloud;

public sealed class GetCloudResourceEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id}", HandleAsync)
        .WithSummary("Gets a cloud resource by id.");

    public sealed record GetCloudResourceResponse(
        string Id,
        string Name,
        string Description,
        CloudPlatform Platform,
        string Type,
        Uri Url,
        DateTime DiscoveredAt,
        DateTime UpdatedAt);

    private static async Task<Results<Ok<GetCloudResourceResponse>, NotFound>> HandleAsync(
        [FromRoute]
        string id,
        [FromServices]
        ICloudResourceRepository repository,
        CancellationToken cancellationToken)
    {
        var cloudResourceId = new CloudResourceId(id);
        var cloudResourceResponse = await repository.GetByIdAsync(cloudResourceId, cancellationToken);

        if (cloudResourceResponse is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetCloudResourceResponse(
            cloudResourceResponse.Id.Value,
            cloudResourceResponse.Name,
            cloudResourceResponse.Description,
            cloudResourceResponse.Platform,
            cloudResourceResponse.Type,
            cloudResourceResponse.Url,
            cloudResourceResponse.DiscoveredAt,
            cloudResourceResponse.UpdatedAt));
    }
}