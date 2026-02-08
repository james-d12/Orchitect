var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var conductorDb = postgres.AddDatabase("conductor");

var engineApi = builder.AddProject<Projects.Conductor_Engine_Api>("conductor-engine-api")
    .WithOtlpExporter()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(conductorDb)
    .WaitFor(conductorDb);

builder.AddProject<Projects.Conductor_Engine_Playground>("conductor-engine-playground")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithOtlpExporter()
    .WithReference(conductorDb)
    .WithExplicitStart()
    .WaitFor(conductorDb);

var inventoryApi = builder.AddProject<Projects.Conductor_Inventory_Api>("conductor-inventory-api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithOtlpExporter()
    .WithReference(conductorDb)
    .WaitFor(conductorDb);

builder.AddJavaScriptApp("conductor-portal-web", "../../../portals/Conductor.Client.Web")
    .WithPnpm()
    .WithRunScript("dev")
    .WithArgs("--port", "3001")
    .WithBuildScript("build")
    .WithHttpEndpoint(targetPort: 3001)
    .WithEnvironment("ENGINE_API_BASE_URL", engineApi.GetEndpoint("http"))
    .WithEnvironment("INVENTORY_API_BASE_URL", inventoryApi.GetEndpoint("http"))
    .WithReference(engineApi)
    .WithReference(inventoryApi)
    .WaitFor(engineApi)
    .WaitFor(inventoryApi);

var app = builder.Build();
await app.RunAsync();