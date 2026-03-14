using Microsoft.AspNetCore.Builder;
using Orchitect.Api.Endpoints.Core;
using Orchitect.Api.Endpoints.Engine;
using Orchitect.Api.Endpoints.Inventory;

namespace Orchitect.Api.Endpoints;

public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapCoreEndpoints();
        app.MapEngineEndpoints();
        app.MapInventoryEndpoints();
    }
}