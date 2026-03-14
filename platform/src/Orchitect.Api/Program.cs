using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.VisualStudio.Services.Common;
using Orchitect.Api.Endpoints;
using Orchitect.Api.Jobs;
using Orchitect.Api.Queue;
using Orchitect.Api.Settings;
using Orchitect.Infrastructure;
using Orchitect.Persistence;
using Orchitect.Persistence.Services;
using Orchitect.ServiceDefaults;

var applicationName = AppDomain.CurrentDomain.FriendlyName;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
    builder.AddServiceDefaults();

    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });

    builder.Services.AddHostedService<QueuedHostedService>();
    builder.Services.AddSingleton<IBackgroundTaskQueueProcessor>(_ => new BackgroundTaskQueueProcessor(5));

    builder.Services.AddLogging();
    builder.Services.AddOpenApi()
        .AddEndpointsApiExplorer()
        .AddPersistenceServices()
        .AddInfrastructureServices();

    builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<OrchitectDbContext>();

    builder.Services.AddOptions<JwtOptions>()
        .Bind(builder.Configuration.GetSection("JwtOptions"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<EncryptionOptions>()
        .Bind(builder.Configuration.GetSection("EncryptionOptions"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddHostedService<DiscoveryHostedService>();
    
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters
            .Add(new JsonStringEnumConverter());
    });

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters()
            {
                ValidateActor = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidateAudience = true,
                ValidateSignatureLast = true,

                ValidIssuer = builder.Configuration["JwtOptions:Issuer"],
                ValidAudience = builder.Configuration["JwtOptions:Audience"],
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["JwtOptions:Secret"]!)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

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

    builder.Services.AddAuthorization();

    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter 'Bearer {token}'"
        });

        options.AddSecurityRequirement(_ =>
        {
            var schemeReference = new OpenApiSecuritySchemeReference("schema")
            {
                Reference = new OpenApiReferenceWithDescription()
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };

            var requirement = new OpenApiSecurityRequirement();
            requirement.Add(schemeReference, []);

            return requirement;
        });
    });

    await builder.Services.ApplyMigrations();

    WebApplication app = builder.Build();

    app.MapDefaultEndpoints();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("AllowFrontendOrigin");

    app.MapOpenApi();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapEndpoints();

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