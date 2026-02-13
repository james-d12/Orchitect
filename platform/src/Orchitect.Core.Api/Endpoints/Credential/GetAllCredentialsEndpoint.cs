using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Core.Domain.Credential;
using Orchitect.Core.Domain.Organisation;
using Orchitect.Shared;

namespace Orchitect.Core.Api.Endpoints.Credential;

public sealed class GetAllCredentialsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Gets all credentials for an organisation.");

    private sealed record GetAllCredentialsResponse(List<CredentialResponse> Credentials);

    private static Ok<GetAllCredentialsResponse> Handle(
        [FromQuery]
        Guid organisationId,
        [FromServices]
        ICredentialRepository repository)
    {
        var orgId = new OrganisationId(organisationId);
        var credentials = repository.GetAllByOrganisationId(orgId)
            .Select(CredentialResponse.From)
            .ToList();

        return TypedResults.Ok(new GetAllCredentialsResponse(credentials));
    }
}
