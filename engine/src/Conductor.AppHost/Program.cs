var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var conductorDb = postgres.AddDatabase("conductor");

builder.AddProject<Projects.Conductor_Engine_Api>("conductor-engine-api")
    .WithReference(conductorDb)
    .WaitFor(conductorDb);

builder.AddProject<Projects.Conductor_Engine_Playground>("conductor-engine-playground")
    .WithOtlpExporter()
    .WithReference(conductorDb)
    .WaitFor(conductorDb);

builder.AddProject<Projects.Conductor_Inventory_Application>("conductor-inventory-application")
    .WithOtlpExporter()
    .WithReference(conductorDb)
    .WaitFor(conductorDb);

var app = builder.Build();
await app.RunAsync();