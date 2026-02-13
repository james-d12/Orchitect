using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Core.Domain.Credential;
using Orchitect.Shared;

namespace Orchitect.Core.Api.Endpoints.Credential;

public sealed class DeleteCredentialEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id:guid}", HandleAsync)
        .WithSummary("Deletes a credential by Id.");

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        ICredentialRepository repository,
        CancellationToken cancellationToken)
    {
        var credentialId = new CredentialId(id);
        var deleted = await repository.DeleteAsync(credentialId, cancellationToken);

        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
