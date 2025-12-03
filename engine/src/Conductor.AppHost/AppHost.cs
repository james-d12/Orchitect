var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Conductor_Engine_Api>("conductor-engine-api");

builder.AddProject<Projects.Conductor_Engine_Playground>("conductor-engine-playground");

builder.AddProject<Projects.Conductor_Inventory_Application>("conductor-inventory-application");

builder.Build().Run();