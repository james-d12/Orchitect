namespace Orchitect.Engine.Api.Common;

public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder builder);
}