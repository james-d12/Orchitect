using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Shared.Authorization;

public static class OrganisationAuthorizationExtensions
{
    private const string OrganisationIdParameterName = "organisationId";
    private const string IdParameterName = "id";

    public static RouteHandlerBuilder RequireOrganisationMember(this RouteHandlerBuilder builder,
        string parameterName = OrganisationIdParameterName)
    {
        return builder.RequireOrganisationMember<Guid>(parameterName, id => new OrganisationId(id));
    }

    public static RouteHandlerBuilder RequireOrganisationMember<TRequest>(this RouteHandlerBuilder builder,
        Func<TRequest, Guid> select)
    {
        return builder.RequireOrganisationMember<TRequest>(null, request => new OrganisationId(select(request)));
    }

    public static RouteHandlerBuilder RequireOrganisationMember<TRequest>(this RouteHandlerBuilder builder,
        Func<TRequest, string> select)
    {
        return builder.RequireOrganisationMember<TRequest>(null,
            request => Guid.TryParse(select(request), out var id) ? new OrganisationId(id) : null);
    }

    public static RouteHandlerBuilder RequireOrganisationMember<TRequest>(this RouteHandlerBuilder builder,
        Func<TRequest, OrganisationId> select)
    {
        return builder.RequireOrganisationMember<TRequest>(null, request => select(request));
    }

    public static RouteHandlerBuilder RequireOrganisationAccess<TEntity, TId>(this RouteHandlerBuilder builder,
        Func<Guid, TId> toId)
        where TEntity : class, IEntity
    {
        return builder.RequireOrganisationAccess<TEntity, TId>(toId, ResolveEntityOrganisationAsync);
    }

    public static RouteHandlerBuilder RequireOrganisationAccess<TEntity, TId>(this RouteHandlerBuilder builder,
        Func<string, TId> toId)
        where TEntity : class, IEntity
    {
        return builder.RequireOrganisationAccess<TEntity, TId>(toId, ResolveEntityOrganisationAsync);
    }

    public static RouteHandlerBuilder RequireOrganisationAccess<TEntity, TId>(this RouteHandlerBuilder builder,
        Func<Guid, TId> toId,
        Func<TEntity, IServiceProvider, CancellationToken, Task<OrganisationId?>> resolveOrganisationId)
        where TEntity : class
    {
        return builder.RequireOrganisationAccess<TEntity, TId, Guid>(toId, resolveOrganisationId);
    }

    public static RouteHandlerBuilder HandlesOrganisationScope(this RouteHandlerBuilder builder)
    {
        return builder.WithMetadata(new HandlesOrganisationScopeMetadata());
    }

    private static RouteHandlerBuilder RequireOrganisationMember<TArgument>(this RouteHandlerBuilder builder,
        string? parameterName, Func<TArgument, OrganisationId?> select)
    {
        builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            var index = GetParameterIndex<TArgument>(factoryContext, parameterName);

            return async context =>
            {
                var organisationId = select(context.GetArgument<TArgument>(index));

                if (organisationId is null)
                {
                    return TypedResults.BadRequest(new ErrorResponse
                    {
                        Errors = [new Error { Code = "INVALID_ORGANISATION_ID", Message = "Organisation id is not valid." }]
                    });
                }

                var access = context.HttpContext.RequestServices.GetRequiredService<IOrganisationAccess>();

                if (!await access.IsMemberAsync(organisationId.Value, context.HttpContext.RequestAborted))
                {
                    return TypedResults.Forbid();
                }

                return await next(context);
            };
        });

        return builder.WithMetadata(new OrganisationScopedMetadata());
    }

    private static RouteHandlerBuilder RequireOrganisationAccess<TEntity, TId, TArgument>(
        this RouteHandlerBuilder builder,
        Func<TArgument, TId> toId,
        Func<TEntity, IServiceProvider, CancellationToken, Task<OrganisationId?>> resolveOrganisationId)
        where TEntity : class
    {
        builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            var index = GetParameterIndex<TArgument>(factoryContext, IdParameterName);
            EnsureServiceIsRegistered<IRepository<TEntity, TId>>(factoryContext);

            return async context =>
            {
                var services = context.HttpContext.RequestServices;
                var cancellationToken = context.HttpContext.RequestAborted;
                var repository = services.GetRequiredService<IRepository<TEntity, TId>>();
                var entity = await repository.GetByIdAsync(toId(context.GetArgument<TArgument>(index)),
                    cancellationToken);

                if (entity is null)
                {
                    return TypedResults.NotFound();
                }

                var organisationId = await resolveOrganisationId(entity, services, cancellationToken);
                var access = services.GetRequiredService<IOrganisationAccess>();

                if (organisationId is null || !await access.IsMemberAsync(organisationId.Value, cancellationToken))
                {
                    return TypedResults.NotFound();
                }

                return await next(context);
            };
        });

        return builder.WithMetadata(new OrganisationScopedMetadata());
    }

    private static RouteHandlerBuilder RequireOrganisationAccess<TEntity, TId>(this RouteHandlerBuilder builder,
        Func<string, TId> toId,
        Func<TEntity, IServiceProvider, CancellationToken, Task<OrganisationId?>> resolveOrganisationId)
        where TEntity : class
    {
        return builder.RequireOrganisationAccess<TEntity, TId, string>(toId, resolveOrganisationId);
    }

    private static Task<OrganisationId?> ResolveEntityOrganisationAsync<TEntity>(TEntity entity,
        IServiceProvider services, CancellationToken cancellationToken)
        where TEntity : IEntity
    {
        return Task.FromResult<OrganisationId?>(entity.OrganisationId);
    }

    private static void EnsureServiceIsRegistered<TService>(EndpointFilterFactoryContext context)
    {
        var isService = context.ApplicationServices.GetRequiredService<IServiceProviderIsService>();

        if (!isService.IsService(typeof(TService)))
        {
            throw new InvalidOperationException(
                $"{context.MethodInfo.DeclaringType?.Name}.{context.MethodInfo.Name} needs {typeof(TService).Name} to be registered.");
        }
    }

    private static int GetParameterIndex<TArgument>(EndpointFilterFactoryContext context, string? parameterName)
    {
        var parameters = context.MethodInfo.GetParameters();
        var index = Array.FindIndex(parameters, p =>
            p.ParameterType == typeof(TArgument) &&
            (parameterName is null || string.Equals(p.Name, parameterName, StringComparison.OrdinalIgnoreCase)));

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"{context.MethodInfo.DeclaringType?.Name}.{context.MethodInfo.Name} has no {typeof(TArgument).Name} parameter{(parameterName is null ? string.Empty : $" named '{parameterName}'")}.");
        }

        return index;
    }
}