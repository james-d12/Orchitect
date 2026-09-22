using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchitect.Domain.Engine.Application;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Infrastructure;
using Orchitect.Infrastructure.Engine;
using Orchitect.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructureServices();
builder.Services.AddPersistenceServices();

using var host = builder.Build();

var rootCommand = new RootCommand("Self contained Runner for Orchitect that provisions resources.");

var applicationIdOption = new Option<Guid>("--application-id")
{
    Description = "The application id to provision resources.",
    Required = true
};

var deploymentIdOption = new Option<Guid>("--deployment-id")
{
    Description = "The deployment id to provision resources.",
    Required = true
};

rootCommand.Options.Add(applicationIdOption);
rootCommand.Options.Add(deploymentIdOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var applicationId = new ApplicationId(parseResult.GetRequiredValue(applicationIdOption));
    var deploymentId = new DeploymentId(parseResult.GetRequiredValue(deploymentIdOption));

    using var scope = host.Services.CreateScope();

    var applicationRepository = scope.ServiceProvider.GetRequiredService<IApplicationRepository>();
    var deploymentRepository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();

    var application = await applicationRepository.GetByIdAsync(applicationId, cancellationToken);
    var deployment = await deploymentRepository.GetByIdAsync(deploymentId, cancellationToken);

    if (application is null || deployment is null)
    {
        throw new ArgumentException(
            $"Application {applicationId.Value} or Deployment {deploymentId.Value} not found.");
    }

    var orchestrator = scope.ServiceProvider.GetRequiredService<IEngineOrchestrator>();

    await orchestrator.StartAsync(application, deployment, cancellationToken);
});

return await rootCommand.Parse(args).InvokeAsync();
