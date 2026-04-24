using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Requests;
using Orchitect.Domain.Inventory.SourceControl.Services;

namespace Orchitect.Api.Endpoints.Inventory.SourceControl;

public sealed class GetAllRepositoriesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Gets all repositories by query parameters.");

    public sealed record GetAllRepositoriesResponse(
        List<GetRepositoryEndpoint.GetRepositoryResponse> Repositories);

    public sealed record RepositoryQueryRequest(
        Guid OrganisationId,
        string? Id = null,
        string? Name = null,
        string? Url = null,
        string? DefaultBranch = null,
        string? OwnerName = null,
        RepositoryPlatform? Platform = null);

    private static Results<Ok<GetAllRepositoriesResponse>, NotFound> Handle(
        [AsParameters]
        RepositoryQueryRequest query,
        [FromServices]
        IRepositoryRepository repository)
    {
        var organisationId = new OrganisationId(query.OrganisationId);

        var repositoryQuery = new RepositoryQuery(
            OrganisationId: organisationId,
            Id: query.Id,
            Name: query.Name,
            Url: query.Url,
            DefaultBranch: query.DefaultBranch,
            OwnerName: query.OwnerName,
            Platform: query.Platform);

        var repositories = repository.GetByQuery(repositoryQuery);

        var response = repositories
            .Select(r => new GetRepositoryEndpoint.GetRepositoryResponse(
                r.Id.Value,
                r.Name,
                r.Url,
                r.DefaultBranch,
                r.User.Name,
                r.Platform,
                r.DiscoveredAt,
                r.UpdatedAt))
            .ToList();

        return TypedResults.Ok(new GetAllRepositoriesResponse(response));
    }
}
