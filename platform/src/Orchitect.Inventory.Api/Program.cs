using System.Text.Json;
using System.Text.Json.Serialization;
using Orchitect.Inventory.Api.Jobs;
using Orchitect.Inventory.Api.Endpoints.Discovery;
using Orchitect.Inventory.Infrastructure.Azure.Extensions;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Extensions;
using Orchitect.Inventory.Infrastructure.GitHub.Extensions;
using Orchitect.Inventory.Infrastructure.GitLab.Extensions;
using Orchitect.Inventory.Persistence;
using Orchitect.Core.Persistence;
using Orchitect.Core.Persistence.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Orchitect.Inventory.Api.Endpoints;
using Orchitect.Inventory.Api.Settings;
using Orchitect.ServiceDefaults;
using Orchitect.Shared;

var builder = WebApplication.CreateBuilder(args);
var applicationName = AppDomain.CurrentDomain.FriendlyName;

var loggerFactory = LoggerFactory.Create(loggingBuilder =>
{
    loggingBuilder.AddJsonConsole();
    loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));
    loggingBuilder.AddDebug();
    loggingBuilder.AddEventSourceLogger();
});
var logger = loggerFactory.CreateLogger<Program>();

try
{
    logger.LogDebug("Configuration: {Config}",
        JsonSerializer.Serialize(builder.Configuration.GetSection("CorsSettings").Value));
    logger.LogInformation("Starting up: {ApplicationName}", applicationName);

    builder.AddServiceDefaults();
    builder.Services.AddLogging();

    // Configure encryption options (required by Core's IEncryptionService)
    builder.Services.AddOptions<EncryptionOptions>()
        .Bind(builder.Configuration.GetSection("EncryptionOptions"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    builder.Services
        .AddOpenApi()
        .AddEndpointsApiExplorer()
        .AddSwaggerGen();
    builder.Services.AddHostedService<DiscoveryHostedService>();

    // Add Core services (provides IEncryptionService, ICredentialRepository, etc.)
    builder.Services.AddCorePersistenceServices();

    builder.Services.RegisterAzure();
    builder.Services.RegisterAzureDevOps();
    builder.Services.RegisterGitHub();
    builder.Services.RegisterGitLab();
    builder.Services.AddInventoryPersistenceServices();

    var corsSettings = builder.Configuration.GetSection(nameof(CorsSettings)).Get<CorsSettings>();

    builder.Services.AddCors(options =>
    {
        logger.LogInformation("Enabling Cors.");

        corsSettings?.Policies
            .ForEach(policy =>
            {
                logger.LogInformation("Adding Cors policy: {Name} {Origin}", policy.Key, policy.Value);

                options.AddPolicy(name: policy.Key, corsPolicy =>
                {
                    corsPolicy
                        .WithOrigins(policy.Value)
                        .SetIsOriginAllowed(_ => true)
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });
    });

    await builder.Services.ApplyInventoryMigrations();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        logger.LogInformation("Adding swagger UI");
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("AllowFrontendOrigin");

    app.UseHttpsRedirection();
    app.UseAuthorization();

    app.MapInventoryEndpoints();

    await app.RunAsync();
}
catch (Exception exception)
{
    logger.LogCritical(exception, "Could not startup: {ApplicationName} due to Exception: {Exception}.",
        applicationName, exception);
}
finally
{
    logger.LogInformation("Stopping: {ApplicationName}.", applicationName);
}