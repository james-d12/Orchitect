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

public sealed class GetAllCloudResourcesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Gets all cloud resources by query parameters.");

    public sealed record GetAllCloudResourcesResponse(
        List<GetCloudResourceEndpoint.GetCloudResourceResponse> CloudResources);

    public sealed record CloudResourceQueryRequest(
        Guid OrganisationId,
        string? Id = null,
        string? Name = null,
        string? Description = null,
        string? Url = null,
        string? Type = null,
        CloudPlatform? Platform = null);

    private static Results<Ok<GetAllCloudResourcesResponse>, NotFound> Handle(
        [AsParameters]
        CloudResourceQueryRequest query,
        [FromServices]
        ICloudResourceRepository repository)
    {
        var organisationId = new OrganisationId(query.OrganisationId);

        var cloudResourceQuery = new CloudResourceQuery(
            OrganisationId: organisationId,
            Name: query.Name,
            Description: query.Description,
            Url: query.Url,
            Platform: query.Platform,
            Type: query.Type);

        var cloudResources = repository.GetByQuery(cloudResourceQuery);

        var organisationsResponse = cloudResources
            .Select(cr => new GetCloudResourceEndpoint.GetCloudResourceResponse(
                cr.Id.Value,
                cr.Name,
                cr.Description,
                cr.Platform,
                cr.Type,
                cr.Url,
                cr.DiscoveredAt,
                cr.UpdatedAt))
            .ToList();

        return TypedResults.Ok(new GetAllCloudResourcesResponse(organisationsResponse));
    }
}