using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
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
using Orchitect.Engine.Execution.RunnerApi;
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

if (!string.IsNullOrWhiteSpace(builder.Configuration[RunnerEnvironment.ApiBaseUrl]))
{
    builder.Services.AddRunnerApiClient(builder.Configuration);
}

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
    var runId = Guid.TryParse(host.Services.GetRequiredService<IConfiguration>()[RunnerEnvironment.RunId],
        out var runGuid)
        ? new DeploymentRunId(runGuid)
        : throw new InvalidOperationException($"{RunnerEnvironment.RunId} must be a non-empty GUID.");

    ActivityContext.TryParse(
        Environment.GetEnvironmentVariable(RunnerEnvironment.TraceParent),
        Environment.GetEnvironmentVariable(RunnerEnvironment.TraceState),
        isRemote: true,
        out var parentContext);

    using var activity = Tracing.StartActivity(parentContext, "Run");
    activity?.SetTag("orchitect.deployment.id", deploymentId.Value);
    activity?.SetTag("orchitect.run.id", runId.Value);
    activity?.SetTag("orchitect.run.operation", operation.ToString());

    using var scope = host.Services.CreateScope();

    async Task RunAsync(CancellationToken ct)
    {
        var applicationRepository = scope.ServiceProvider.GetRequiredService<IApplicationRepository>();
        var deploymentRepository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();

        var application = await applicationRepository.GetByIdAsync(applicationId, ct);
        var deployment = await deploymentRepository.GetByIdAsync(deploymentId, ct);

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
        await secretEnvironmentLoader.LoadAsync(ct);

        var orchestrator = scope.ServiceProvider.GetRequiredService<IEngineOrchestrator>();

        switch (operation)
        {
            case RunnerOperation.Provision:
                await orchestrator.StartAsync(application, deployment, runId, ct);
                break;
            case RunnerOperation.Destroy:
                await orchestrator.DestroyAsync(application, deployment, runId, ct);
                break;
            default:
                throw new InvalidOperationException($"Unsupported runner operation '{operation}'.");
        }
    }

    if (scope.ServiceProvider.GetService<IRunCompletionReporter>() is { } reporter)
    {
        await reporter.RunAsync(RunAsync, cancellationToken);
    }
    else
    {
        await RunAsync(cancellationToken);
    }
});

await host.StartAsync();

var exitCode = await rootCommand.Parse(args)
    .InvokeAsync(new InvocationConfiguration { ProcessTerminationTimeout = Timeout.InfiniteTimeSpan });

await host.StopAsync();

return exitCode;
