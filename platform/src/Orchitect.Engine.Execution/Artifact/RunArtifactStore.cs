using System.Text;
using Microsoft.Extensions.Logging;
using Orchitect.Storage;

namespace Orchitect.Engine.Execution.Artifact;

public sealed class RunArtifactStore : IRunArtifactStore
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<RunArtifactStore> _logger;
    private readonly StorageKey _runKey;

    public RunArtifactStore(IStorageProvider storageProvider, ILogger<RunArtifactStore> logger, Guid runId)
    {
        _storageProvider = storageProvider;
        _logger = logger;
        _runKey = StorageKey.Create($"runs/{runId}");
    }

    public bool IsEnabled => true;

    public Task SaveTextAsync(RunArtifactKind kind, string name, string content,
        CancellationToken cancellationToken = default) =>
        SaveAsync(kind, name, () => new MemoryStream(Encoding.UTF8.GetBytes(content)), cancellationToken);

    public Task SaveFileAsync(RunArtifactKind kind, string name, string path,
        CancellationToken cancellationToken = default) =>
        SaveAsync(kind, name, () => File.OpenRead(path), cancellationToken);

    internal StorageKey GetKey(RunArtifactKind kind, string name) =>
        _runKey.Combine(kind.ToString().ToLowerInvariant(), name);

    private async Task SaveAsync(RunArtifactKind kind, string name, Func<Stream> open,
        CancellationToken cancellationToken)
    {
        try
        {
            var key = GetKey(kind, name);

            await using var content = open();
            await _storageProvider.WriteAsync(key, content, cancellationToken);

            _logger.LogDebug("Saved run artifact {ArtifactKey}", key);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Could not save {ArtifactKind} artifact {ArtifactName} of the run.", kind,
                name);
        }
    }
}
