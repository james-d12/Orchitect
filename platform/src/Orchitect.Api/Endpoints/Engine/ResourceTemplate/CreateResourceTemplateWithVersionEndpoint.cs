using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Api.Endpoints.Engine.ResourceTemplate;

public sealed class CreateResourceTemplateWithVersionEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/with-version", HandleAsync)
        .WithSummary("Creates a new resource template with the specified version");

    private sealed record CreateResourceTemplateWithVersionResponse(Guid Id);

    private static async Task<Results<Ok<CreateResourceTemplateWithVersionResponse>, InternalServerError>> HandleAsync(
        [FromBody]
        CreateResourceTemplateWithVersionRequest request,
        [FromServices]
        IResourceTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var resourceTemplate = Orchitect.Domain.Engine.ResourceTemplate.ResourceTemplate.CreateWithVersion(request);
        var resourceTemplateResponse = await repository.CreateAsync(resourceTemplate, cancellationToken);

        if (resourceTemplateResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new CreateResourceTemplateWithVersionResponse(resourceTemplateResponse.Id.Value));
    }
}