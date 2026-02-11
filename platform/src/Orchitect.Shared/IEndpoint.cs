using Microsoft.AspNetCore.Routing;

namespace Orchitect.Shared;

public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder builder);
}