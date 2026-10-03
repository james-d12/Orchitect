using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Api.Endpoints.Engine.Deployment;

public static class DeploymentAuthorization
{
    public static RouteHandlerBuilder RequireDeploymentAccess(this RouteHandlerBuilder builder)
    {
        return builder.RequireOrganisationAccess<Orchitect.Domain.Engine.Deployment.Deployment, DeploymentId>(
            id => new DeploymentId(id),
            ResolveOrganisationIdAsync);
    }

    private static async Task<OrganisationId?> ResolveOrganisationIdAsync(
        Orchitect.Domain.Engine.Deployment.Deployment deployment, IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var application = await services.GetRequiredService<IApplicationRepository>()
            .GetByIdAsync(deployment.ApplicationId, cancellationToken);

        return application?.OrganisationId;
    }
}