var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var conductorDb = postgres.AddDatabase("conductor");

builder.AddProject<Projects.Conductor_Engine_Api>("conductor-engine-api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(conductorDb)
    .WaitFor(conductorDb);

builder.AddProject<Projects.Conductor_Engine_Playground>("conductor-engine-playground")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithOtlpExporter()
    .WithReference(conductorDb)
    .WithExplicitStart()
    .WaitFor(conductorDb);

builder.AddProject<Projects.Conductor_Inventory_Api>("conductor-inventory-api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithOtlpExporter()
    .WithReference(conductorDb)
    .WaitFor(conductorDb);

var app = builder.Build();
await app.RunAsync();