using Microsoft.Extensions.Logging;

namespace Conductor.Engine.Infrastructure.CommandLine;

public interface IGitCommandLine
{
    Task<bool> CloneAsync(Uri source, string destination);
    Task<bool> CloneTagAsync(Uri source, string tag, string destination);
    Task<bool> CloneCommitAsync(Uri source, string commit, string destination);
}

public sealed class GitCommandLine : IGitCommandLine
{
    private readonly ILogger<GitCommandLine> _logger;

    public GitCommandLine(ILogger<GitCommandLine> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CloneAsync(Uri source, string destination)
    {
        var arguments =
            $"clone --depth 1 --single-branch --no-tags --no-recurse-submodules \"{source}\" \"{destination}\"";
        CommandLineResult cliResult =
            await new CommandLineBuilder("git")
                .WithArguments(arguments)
                .ExecuteAsync();

        _logger.LogDebug("Git clone output for {Source}:\n{StdOut}", source, cliResult.StdOut);

        if (cliResult.ExitCode != 0)
        {
            _logger.LogWarning("Could not Clone {Source} Due to {Error}", source, cliResult.StdErr);
        }

        return Directory.Exists(destination);
    }

    public async Task<bool> CloneTagAsync(Uri source, string tag, string destination)
    {
        var arguments =
            $"clone --branch \"{tag}\" --depth 1 --single-branch --no-tags --no-recurse-submodules \"{source}\" \"{destination}\"";
        CommandLineResult cliResult =
            await new CommandLineBuilder("git")
                .WithArguments(arguments)
                .ExecuteAsync();

        _logger.LogDebug("Git clone output for {Source}:\n{StdOut}", source, cliResult.StdOut);

        if (cliResult.ExitCode != 0)
        {
            _logger.LogWarning("Could not Clone {Source} Due to {Error}", source, cliResult.StdErr);
        }

        return Directory.Exists(destination);
    }

    public async Task<bool> CloneCommitAsync(Uri source, string commit, string destination)
    {
        var initResult = await new CommandLineBuilder("git")
            .WithArguments($"init \"{destination}\"")
            .ExecuteAsync();

        if (initResult.ExitCode != 0)
        {
            _logger.LogWarning("Could not Git Init {Source} Due to {Error}", source, initResult.StdErr);
        }

        var remoteAddResult = await new CommandLineBuilder("git")
            .WithArguments($"remote add origin \"{source}\"")
            .WithWorkingDirectory(destination)
            .ExecuteAsync();

        if (remoteAddResult.ExitCode != 0)
        {
            _logger.LogWarning("Could not Remote Add Origin {Source} Due to {Error}", source, remoteAddResult.StdErr);
        }

        var fetchResult = await new CommandLineBuilder("git")
            .WithArguments($"fetch --depth 1 origin \"{commit}\"")
            .WithWorkingDirectory(destination)
            .ExecuteAsync();

        if (fetchResult.ExitCode != 0)
        {
            _logger.LogWarning("Could not Fetch Depth 1 {Source} Due to {Error}", source, fetchResult.StdErr);
        }

        var checkoutResult = await new CommandLineBuilder("git")
            .WithArguments($"checkout FETCH_HEAD")
            .WithWorkingDirectory(destination)
            .ExecuteAsync();

        if (checkoutResult.ExitCode != 0)
        {
            _logger.LogWarning("Could not Checkout {Source} Due to {Error}", source, checkoutResult.StdErr);
        }

        return Directory.Exists(destination) && checkoutResult.ExitCode == 0;
    }
}