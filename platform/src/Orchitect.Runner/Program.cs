using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Common.Observability;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Terraform;
using Orchitect.Engine.Execution;
using Orchitect.ServiceDefaults;

RunnerSecretsFile.LoadIntoEnvironment();

var rootCommand = new RootCommand("Self contained Runner for Orchitect that provisions resources.");

var runIdOption = new Option<Guid>(RunnerArguments.RunId)
{
    Description = "The run to execute. The runner fetches what to do from the Orchitect API.",
    Required = true
};

rootCommand.Options.Add(runIdOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var runId = parseResult.GetRequiredValue(runIdOption);

    var builder = Host.CreateApplicationBuilder();

    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        [RunnerEnvironment.RunId] = runId.ToString()
    });
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
    builder.AddServiceDefaults();
    builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);

    builder.Services.AddEngineProvisioningServices();
    builder.Services.AddRunnerServices(builder.Configuration);
    builder.Services.AddRunnerApiClient(builder.Configuration);

    using var host = builder.Build();

    await host.StartAsync(cancellationToken);

    try
    {
        ActivityContext.TryParse(
            Environment.GetEnvironmentVariable(RunnerEnvironment.TraceParent),
            Environment.GetEnvironmentVariable(RunnerEnvironment.TraceState),
            isRemote: true,
            out var parentContext);

        using var activity = Tracing.StartActivity(parentContext, "Run");
        activity?.SetTag("orchitect.run.id", runId);

        using var scope = host.Services.CreateScope();

        var backendOptions = scope.ServiceProvider.GetRequiredService<IOptions<TerraformBackendOptions>>().Value;

        if (!backendOptions.IsRemote)
        {
            scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogWarning(
                "TerraformBackend:Mode is Local. State will be lost when this runner " +
                "container is removed, so later provision/destroy runs will not see these resources.");
        }

        await scope.ServiceProvider.GetRequiredService<IEngineOrchestrator>().RunAsync(cancellationToken);
    }
    finally
    {
        await host.StopAsync(CancellationToken.None);
    }
});

return await rootCommand.Parse(args)
    .InvokeAsync(new InvocationConfiguration { ProcessTerminationTimeout = Timeout.InfiniteTimeSpan });
