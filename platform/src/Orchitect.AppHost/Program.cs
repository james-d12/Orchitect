var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithHostPort(41031);
var orchitectDb = postgres.AddDatabase("orchitect");

var coreApi = builder.AddProject<Projects.Orchitect_Core_Api>("orchitect-core-api")
    .WithOtlpExporter()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(orchitectDb)
    .WaitFor(orchitectDb);

var engineApi = builder.AddProject<Projects.Orchitect_Engine_Api>("orchitect-engine-api")
    .WithOtlpExporter()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(orchitectDb)
    .WaitFor(orchitectDb);

var inventoryApi = builder.AddProject<Projects.Orchitect_Inventory_Api>("orchitect-inventory-api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithOtlpExporter()
    .WithReference(orchitectDb)
    .WaitFor(orchitectDb);

builder.AddProject<Projects.Orchitect_Playground>("orchitect-playground")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithOtlpExporter()
    .WithReference(orchitectDb)
    .WithExplicitStart()
    .WaitFor(orchitectDb);

builder.AddJavaScriptApp("orchitect-portal-web", "../../../portals/Orchitect.Portal.Web")
    .WithPnpm()
    .WithRunScript("dev")
    .WithArgs("--port", "3001")
    .WithBuildScript("build")
    .WithHttpEndpoint(targetPort: 3001)
    .WithEnvironment("ENGINE_API_BASE_URL", engineApi.GetEndpoint("http"))
    .WithEnvironment("INVENTORY_API_BASE_URL", inventoryApi.GetEndpoint("http"))
    .WithReference(coreApi)
    .WithReference(engineApi)
    .WithReference(inventoryApi)
    .WaitFor(coreApi)
    .WaitFor(engineApi)
    .WaitFor(inventoryApi);

var app = builder.Build();
await app.RunAsync();