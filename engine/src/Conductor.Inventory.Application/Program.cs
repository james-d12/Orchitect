using Conductor.Inventory.Application.Jobs;
using Conductor.Inventory.Infrastructure.Azure.Extensions;
using Conductor.Inventory.Infrastructure.AzureDevOps.Extensions;
using Conductor.Inventory.Infrastructure.GitHub.Extensions;
using Conductor.Inventory.Infrastructure.GitLab.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.AddServiceDefaults();

builder.Services.RegisterAzure(builder.Configuration);
builder.Services.RegisterAzureDevOps(builder.Configuration);
builder.Services.RegisterGitHub(builder.Configuration);
builder.Services.RegisterGitLab(builder.Configuration);
builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddHostedService<DiscoveryHostedService>();

using IHost host = builder.Build();

await host.RunAsync();