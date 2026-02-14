using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Orchitect.Core.Api.Configuration;
using Orchitect.Core.Api.Endpoints;
using Orchitect.Core.Persistence;
using Orchitect.Core.Persistence.Services;
using Orchitect.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("JwtOptions"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EncryptionOptions>()
    .Bind(builder.Configuration.GetSection("EncryptionOptions"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddLogging();
builder.Services
    .AddOpenApi()
    .AddEndpointsApiExplorer()
    .AddCorePersistenceServices();

builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<CoreDbContext>();

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

await builder.Services.ApplyCoreMigrations();

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