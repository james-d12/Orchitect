using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Credential;

namespace Orchitect.Api.Endpoints.Core.Credential;

public sealed class GetCredentialEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}", HandleAsync)
        .WithSummary("Gets a credential by Id.");

    private static async Task<Results<Ok<CredentialResponse>, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        ICredentialRepository repository,
        CancellationToken cancellationToken)
    {
        var credentialId = new CredentialId(id);
        var credential = await repository.GetByIdAsync(credentialId, cancellationToken);

        if (credential is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(CredentialResponse.From(credential));
    }
}