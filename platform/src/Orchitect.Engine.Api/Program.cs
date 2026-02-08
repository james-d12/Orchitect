using System.Text;
using Orchitect.Engine.Api.Common;
using Orchitect.Engine.Api.Endpoints;
using Orchitect.Engine.Api.Queue;
using Orchitect.Engine.Infrastructure;
using Orchitect.Engine.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<IBackgroundTaskQueueProcessor>(ctx => new BackgroundTaskQueueProcessor(5));

builder.Services.AddLogging();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("JwtOptions"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOpenApi()
    .AddEndpointsApiExplorer()
    .AddPersistenceServices()
    .AddInfrastructureServices();

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<EngineDbContext>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters.ValidIssuer = builder.Configuration["JwtOptions:Issuer"];
        options.TokenValidationParameters.ValidAudience = builder.Configuration["JwtOptions:Audience"];
        options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JwtOptions:Secret"]!));
        options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
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

    options.AddSecurityRequirement(doc =>
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

app.MapOpenApi();
app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

await app.RunAsync();