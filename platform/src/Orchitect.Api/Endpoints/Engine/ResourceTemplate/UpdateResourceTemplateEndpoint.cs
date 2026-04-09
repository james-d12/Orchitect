using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Api.Endpoints.Engine.ResourceTemplate;

public sealed class UpdateResourceTemplateEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id:guid}", HandleAsync)
        .WithSummary("Updates an existing resource template.");

    public sealed record UpdateResourceTemplateRequest(string Name, string Type, string Description, ResourceTemplateProvider Provider);

    public sealed record UpdateResourceTemplateResponse(Guid Id, Guid OrganisationId, string Name, string Type, string Description, ResourceTemplateProvider Provider, DateTime CreatedAt, DateTime UpdatedAt);

    private static async Task<Results<Ok<UpdateResourceTemplateResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        UpdateResourceTemplateRequest request,
        [FromServices]
        IResourceTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var resourceTemplateId = new ResourceTemplateId(id);
        var existingResourceTemplate = await repository.GetByIdAsync(resourceTemplateId, cancellationToken);

        if (existingResourceTemplate is null)
        {
            return TypedResults.NotFound();
        }

        existingResourceTemplate.Update(request.Name, request.Type, request.Description, request.Provider);
        var resourceTemplateResponse = await repository.UpdateAsync(existingResourceTemplate, cancellationToken);

        if (resourceTemplateResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new UpdateResourceTemplateResponse(
            resourceTemplateResponse.Id.Value,
            resourceTemplateResponse.OrganisationId.Value,
            resourceTemplateResponse.Name,
            resourceTemplateResponse.Type,
            resourceTemplateResponse.Description,
            resourceTemplateResponse.Provider,
            resourceTemplateResponse.CreatedAt,
            resourceTemplateResponse.UpdatedAt));
    }
}
