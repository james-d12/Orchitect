using System.Diagnostics;
using System.Net;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Engine.Contracts.Runner;

namespace Orchitect.Engine.Dispatch.Executor;

public sealed class DockerExecutor : IExecutor
{
    private static readonly TimeSpan DefaultStopGracePeriod = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RawOutputFlushInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan OutputRelayStopTimeout = TimeSpan.FromSeconds(2);

    private const string DefaultNetworkMode = "default";
    private const string DockerHost = "host.docker.internal";

    public const string RunnerServiceName = "orchitect-runner";

    private readonly ILogger<DockerExecutor> _logger;
    private readonly IDockerClient _docker;
    private readonly IConfiguration _configuration;

    public DockerExecutor(ILogger<DockerExecutor> logger, IDockerClient docker, IConfiguration configuration)
    {
        _logger = logger;
        _docker = docker;
        _configuration = configuration;
    }

    public async Task<ExecutorResult> ExecuteAsync(
        ExecutorContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunAsync(context, cancellationToken);
        }
        catch (Exception exception)
        {
            return new ExecutorResult(null, exception);
        }
    }

    public async Task<bool> SignalStopAsync(string runId, CancellationToken cancellationToken = default)
    {
        var containers = await _docker.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool> { [$"{RunnerContainerLabels.RunId}={runId}"] = true }
                }
            },
            cancellationToken);

        var signalled = false;

        foreach (var container in containers)
        {
            try
            {
                await _docker.Containers.KillContainerAsync(
                    container.ID,
                    new ContainerKillParameters { Signal = "SIGTERM" },
                    cancellationToken);
                signalled = true;
                _logger.LogInformation("Sent SIGTERM to runner container {ContainerId} of run {RunId}.",
                    container.ID, runId);
            }
            catch (DockerApiException exception) when (exception.StatusCode is HttpStatusCode.Conflict
                                                           or HttpStatusCode.NotFound)
            {
                _logger.LogInformation(exception, "Runner container {ContainerId} of run {RunId} is not running.",
                    container.ID, runId);
            }
        }

        return signalled;
    }

    private async Task<ExecutorResult> RunAsync(ExecutorContext context, CancellationToken cancellationToken)
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
        Dictionary<string, string> secrets;

        try
        {
            secrets = new Dictionary<string, string>(context.Secrets);

            if (_configuration[RunnerEnvironment.OtlpHeaders] is { Length: > 0 } otlpHeaders)
            {
                secrets[RunnerEnvironment.OtlpHeaders] = otlpHeaders;
            }

            container = await _docker.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Name = containerName,
                    Image = context.Image,
                    Labels = new Dictionary<string, string>
                    {
                        [RunnerContainerLabels.Runner] = "true",
                        [RunnerContainerLabels.RunId] = context.RunId
                    },
                    Cmd = context.Arguments.ToList(),
                    StopTimeout = context.StopGracePeriod,
                    Env =
                    [
                        $"{RunnerEnvironment.RunId}={context.RunId}",
                        $"{RunnerEnvironment.ApiBaseUrl}={BuildRunnerApiBaseUrl(context)}",
                        ..context.Configuration.Select(x => $"{x.Key}={x.Value}"),
                        ..BuildTelemetryEnvironment(activity).Select(x => $"{x.Key}={x.Value}")
                    ],
                    HostConfig = new HostConfig
                    {
                        NetworkMode = network ?? DefaultNetworkMode,
                        ExtraHosts = [$"{DockerHost}:host-gateway"],
                        CapDrop = ["ALL"],
                        SecurityOpt = ["no-new-privileges"],
                        Init = true,
                        Memory = context.MemoryBytes ?? 0,
                        NanoCPUs = context.NanoCpus ?? 0,
                        PidsLimit = context.PidsLimit
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

        using var logCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await CopySecretsAsync(container.ID, secrets, cancellationToken);

            if (context.StopRequested.IsCancellationRequested)
            {
                _logger.LogInformation("Run {RunId} was stopped before runner container {ContainerId} started.",
                    context.RunId, container.ID);
                activity?.AddEvent(new ActivityEvent("container.stopped"));
                return new ExecutorResult(null, RunnerId: container.ID, Stopped: true);
            }

            var started = await _docker.Containers.StartContainerAsync(
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

            var wait = await WaitForExitAsync(container.ID, context.Timeout, context.StopRequested,
                cancellationToken);
            if (wait is null && context.StopRequested.IsCancellationRequested)
            {
                activity?.AddEvent(new ActivityEvent("container.stopped"));
                _logger.LogInformation(
                    "Run {RunId} was asked to stop. Stopping runner container {ContainerId}.",
                    context.RunId, container.ID);
                var exitCode = await StopContainerAsync(container.ID, context, logStreaming, logCancellation,
                    cancellationToken);
                activity?.SetTag("container.exit_code", exitCode);
                return new ExecutorResult(exitCode, RunnerId: container.ID, Stopped: true);
            }

            if (wait is null)
            {
                activity?.AddEvent(new ActivityEvent("container.timed_out"));
                _logger.LogWarning("Runner container {ContainerId} did not finish within {Timeout}. Stopping it.",
                    container.ID, context.Timeout);
                await StopContainerAsync(container.ID, context, logStreaming, logCancellation, cancellationToken);
                throw new TimeoutException($"Runner '{context.RunId}' did not finish within {context.Timeout}.");
            }

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
                _logger.LogWarning("Run {RunId} failed with exit code {ExitCode}.", context.RunId, wait.StatusCode);
            }
            else
            {
                _logger.LogInformation("Run {RunId} completed successfully.", context.RunId);
            }

            return new ExecutorResult(wait.StatusCode, RunnerId: container.ID);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            activity.RecordException(exception);
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
            var detached = await CleanupContainerAsync(container.ID, context.RunId, cancellationToken);
            activity?.SetTag("container.detached", detached);
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

    private static string BuildRunnerApiBaseUrl(ExecutorContext context)
    {
        if (context.ApiBaseUrl is not { IsAbsoluteUri: true } apiBaseUrl)
        {
            throw new InvalidOperationException(
                "ExecutorOptions:ApiBaseUrl is not set, so the runner container cannot reach the API.");
        }

        return RewriteLoopbackUrl(apiBaseUrl.AbsoluteUri);
    }

    private Dictionary<string, string> BuildTelemetryEnvironment(Activity? activity)
    {
        var environment = new Dictionary<string, string>();

        if (activity is { IdFormat: ActivityIdFormat.W3C, Id: { } traceParent })
        {
            environment[RunnerEnvironment.TraceParent] = traceParent;

            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                environment[RunnerEnvironment.TraceState] = activity.TraceStateString;
            }
        }

        var endpoint = _configuration[RunnerEnvironment.OtlpEndpoint];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return environment;
        }

        environment[RunnerEnvironment.OtlpEndpoint] = RewriteLoopbackUrl(endpoint);
        environment[RunnerEnvironment.ServiceName] = RunnerServiceName;

        if (_configuration[RunnerEnvironment.OtlpProtocol] is { Length: > 0 } protocol)
        {
            environment[RunnerEnvironment.OtlpProtocol] = protocol;
        }

        return environment;
    }

    /// <summary>
    /// Points a URL on the API's loopback or unspecified address at the Docker host, as seen from inside the runner container.
    /// </summary>
    internal static string RewriteLoopbackUrl(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !(uri.IsLoopback || IsUnspecified(uri)))
        {
            return endpoint;
        }

        var rewritten = new UriBuilder(uri) { Host = DockerHost }.Uri.ToString();
        return endpoint.EndsWith('/') ? rewritten : rewritten.TrimEnd('/');
    }

    private static bool IsUnspecified(Uri uri) =>
        IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) &&
        (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));

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

    private async Task CopySecretsAsync(
        string containerId,
        Dictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        await using var archive = RunnerSecretsFile.CreateArchive(secrets);

        await _docker.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new CopyToContainerParameters { Path = RunnerSecretsFile.DirectoryPath },
            archive,
            cancellationToken);

        _logger.LogInformation("Copied {SecretCount} secrets into runner container {ContainerId}.",
            secrets.Count, containerId);
    }

    private async Task<ContainerWaitResponse?> WaitForExitAsync(
        string containerId,
        TimeSpan? timeout,
        CancellationToken stopRequested,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopRequested);
        if (timeout is not null)
        {
            timeoutCancellation.CancelAfter(timeout.Value);
        }

        try
        {
            return await _docker.Containers.WaitContainerAsync(containerId, timeoutCancellation.Token);
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested &&
                                                  !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<long?> StopContainerAsync(
        string containerId,
        ExecutorContext context,
        Task logStreaming,
        CancellationTokenSource logCancellation,
        CancellationToken cancellationToken)
    {
        var gracePeriod = context.StopGracePeriod ?? DefaultStopGracePeriod;

        _logger.LogInformation("Sending SIGTERM to runner container {ContainerId} with a grace period of {GracePeriod}.",
            containerId, gracePeriod);

        try
        {
            await _docker.Containers.KillContainerAsync(
                containerId,
                new ContainerKillParameters { Signal = "SIGTERM" },
                cancellationToken);
        }
        catch (DockerApiException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogInformation(exception, "Runner container {ContainerId} had already exited.", containerId);
        }

        var wait = await WaitForExitAsync(containerId, gracePeriod, CancellationToken.None, cancellationToken);

        if (wait is null)
        {
            _logger.LogWarning("Runner container {ContainerId} did not stop within {GracePeriod}; it will be killed.",
                containerId, gracePeriod);
            await logCancellation.CancelAsync();
            return null;
        }

        await WaitForOutputAsync(containerId, logStreaming, logCancellation);
        return wait.StatusCode;
    }

    private async Task WaitForOutputAsync(
        string containerId,
        Task logStreaming,
        CancellationTokenSource logCancellation)
    {
        try
        {
            await logStreaming.WaitAsync(OutputDrainTimeout, logCancellation.Token);
        }
        catch (OperationCanceledException exception) when (logCancellation.IsCancellationRequested)
        {
            _logger.LogInformation(exception, "Stopped waiting for output from runner container {ContainerId} after cancellation.",
                containerId);
        }
        catch (TimeoutException exception)
        {
            _logger.LogWarning(exception,
                "Output from runner container {ContainerId} did not finish within {Timeout} after it exited.",
                containerId, OutputDrainTimeout);
            await logCancellation.CancelAsync();

            try
            {
                await logStreaming.WaitAsync(OutputRelayStopTimeout, logCancellation.Token);
            }
            catch (TimeoutException innerITimeoutException)
            {
                _logger.LogWarning(innerITimeoutException, "Output relay for runner container {ContainerId} did not stop after cancellation.",
                    containerId);
            }
        }
    }

    private async Task StreamContainerOutputAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("container.id", containerId);

        var shortId = containerId[..Math.Min(12, containerId.Length)];
        var stdout = new ExecutorOutputRelay(_logger, shortId, LogLevel.Information);
        var stderr = new ExecutorOutputRelay(_logger, shortId, LogLevel.Warning);
        Task<MultiplexedStream.ReadResult>? read = null;

        try
        {
            using var stream = await _docker.Containers.GetContainerLogsAsync(
                containerId,
                new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true
                },
                cancellationToken);

            var buffer = new byte[8192];
            read = stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);

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
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(exception, "Stopped streaming output from runner container {ContainerId} after cancellation.",
                containerId);
        }
        catch (Exception exception)
        {
            activity.RecordException(exception);
            _logger.LogWarning(exception, "Stopped streaming output from runner container {ContainerId}.",
                containerId);
        }
        finally
        {
            if (read is { IsCompleted: false })
            {
                _ = read.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }

            stdout.Complete();
            stderr.Complete();

            activity?.SetTag("container.stdout.entries", stdout.EntryCount);
            activity?.SetTag("container.stderr.entries", stderr.EntryCount);
        }
    }

    private async Task<bool> CleanupContainerAsync(
        string containerId,
        string runId,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested && await IsRunningAsync(containerId))
        {
            _logger.LogWarning(
                "Run {RunId} was cancelled while runner container {ContainerId} was running. The container was " +
                "left running so terraform can finish. The runner container sweep removes it once it has exited.",
                runId, containerId);
            return true;
        }

        await RemoveContainerAsync(containerId, CancellationToken.None);
        return false;
    }

    private async Task<bool> IsRunningAsync(string containerId)
    {
        try
        {
            var container = await _docker.Containers.InspectContainerAsync(containerId, CancellationToken.None);
            return container.State?.Running == true;
        }
        catch (DockerContainerNotFoundException)
        {
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Could not inspect runner container {ContainerId}; leaving it in place in case it is still running.",
                containerId);
            return true;
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
            _logger.LogWarning(exception, "Container {ContainerId} does not exist.", containerId);
        }
    }
}
