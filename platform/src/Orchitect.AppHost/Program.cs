var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithHostPort(41031);
var orchitectDb = postgres.AddDatabase("orchitect");

var api = builder.AddProject<Projects.Orchitect_Api>("orchitect-api")
    .WithOtlpExporter()
    .WithHttpEndpoint(port: 41005)
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "Swagger";
        url.Url = $"{url.Url}/swagger/index.html";
    })
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("CorsSettings__AllowedFrontend", "https://localhost:3001")
    .WithReference(orchitectDb)
    .WithExternalHttpEndpoints()
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
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api);

var app = builder.Build();
await app.RunAsync();