using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace Orchitect.Infrastructure.Engine.Runner;

public sealed class DockerRunner : IRunner
{
    private readonly ILogger<DockerRunner> _logger;
    private readonly DockerClient _docker;

    public DockerRunner(ILogger<DockerRunner> logger, DockerClient docker)
    {
        _logger = logger;
        _docker = docker;
    }

    public async Task ExecuteAsync(
        RunnerContext context,
        CancellationToken cancellationToken = default)
    {
        await EnsureImageExistsAsync(context.Image, cancellationToken);

        var container = await _docker.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Name = $"orchitect-runner-{context.RunId}",
                Image = context.Image,
                Cmd = context.Arguments.ToList(),
                Env =
                [
                    $"ORCHITECT_RUN_ID={context.RunId}",
                    ..context.Configuration.Select(x => $"{x.Key}={x.Value}")
                ],
                HostConfig = new HostConfig
                {
                    // Allows the runner to reach services exposed on the Docker host (e.g. the database).
                    ExtraHosts = ["host.docker.internal:host-gateway"]
                }
            },
            cancellationToken);

        try
        {
            var started = await _docker.Containers.StartContainerAsync(
                container.ID,
                new ContainerStartParameters(),
                cancellationToken);

            if (!started)
            {
                throw new InvalidOperationException(
                    $"Failed to start runner container '{container.ID}'.");
            }

            var wait = await _docker.Containers.WaitContainerAsync(
                container.ID,
                cancellationToken);

            await LogContainerOutputAsync(container.ID, cancellationToken);

            if (wait.StatusCode != 0)
            {
                throw new InvalidOperationException(
                    $"Runner '{context.RunId}' failed with exit code {wait.StatusCode}.");
            }
        }
        finally
        {
            // Not using the caller's token, so the container is still removed when the run is cancelled.
            await RemoveContainerAsync(container.ID, CancellationToken.None);
        }
    }

    private async Task EnsureImageExistsAsync(
        string image,
        CancellationToken cancellationToken)
    {
        try
        {
            await _docker.Images.InspectImageAsync(image, cancellationToken);
        }
        catch (DockerImageNotFoundException)
        {
            throw new InvalidOperationException(
                $"Runner image '{image}' does not exist.");
        }
    }

    private async Task LogContainerOutputAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        using var stream = await _docker.Containers.GetContainerLogsAsync(
            containerId,
            tty: false,
            new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true
            },
            cancellationToken);

        var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            _logger.LogInformation("Runner {ContainerId} output:\n{Output}", containerId, stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogWarning("Runner {ContainerId} error output:\n{Output}", containerId, stderr);
        }
    }

    private async Task RemoveContainerAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _docker.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters
                {
                    Force = true
                },
                cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            // Already removed.
        }
    }
}
