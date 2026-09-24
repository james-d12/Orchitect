using System.Data.Common;
using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;

namespace Orchitect.Infrastructure.Engine.Executor;

public sealed class DockerExecutor : IExecutor
{
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RawOutputFlushInterval = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<DockerExecutor> _logger;
    private readonly DockerClient _docker;

    public DockerExecutor(ILogger<DockerExecutor> logger, DockerClient docker)
    {
        _logger = logger;
        _docker = docker;
    }

    public async Task ExecuteAsync(
        ExecutorContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("orchitect.run.id", context.RunId);
        activity?.SetTag("container.image.name", context.Image);

        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["RunId"] = context.RunId });

        _logger.LogInformation(
            "Executing run {RunId} using image {Image} with {ArgumentCount} arguments and {ConfigurationCount} configuration values.",
            context.RunId, context.Image, context.Arguments.Count, context.Configuration.Count);

        await EnsureImageExistsAsync(context.Image, cancellationToken);

        var network = await ResolveNetworkAsync(context.Network, cancellationToken);
        activity?.SetTag("container.network", network);

        var containerName = $"orchitect-runner-{context.RunId}";
        CreateContainerResponse container;

        try
        {
            container = await _docker.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Name = containerName,
                    Image = context.Image,
                    Cmd = context.Arguments.ToList(),
                    Env =
                    [
                        $"ORCHITECT_RUN_ID={context.RunId}",
                        $"ConnectionStrings__orchitect={BuildRunnerConnectionString(context)}",
                        "Logging__LogLevel__Default=Debug",
                        ..context.Configuration.Select(x => $"{x.Key}={x.Value}")
                    ],
                    HostConfig = new HostConfig
                    {
                        NetworkMode = network,
                        ExtraHosts = ["host.docker.internal:host-gateway"]
                    }
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            activity.RecordException(exception);
            _logger.LogError(exception, "Failed to create runner container {ContainerName} for run {RunId}.",
                containerName, context.RunId);
            throw;
        }

        activity?.SetTag("container.id", container.ID);
        activity?.SetTag("container.name", containerName);

        foreach (var warning in container.Warnings ?? [])
        {
            _logger.LogWarning("Docker warning while creating runner container {ContainerId}: {Warning}",
                container.ID, warning);
        }

        _logger.LogInformation("Created runner container {ContainerId} ({ContainerName}) for run {RunId}.",
            container.ID, containerName, context.RunId);

        var started = false;
        var detached = false;

        using var logCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            started = await _docker.Containers.StartContainerAsync(
                container.ID,
                new ContainerStartParameters(),
                cancellationToken);

            if (!started)
            {
                throw new InvalidOperationException(
                    $"Failed to start runner container '{container.ID}'.");
            }

            _logger.LogInformation("Started runner container {ContainerId}, waiting for it to exit.", container.ID);
            activity?.AddEvent(new ActivityEvent("container.started"));

            Task logStreaming = StreamContainerOutputAsync(container.ID, logCancellation.Token);

            var stopwatch = Stopwatch.StartNew();

            var wait = await _docker.Containers.WaitContainerAsync(
                container.ID,
                cancellationToken);

            stopwatch.Stop();
            activity?.SetTag("container.exit_code", wait.StatusCode);
            activity?.AddEvent(new ActivityEvent("container.exited"));

            _logger.LogInformation(
                "Runner container {ContainerId} exited with code {ExitCode} after {ElapsedMilliseconds}ms.",
                container.ID, wait.StatusCode, stopwatch.ElapsedMilliseconds);

            if (wait.Error is not null)
            {
                _logger.LogWarning("Docker reported an error while waiting on runner container {ContainerId}: {Error}",
                    container.ID, wait.Error.Message);
            }

            await WaitForOutputAsync(container.ID, logStreaming, logCancellation);

            if (wait.StatusCode != 0)
            {
                throw new InvalidOperationException(
                    $"Runner '{context.RunId}' failed with exit code {wait.StatusCode}.");
            }

            _logger.LogInformation("Run {RunId} completed successfully.", context.RunId);
        }
        catch (OperationCanceledException exception) when (started && cancellationToken.IsCancellationRequested)
        {
            detached = true;
            activity?.SetTag("container.detached", true);
            activity.RecordException(exception);
            _logger.LogWarning(exception,
                "Run {RunId} was cancelled while runner container {ContainerId} was running. The container was " +
                "left running so terraform can finish. Check its logs and remove it once it has exited.",
                context.RunId, container.ID);
            throw;
        }
        catch (Exception exception)
        {
            activity.RecordException(exception);
            _logger.LogError(exception, "Run {RunId} failed in runner container {ContainerId}.",
                context.RunId, container.ID);
            throw;
        }
        finally
        {
            if (!detached)
            {
                await RemoveContainerAsync(container.ID, CancellationToken.None);
            }
        }
    }

    private async Task<string?> ResolveNetworkAsync(
        string? network,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(network))
        {
            return null;
        }

        var networks = await _docker.Networks.ListNetworksAsync(new NetworksListParameters(), cancellationToken);

        if (networks.Any(x => x.Name == network))
        {
            return network;
        }

        var matches = networks
            .Where(x => x.Name.StartsWith(network, StringComparison.Ordinal))
            .Select(x => x.Name)
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"No docker network named or starting with '{network}' was found."),
            _ => throw new InvalidOperationException(
                $"Docker network '{network}' is ambiguous, it matches: {string.Join(", ", matches)}.")
        };
    }

    private static string BuildRunnerConnectionString(ExecutorContext context)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__orchitect");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__orchitect is not set, so the runner container cannot reach the database.");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (!string.IsNullOrWhiteSpace(context.DatabaseHost))
        {
            builder["Host"] = context.DatabaseHost;
        }
        else if (builder.TryGetValue("Host", out var host) &&
            host is string value &&
            value is "localhost" or "127.0.0.1" or "::1")
        {
            builder["Host"] = "host.docker.internal";
        }

        if (context.DatabasePort is { } port)
        {
            builder["Port"] = port;
        }

        return builder.ConnectionString;
    }

    private async Task EnsureImageExistsAsync(
        string image,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("container.image.name", image);

        try
        {
            var inspect = await _docker.Images.InspectImageAsync(image, cancellationToken);
            activity?.SetTag("container.image.id", inspect.ID);
            _logger.LogDebug("Found runner image {Image} ({ImageId}).", image, inspect.ID);
        }
        catch (DockerImageNotFoundException exception)
        {
            activity.RecordException(exception);
            _logger.LogError(exception, "Runner image {Image} does not exist.", image);
            throw new InvalidOperationException(
                $"Runner image '{image}' does not exist.");
        }
    }

    private async Task WaitForOutputAsync(
        string containerId,
        Task logStreaming,
        CancellationTokenSource logCancellation)
    {
        try
        {
            await logStreaming.WaitAsync(OutputDrainTimeout);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Output from runner container {ContainerId} did not finish within {Timeout} after it exited.",
                containerId, OutputDrainTimeout);
            await logCancellation.CancelAsync();
        }
    }

    private async Task StreamContainerOutputAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("container.id", containerId);

        var shortId = containerId[..Math.Min(12, containerId.Length)];
        var stdout = new RunnerOutputRelay(_logger, shortId, LogLevel.Information);
        var stderr = new RunnerOutputRelay(_logger, shortId, LogLevel.Warning);

        try
        {
            using var stream = await _docker.Containers.GetContainerLogsAsync(
                containerId,
                tty: false,
                new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true
                },
                cancellationToken);

            var buffer = new byte[8192];
            var read = stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);

            while (true)
            {
                if (await Task.WhenAny(read, Task.Delay(RawOutputFlushInterval, cancellationToken)) != read)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stdout.FlushRaw();
                    stderr.FlushRaw();
                    continue;
                }

                var result = await read;

                if (result.EOF)
                {
                    break;
                }

                var target = result.Target == MultiplexedStream.TargetStream.StandardError ? stderr : stdout;
                target.Append(buffer, result.Count);

                read = stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            activity.RecordException(exception);
            _logger.LogWarning(exception, "Stopped streaming output from runner container {ContainerId}.",
                containerId);
        }
        finally
        {
            stdout.Complete();
            stderr.Complete();

            activity?.SetTag("container.stdout.entries", stdout.EntryCount);
            activity?.SetTag("container.stderr.entries", stderr.EntryCount);
        }
    }

    private async Task RemoveContainerAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("container.id", containerId);

        try
        {
            await _docker.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters
                {
                    Force = true
                },
                cancellationToken);

            _logger.LogInformation("Removed runner container {ContainerId}.", containerId);
        }
        catch (DockerContainerNotFoundException exception)
        {
            activity.RecordException(exception);
            _logger.LogError(exception, "Container {ContainerId} does not exist.", containerId);
        }
    }
}
