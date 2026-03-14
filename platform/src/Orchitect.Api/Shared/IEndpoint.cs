using Microsoft.AspNetCore.Routing;

namespace Orchitect.Api.Shared;

public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder builder);
}