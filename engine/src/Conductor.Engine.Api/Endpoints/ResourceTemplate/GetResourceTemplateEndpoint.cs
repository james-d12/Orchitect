using Conductor.Engine.Api.Common;
using Conductor.Engine.Domain.ResourceTemplate;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Conductor.Engine.Api.Endpoints.ResourceTemplate;

public sealed class GetResourceTemplateEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}", HandleAsync)
        .WithSummary("Gets an existing resource template by id.");

    public sealed record GetResourceTemplateResponse(
        Guid Id,
        string Name,
        string Type,
        string Description,
        ResourceTemplateProvider Provider,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<ResourceTemplateVersionResponse> Versions);

    public sealed record ResourceTemplateVersionResponse(
        Guid Id,
        string Version,
        ResourceTemplateVersionSourceResponse Source,
        string Notes,
        ResourceTemplateVersionState State,
        DateTime CreatedAt);

    public sealed record ResourceTemplateVersionSourceResponse(
        Uri BaseUrl,
        string FolderPath,
        string Tag);

    private static async Task<Results<Ok<GetResourceTemplateResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IResourceTemplateRepository repository,
        [FromServices]
        ILogger<GetResourceTemplateEndpoint> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var resourceTemplateId = new ResourceTemplateId(id);
            var resourceTemplateResponse = await repository.GetByIdAsync(resourceTemplateId, cancellationToken);

            if (resourceTemplateResponse is null)
            {
                return TypedResults.NotFound();
            }

            var versions = resourceTemplateResponse.Versions.Select(v => new ResourceTemplateVersionResponse(
                Id: v.Id.Value,
                Version: v.Version,
                Source: new ResourceTemplateVersionSourceResponse(
                    BaseUrl: v.Source.BaseUrl,
                    FolderPath: v.Source.FolderPath,
                    Tag: v.Source.Tag),
                Notes: v.Notes,
                State: v.State,
                CreatedAt: v.CreatedAt
            )).ToList();

            var response = new GetResourceTemplateResponse(
                Id: resourceTemplateResponse.Id.Value,
                Name: resourceTemplateResponse.Name,
                Type: resourceTemplateResponse.Type,
                Description: resourceTemplateResponse.Description,
                Provider: resourceTemplateResponse.Provider,
                CreatedAt: resourceTemplateResponse.CreatedAt,
                UpdatedAt: resourceTemplateResponse.UpdatedAt,
                Versions: versions
            );

            return TypedResults.Ok(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not retrieve Resource Template for {Id}.", id);
            return TypedResults.InternalServerError();
        }
    }
}