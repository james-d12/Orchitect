using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Infrastructure;
using Orchitect.Infrastructure.Engine;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Secret;
using Orchitect.Persistence;
using Orchitect.Runner;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructureServices();
builder.Services.AddPersistenceServices();
builder.Services.AddRunnerServices(builder.Configuration);

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

var operationOption = new Option<RunnerOperation>("--operation")
{
    Description = "Whether to provision or destroy the deployment's resources.",
    DefaultValueFactory = _ => RunnerOperation.Provision
};

rootCommand.Options.Add(applicationIdOption);
rootCommand.Options.Add(deploymentIdOption);
rootCommand.Options.Add(operationOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var applicationId = new ApplicationId(parseResult.GetRequiredValue(applicationIdOption));
    var deploymentId = new DeploymentId(parseResult.GetRequiredValue(deploymentIdOption));
    var operation = parseResult.GetValue(operationOption);

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

    var backendOptions = scope.ServiceProvider.GetRequiredService<IOptions<TerraformBackendOptions>>().Value;

    if (!backendOptions.IsRemote)
    {
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogWarning(
            "TerraformBackend:Mode is Local. State will be lost when this runner " +
            "container is removed, so later provision/destroy runs will not see these resources.");
    }

    var secretEnvironmentLoader = scope.ServiceProvider.GetRequiredService<ISecretEnvironmentLoader>();
    await secretEnvironmentLoader.LoadAsync(cancellationToken);

    var orchestrator = scope.ServiceProvider.GetRequiredService<IEngineOrchestrator>();

    switch (operation)
    {
        case RunnerOperation.Provision:
            await orchestrator.StartAsync(application, deployment, cancellationToken);
            break;
        case RunnerOperation.Destroy:
            await orchestrator.DestroyAsync(application, deployment, cancellationToken);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported runner operation.");
    }
});

return await rootCommand.Parse(args).InvokeAsync();

namespace Orchitect.Runner
{
    internal enum RunnerOperation
    {
        Provision,
        Destroy
    }
}
