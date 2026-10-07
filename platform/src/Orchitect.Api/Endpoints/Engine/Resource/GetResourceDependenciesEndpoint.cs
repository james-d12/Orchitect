using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;

namespace Orchitect.Api.Endpoints.Engine.Resource;

public sealed class GetResourceDependenciesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}/dependencies", HandleAsync)
        .RequireOrganisationAccess<Orchitect.Domain.Engine.Resource.Resource, ResourceId>(id => new ResourceId(id))
        .WithSummary("Gets the resources a resource depends on and the resources that depend on it.");

    public sealed record GetResourceDependenciesResponse(
        Guid ResourceId,
        List<ResourceResponse> DependsOn,
        List<ResourceResponse> Dependents);

    private static async Task<Results<Ok<GetResourceDependenciesResponse>, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IResourceRepository repository,
        [FromServices]
        IResourceDependencyGraphRepository graphRepository,
        CancellationToken cancellationToken)
    {
        var resource = await repository.GetByIdAsync(new ResourceId(id), cancellationToken);

        if (resource is null)
        {
            return TypedResults.NotFound();
        }

        var graph = await graphRepository.GetByEnvironmentAsync(resource.EnvironmentId, cancellationToken);
        var dependsOn = await GetResourcesAsync(repository, graph?.GetDependencies(resource.Id), cancellationToken);
        var dependents = await GetResourcesAsync(repository, graph?.GetDependents(resource.Id), cancellationToken);

        return TypedResults.Ok(new GetResourceDependenciesResponse(resource.Id.Value, dependsOn, dependents));
    }

    private static async Task<List<ResourceResponse>> GetResourcesAsync(IResourceRepository repository,
        IReadOnlyCollection<ResourceId>? ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        var resources = await repository.GetByIdsAsync(ids, cancellationToken);
        return resources.Select(ResourceResponse.From).ToList();
    }
}
