using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Requests;
using Orchitect.Domain.Inventory.Cloud.Services;

namespace Orchitect.Api.Endpoints.Inventory.Cloud;

public sealed class GetAllCloudSecretsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Gets all cloud secrets by query parameters.");

    public sealed record GetAllCloudSecretsResponse(
        List<GetCloudSecretEndpoint.GetCloudSecretResponse> CloudSecrets);

    public sealed record CloudSecretQueryRequest(
        Guid OrganisationId,
        string? Name = null,
        string? Location = null,
        string? Url = null,
        CloudSecretPlatform? Platform = null);

    private static Results<Ok<GetAllCloudSecretsResponse>, NotFound> Handle(
        [AsParameters]
        CloudSecretQueryRequest query,
        [FromServices]
        ICloudSecretRepository repository)
    {
        var organisationId = new OrganisationId(query.OrganisationId);

        var cloudSecretQuery = new CloudSecretQuery(
            OrganisationId: organisationId,
            Name: query.Name,
            Location: query.Location,
            Url: query.Url,
            Platform: query.Platform);

        var cloudSecrets = repository.GetByQuery(cloudSecretQuery);

        var response = cloudSecrets
            .Select(cs => new GetCloudSecretEndpoint.GetCloudSecretResponse(
                cs.Id.Value,
                cs.Name,
                cs.Location,
                cs.Platform,
                cs.Url,
                cs.DiscoveredAt,
                cs.UpdatedAt))
            .ToList();

        return TypedResults.Ok(new GetAllCloudSecretsResponse(response));
    }
}
