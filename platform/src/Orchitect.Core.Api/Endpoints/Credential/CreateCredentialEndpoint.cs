using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Core.Domain.Credential;
using Orchitect.Core.Domain.Organisation;
using Orchitect.Shared;

namespace Orchitect.Core.Api.Endpoints.Credential;

public sealed class CreateCredentialEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new credential.");

    private static async Task<Results<Ok<CredentialResponse>, InternalServerError>> HandleAsync(
        [FromBody]
        CreateCredentialRequest request,
        [FromServices]
        ICredentialRepository repository,
        [FromServices]
        IEncryptionService encryptionService,
        CancellationToken cancellationToken)
    {
        var organisationId = new OrganisationId(request.OrganisationId);
        var encryptedPayload = encryptionService.Encrypt(request.Payload);

        var credential = Domain.Credential.Credential.Create(
            organisationId,
            request.Name,
            request.Type,
            request.Platform,
            encryptedPayload);

        var result = await repository.CreateAsync(credential, cancellationToken);

        if (result is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(CredentialResponse.From(result));
    }
}
