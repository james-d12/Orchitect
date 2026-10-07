using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchitect.Engine.Execution.Artifact;
using Orchitect.Storage;
using Orchitect.Storage.FileSystem;

namespace Orchitect.Engine.Execution.Unit.Tests.Artifact;

public sealed class RunArtifactStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"orchitect-artifacts-{Guid.NewGuid():N}");
    private readonly Guid _runId = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveTextAsync_WritesUnderTheRunKindAndName()
    {
        var store = CreateStore(new FileSystemStorageProvider(new FileSystemStorageOptions { RootPath = _root }));

        await store.SaveTextAsync(RunArtifactKind.Log, "terraform-init.log", "initialised");

        Assert.Equal("initialised",
            await File.ReadAllTextAsync(Path.Combine(_root, "runs", _runId.ToString(), "log", "terraform-init.log")));
    }

    [Fact]
    public async Task SaveFileAsync_CopiesTheFile()
    {
        var source = Path.Combine(Path.GetTempPath(), $"plan-{Guid.NewGuid():N}.tfplan");
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        var store = CreateStore(new FileSystemStorageProvider(new FileSystemStorageOptions { RootPath = _root }));

        try
        {
            await store.SaveFileAsync(RunArtifactKind.Plan, "plan.tfplan", source);
        }
        finally
        {
            File.Delete(source);
        }

        Assert.Equal([1, 2, 3],
            await File.ReadAllBytesAsync(Path.Combine(_root, "runs", _runId.ToString(), "plan", "plan.tfplan")));
    }

    [Fact]
    public void GetKey_UsesTheLowercaseKind()
    {
        var store = CreateStore(Substitute.For<IStorageProvider>());

        Assert.Equal($"runs/{_runId}/plan/plan.json", store.GetKey(RunArtifactKind.Plan, "plan.json").Value);
    }

    [Fact]
    public async Task SaveTextAsync_ProviderThrows_DoesNotThrow()
    {
        var provider = Substitute.For<IStorageProvider>();
        provider.WriteAsync(Arg.Any<StorageKey>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("disk full"));

        await CreateStore(provider).SaveTextAsync(RunArtifactKind.Log, "terraform-plan.log", "planned");

        await provider.Received(1).WriteAsync(Arg.Any<StorageKey>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveFileAsync_MissingFile_DoesNotThrow()
    {
        var provider = Substitute.For<IStorageProvider>();

        await CreateStore(provider).SaveFileAsync(RunArtifactKind.Plan, "plan.tfplan",
            Path.Combine(_root, "missing.tfplan"));

        await provider.DidNotReceiveWithAnyArgs().WriteAsync(null!, null!, CancellationToken.None);
    }

    [Theory]
    [InlineData("../escape.log")]
    [InlineData("")]
    public async Task SaveTextAsync_InvalidName_DoesNotThrowOrWrite(string name)
    {
        var provider = Substitute.For<IStorageProvider>();

        await CreateStore(provider).SaveTextAsync(RunArtifactKind.Log, name, "content");

        await provider.DidNotReceiveWithAnyArgs().WriteAsync(null!, null!, CancellationToken.None);
    }

    [Fact]
    public async Task SaveTextAsync_Cancelled_Throws()
    {
        var provider = Substitute.For<IStorageProvider>();
        provider.WriteAsync(Arg.Any<StorageKey>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateStore(provider).SaveTextAsync(RunArtifactKind.Log, "terraform-plan.log", "planned",
                cancellation.Token));
    }

    private RunArtifactStore CreateStore(IStorageProvider provider) =>
        new(provider, NullLogger<RunArtifactStore>.Instance, _runId);
}
