using System.Text;
using Conductor.Engine.Api.Common;
using Conductor.Engine.Api.Endpoints;
using Conductor.Engine.Infrastructure;
using Conductor.Engine.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddLogging();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("JwtOptions"))
    .ValidateDataAnnotations()
    .ValidateOnStart();


builder.Services
    .AddOpenApi()
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddPersistenceServices()
    .AddInfrastructureServices();

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ConductorDbContext>();

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
            Encoding.UTF8.GetBytes(builder.Configuration["JwtOptions:SigningKey"]!));
    });

builder.Services.AddAuthorization();

await builder.Services.ApplyMigrations();

WebApplication app = builder.Build();

app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

await app.RunAsync();