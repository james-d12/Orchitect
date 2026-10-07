using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceInstance;

namespace Orchitect.Api.Endpoints.Engine.Resource;

public sealed class GetResourceEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}", HandleAsync)
        .RequireOrganisationAccess<Orchitect.Domain.Engine.Resource.Resource, ResourceId>(id => new ResourceId(id))
        .WithSummary("Gets a resource and the status of its instances by Id.");

    public sealed record GetResourceResponse(ResourceResponse Resource, List<ResourceInstanceResponse> Instances);

    public sealed record ResourceInstanceResponse(
        Guid Id,
        string Name,
        Guid TemplateVersionId,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private static async Task<Results<Ok<GetResourceResponse>, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IResourceRepository repository,
        [FromServices]
        IResourceInstanceRepository instanceRepository,
        CancellationToken cancellationToken)
    {
        var resource = await repository.GetByIdAsync(new ResourceId(id), cancellationToken);

        if (resource is null)
        {
            return TypedResults.NotFound();
        }

        var instances = await instanceRepository.GetByResourceAsync(resource.Id, cancellationToken);

        return TypedResults.Ok(new GetResourceResponse(
            ResourceResponse.From(resource),
            instances
                .OrderBy(i => i.CreatedAt)
                .Select(i => new ResourceInstanceResponse(
                    i.Id.Value,
                    i.Name,
                    i.TemplateVersionId.Value,
                    i.Status.ToString(),
                    i.CreatedAt,
                    i.UpdatedAt))
                .ToList()));
    }
}
