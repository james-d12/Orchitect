using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Api.Endpoints.Engine.ResourceTemplate;

public sealed class CreateResourceTemplateWithVersionEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/with-version", HandleAsync)
        .RequireOrganisationMember<CreateResourceTemplateWithVersionRequest>(r => r.OrganisationId)
        .WithSummary("Creates a new resource template with the specified version");

    public sealed record CreateResourceTemplateWithVersionResponse(Guid Id);

    private static async Task<Results<Ok<CreateResourceTemplateWithVersionResponse>, BadRequest<string>, InternalServerError>> HandleAsync(
        [FromBody]
        CreateResourceTemplateWithVersionRequest request,
        [FromServices]
        IResourceTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        Orchitect.Domain.Engine.ResourceTemplate.ResourceTemplate resourceTemplate;

        try
        {
            resourceTemplate = Orchitect.Domain.Engine.ResourceTemplate.ResourceTemplate.CreateWithVersion(request);
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(exception.Message);
        }

        var resourceTemplateResponse = await repository.CreateAsync(resourceTemplate, cancellationToken);

        if (resourceTemplateResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new CreateResourceTemplateWithVersionResponse(resourceTemplateResponse.Id.Value));
    }
}