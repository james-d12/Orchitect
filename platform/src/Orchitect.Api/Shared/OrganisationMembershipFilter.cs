using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Shared;

public sealed class OrganisationMembershipFilter : IEndpointFilter
{
    private const string OrganisationIdName = "OrganisationId";

    private static readonly ConcurrentDictionary<Type, PropertyInfo?> OrganisationIdProperties = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var requested = GetRequestedOrganisationIds(context).ToList();

        if (requested.Count == 0)
        {
            return await next(context);
        }

        var organisationIds = new HashSet<OrganisationId>();

        foreach (var value in requested)
        {
            if (!TryGetOrganisationId(value, out var organisationId))
            {
                return TypedResults.BadRequest(new ErrorResponse
                {
                    Errors = [new Error { Code = "INVALID_ORGANISATION_ID", Message = "Organisation id is not valid." }]
                });
            }

            organisationIds.Add(organisationId);
        }

        var httpContext = context.HttpContext;
        var repository = httpContext.RequestServices.GetRequiredService<IOrganisationRepository>();

        foreach (var organisationId in organisationIds)
        {
            if (!await repository.IsMemberAsync(httpContext.User, organisationId, httpContext.RequestAborted))
            {
                return TypedResults.Forbid();
            }
        }

        return await next(context);
    }

    private static IEnumerable<object?> GetRequestedOrganisationIds(EndpointFilterInvocationContext context)
    {
        var request = context.HttpContext.Request;

        foreach (var (key, value) in request.Query)
        {
            if (key.Equals(OrganisationIdName, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in value)
                {
                    yield return item;
                }
            }
        }

        foreach (var (key, value) in request.RouteValues)
        {
            if (key.Equals(OrganisationIdName, StringComparison.OrdinalIgnoreCase))
            {
                yield return value;
            }
        }

        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var property = OrganisationIdProperties.GetOrAdd(argument.GetType(), GetOrganisationIdProperty);

            if (property is not null)
            {
                yield return property.GetValue(argument);
            }
        }
    }

    private static PropertyInfo? GetOrganisationIdProperty(Type type)
    {
        var property = type.GetProperty(OrganisationIdName, BindingFlags.Public | BindingFlags.Instance);

        if (property is null)
        {
            return null;
        }

        var propertyType = property.PropertyType;
        return propertyType == typeof(Guid) || propertyType == typeof(string) || propertyType == typeof(OrganisationId)
            ? property
            : null;
    }

    private static bool TryGetOrganisationId(object? value, out OrganisationId organisationId)
    {
        switch (value)
        {
            case OrganisationId id:
                organisationId = id;
                return true;
            case Guid guid:
                organisationId = new OrganisationId(guid);
                return true;
            case string text when Guid.TryParse(text, out var parsed):
                organisationId = new OrganisationId(parsed);
                return true;
            default:
                organisationId = default;
                return false;
        }
    }
}