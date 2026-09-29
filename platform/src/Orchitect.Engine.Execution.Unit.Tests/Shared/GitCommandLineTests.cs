using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Engine.Execution.Shared.CommandLine;

namespace Orchitect.Engine.Execution.Unit.Tests.Shared;

public sealed class GitCommandLineTests : IDisposable
{
    private const string FileName = "README.md";
    private const string FileContents = "orders";

    private readonly string _directory = Directory.CreateTempSubdirectory("orchitect-git-tests").FullName;
    private readonly GitCommandLine _git = new(NullLogger<GitCommandLine>.Instance);

    private string SourcePath => Path.Combine(_directory, "source");
    private Uri Source => new(SourcePath);
    private string Destination => Path.Combine(_directory, "destination");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task CloneAsync_ValidSource_ReturnsTrueAndChecksOutFiles()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await CreateSourceRepositoryAsync();

        var result = await _git.CloneAsync(Source, Destination);

        Assert.True(result);
        Assert.Equal(FileContents, await File.ReadAllTextAsync(Path.Combine(Destination, FileName)));
    }

    [Fact]
    public async Task CloneAsync_MissingSource_ReturnsFalse()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await _git.CloneAsync(Source, Destination);

        Assert.False(result);
    }

    [Fact]
    public async Task CloneTagAsync_ExistingTag_ReturnsTrue()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await CreateSourceRepositoryAsync();

        var result = await _git.CloneTagAsync(Source, "v1", Destination);

        Assert.True(result);
        Assert.True(File.Exists(Path.Combine(Destination, FileName)));
    }

    [Fact]
    public async Task CloneTagAsync_MissingTag_ReturnsFalse()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await CreateSourceRepositoryAsync();

        var result = await _git.CloneTagAsync(Source, "v2", Destination);

        Assert.False(result);
    }

    [Fact]
    public async Task CloneCommitAsync_ExistingCommit_ReturnsTrueAtThatCommit()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var commit = await CreateSourceRepositoryAsync();

        var result = await _git.CloneCommitAsync(Source, commit, Destination);

        Assert.True(result);
        Assert.Equal(commit, await RunGitAsync(Destination, "rev-parse", "HEAD"));
        Assert.Equal(FileContents, await File.ReadAllTextAsync(Path.Combine(Destination, FileName)));
    }

    [Fact]
    public async Task CloneCommitAsync_UnknownCommit_ReturnsFalse()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await CreateSourceRepositoryAsync();

        var result = await _git.CloneCommitAsync(Source, new string('0', 40), Destination);

        Assert.False(result);
    }

    private async Task<string> CreateSourceRepositoryAsync()
    {
        Directory.CreateDirectory(SourcePath);
        await File.WriteAllTextAsync(Path.Combine(SourcePath, FileName), FileContents);

        await RunGitAsync(SourcePath, "init");
        await RunGitAsync(SourcePath, "add", FileName);
        await RunGitAsync(SourcePath, "-c", "user.name=Orchitect", "-c", "user.email=tests@orchitect.dev",
            "commit", "-m", "initial");
        await RunGitAsync(SourcePath, "tag", "v1");

        return await RunGitAsync(SourcePath, "rev-parse", "HEAD");
    }

    private static async Task<string> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var result = await new CommandLineBuilder("git")
            .WithArguments(arguments)
            .WithWorkingDirectory(workingDirectory)
            .ExecuteAsync();

        Assert.True(result.ExitCode == 0, result.StdErr);

        return result.StdOut.Trim();
    }
}
