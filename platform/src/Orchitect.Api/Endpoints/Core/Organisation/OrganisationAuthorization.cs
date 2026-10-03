using Microsoft.AspNetCore.Builder;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

public static class OrganisationAuthorization
{
    public static RouteHandlerBuilder RequireOrganisationAccess(this RouteHandlerBuilder builder)
    {
        return builder.RequireOrganisationAccess<Orchitect.Domain.Core.Organisation.Organisation, OrganisationId>(
            id => new OrganisationId(id),
            (organisation, _, _) => Task.FromResult<OrganisationId?>(organisation.Id));
    }
}