using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Shared.CommandLine;

namespace Orchitect.Engine.Execution.Unit.Tests.Score;

public sealed class ScoreDriverTests : IDisposable
{
    private const string ScoreYaml =
        """
        apiVersion: score.dev/v1b1
        metadata:
          name: orders
        containers:
          web:
            image: nginx
        resources:
          storage:
            type: azure-storage-account
            class: standard
            id: orders-storage
            metadata:
              annotations:
                team: payments
            parameters:
              name: orders
        """;

    private readonly ScoreGitCommandLine _git = new();

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task ParseAsync_ValidScoreFile_DeserialisesResources()
    {
        _git.ScoreFile = Path.Combine("deploy", "score.yaml");
        _git.ScoreContents = ScoreYaml;
        var run = NewRun();

        var scoreFile = await CreateDriver().ParseAsync(run, CancellationToken.None);

        Assert.NotNull(scoreFile);
        Assert.Equal("score.dev/v1b1", scoreFile.ApiVersion);
        Assert.Equal("orders", scoreFile.Metadata.Name);
        var resource = Assert.Single(scoreFile.Resources!);
        Assert.Equal("storage", resource.Key);
        Assert.Equal("azure-storage-account", resource.Value.Type);
        Assert.Equal("standard", resource.Value.Class);
        Assert.Equal("orders-storage", resource.Value.Id);
        Assert.Equal("payments", resource.Value.Metadata?.Annotations?["team"]);
        Assert.Equal("orders", resource.Value.Parameters?["name"]);
        Assert.Equal(run.RepositoryUrl, _git.Source);
        Assert.Equal(run.CommitId, _git.Commit);
    }

    [Fact]
    public async Task ParseAsync_CloneFails_ReturnsNull()
    {
        _git.Succeeds = false;

        var scoreFile = await CreateDriver().ParseAsync(NewRun(), CancellationToken.None);

        Assert.Null(scoreFile);
    }

    [Fact]
    public async Task ParseAsync_NoScoreFile_ReturnsNull()
    {
        var scoreFile = await CreateDriver().ParseAsync(NewRun(), CancellationToken.None);

        Assert.Null(scoreFile);
        Assert.NotNull(_git.Destination);
    }

    private ScoreDriver CreateDriver() => new(NullLogger<ScoreDriver>.Instance, _git);

    private static RunDescriptor NewRun() =>
        new(Guid.NewGuid(), RunnerOperation.Provision, new Uri("https://example.com/orders.git"),
            new string('a', 40), Guid.NewGuid(), Guid.NewGuid());

    private sealed class ScoreGitCommandLine : IGitCommandLine, IDisposable
    {
        public bool Succeeds { get; set; } = true;
        public string? ScoreFile { get; set; }
        public string ScoreContents { get; set; } = string.Empty;
        public Uri? Source { get; private set; }
        public string? Commit { get; private set; }
        public string? Destination { get; private set; }

        public Task<bool> CloneAsync(Uri source, string destination) => throw new NotSupportedException();

        public Task<bool> CloneTagAsync(Uri source, string tag, string destination) =>
            throw new NotSupportedException();

        public async Task<bool> CloneCommitAsync(Uri source, string commit, string destination)
        {
            Source = source;
            Commit = commit;
            Destination = destination;

            if (!Succeeds)
            {
                return false;
            }

            Directory.CreateDirectory(destination);

            if (ScoreFile is not null)
            {
                var path = Path.Combine(destination, ScoreFile);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, ScoreContents);
            }

            return true;
        }

        public void Dispose()
        {
            if (Destination is not null && Directory.Exists(Destination))
            {
                Directory.Delete(Destination, recursive: true);
            }
        }
    }
}
