using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Domain.Core.Credential;
using Orchitect.Shared;

namespace Orchitect.Api.Endpoints.Core.Credential;

public sealed class UpdateCredentialEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id:guid}", HandleAsync)
        .WithSummary("Updates an existing credential.");

    private sealed record UpdateCredentialRequest(
        string Name,
        CredentialType Type,
        CredentialPlatform Platform,
        string Payload);

    private static async Task<Results<Ok<CredentialResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        UpdateCredentialRequest request,
        [FromServices]
        ICredentialRepository repository,
        [FromServices]
        IEncryptionService encryptionService,
        CancellationToken cancellationToken)
    {
        var credentialId = new CredentialId(id);
        var existing = await repository.GetByIdAsync(credentialId, cancellationToken);

        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        var encryptedPayload = encryptionService.Encrypt(request.Payload);
        var updated = existing.Update(request.Name, request.Type, request.Platform, encryptedPayload);
        var result = await repository.UpdateAsync(updated, cancellationToken);

        if (result is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(CredentialResponse.From(result));
    }
}