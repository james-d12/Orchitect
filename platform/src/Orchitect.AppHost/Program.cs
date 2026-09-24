var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithHostPort(41031);
var orchitectDb = postgres.AddDatabase("orchitect");

var keyVaultUri = builder.AddParameter("keyvault-uri");

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
    .WithEnvironment("ExecutorOptions__Network", "aspire-session-network-")
    .WithEnvironment("ExecutorOptions__DatabaseHost", postgres.Resource.Name)
    .WithEnvironment("ExecutorOptions__DatabasePort", "5432")
    .WithEnvironment("ExecutorOptions__SecretProvider__Type", "AzureKeyVault")
    .WithEnvironment("ExecutorOptions__SecretProvider__AzureKeyVault__VaultUri", keyVaultUri)
    .WithEnvironment("ExecutorOptions__SecretProvider__Mappings__ARM_CLIENT_ID", "terraform-client-id")
    .WithEnvironment("ExecutorOptions__SecretProvider__Mappings__ARM_CLIENT_SECRET", "terraform-client-secret")
    .WithEnvironment("ExecutorOptions__SecretProvider__Mappings__ARM_TENANT_ID", "terraform-tenant-id")
    .WithEnvironment("ExecutorOptions__SecretProvider__Mappings__ARM_SUBSCRIPTION_ID", "terraform-subscription-id")
    .WithEnvironment("ExecutorOptions__Configuration__ARM_RESOURCE_PROVIDER_REGISTRATIONS", "core")
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