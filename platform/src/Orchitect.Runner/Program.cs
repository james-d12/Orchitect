using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Terraform;
using Orchitect.Engine.Execution;
using Orchitect.Engine.Execution.Secret;
using Orchitect.Persistence;
using Orchitect.ServiceDefaults;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

RunnerSecretsFile.LoadIntoEnvironment();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.AddServiceDefaults();
builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);

builder.Services.AddEngineProvisioningServices();
builder.Services.AddPersistenceServices();
builder.Services.AddRunnerServices(builder.Configuration);

using var host = builder.Build();

var rootCommand = new RootCommand("Self contained Runner for Orchitect that provisions resources.");

var applicationIdOption = new Option<Guid>(RunnerArguments.ApplicationId)
{
    Description = "The application id to provision resources.",
    Required = true
};

var deploymentIdOption = new Option<Guid>(RunnerArguments.DeploymentId)
{
    Description = "The deployment id to provision resources.",
    Required = true
};

var operationOption = new Option<RunnerOperation>(RunnerArguments.Operation)
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

    ActivityContext.TryParse(
        Environment.GetEnvironmentVariable(RunnerEnvironment.TraceParent),
        Environment.GetEnvironmentVariable(RunnerEnvironment.TraceState),
        isRemote: true,
        out var parentContext);

    using var activity = Tracing.StartActivity(parentContext, "Run");
    activity?.SetTag("orchitect.deployment.id", deploymentId.Value);
    activity?.SetTag("orchitect.run.operation", operation.ToString());

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
            throw new InvalidOperationException($"Unsupported runner operation '{operation}'.");
    }
});

await host.StartAsync();

var exitCode = await rootCommand.Parse(args)
    .InvokeAsync(new InvocationConfiguration { ProcessTerminationTimeout = Timeout.InfiniteTimeSpan });

await host.StopAsync();

return exitCode;
