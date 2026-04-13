using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Services;

namespace Orchitect.Api.Endpoints.Inventory.Cloud;

public sealed class GetCloudSecretEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id}", HandleAsync)
        .WithSummary("Gets a cloud secret by id.");

    public sealed record GetCloudSecretResponse(
        string Id,
        string Name,
        string Location,
        CloudSecretPlatform Platform,
        Uri Url,
        DateTime DiscoveredAt,
        DateTime UpdatedAt);

    private static async Task<Results<Ok<GetCloudSecretResponse>, NotFound>> HandleAsync(
        [FromRoute]
        string id,
        [FromServices]
        ICloudSecretRepository repository,
        CancellationToken cancellationToken)
    {
        var cloudSecretId = new CloudSecretId(id);
        var cloudSecret = await repository.GetByIdAsync(cloudSecretId, cancellationToken);

        if (cloudSecret is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetCloudSecretResponse(
            cloudSecret.Id.Value,
            cloudSecret.Name,
            cloudSecret.Location,
            cloudSecret.Platform,
            cloudSecret.Url,
            cloudSecret.DiscoveredAt,
            cloudSecret.UpdatedAt));
    }
}
