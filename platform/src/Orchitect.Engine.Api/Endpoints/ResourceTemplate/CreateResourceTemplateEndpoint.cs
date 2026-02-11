using Orchitect.Engine.Domain.ResourceTemplate;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Shared;

namespace Orchitect.Engine.Api.Endpoints.ResourceTemplate;

public sealed class CreateResourceTemplateEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new resource template.");

    private sealed record CreateResourceTemplateResponse(Guid Id);

    private static async Task<Results<Ok<CreateResourceTemplateResponse>, InternalServerError>> HandleAsync(
        [FromBody]
        CreateResourceTemplateRequest request,
        [FromServices]
        IResourceTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var resourceTemplate = Orchitect.Engine.Domain.ResourceTemplate.ResourceTemplate.Create(request);
        var resourceTemplateResponse = await repository.CreateAsync(resourceTemplate, cancellationToken);

        if (resourceTemplateResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new CreateResourceTemplateResponse(resourceTemplateResponse.Id.Value));
    }
}