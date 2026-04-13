using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Services;

namespace Orchitect.Api.Endpoints.Inventory.SourceControl;

public sealed class GetRepositoryEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id}", HandleAsync)
        .WithSummary("Gets a repository by id.");

    public sealed record GetRepositoryResponse(
        string Id,
        string Name,
        Uri Url,
        string DefaultBranch,
        string OwnerName,
        RepositoryPlatform Platform,
        DateTime DiscoveredAt,
        DateTime UpdatedAt);

    private static async Task<Results<Ok<GetRepositoryResponse>, NotFound>> HandleAsync(
        [FromRoute]
        string id,
        [FromServices]
        IRepositoryRepository repository,
        CancellationToken cancellationToken)
    {
        var repositoryId = new RepositoryId(id);
        var repo = await repository.GetByIdAsync(repositoryId, cancellationToken);

        if (repo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetRepositoryResponse(
            repo.Id.Value,
            repo.Name,
            repo.Url,
            repo.DefaultBranch,
            repo.User.Name,
            repo.Platform,
            repo.DiscoveredAt,
            repo.UpdatedAt));
    }
}
