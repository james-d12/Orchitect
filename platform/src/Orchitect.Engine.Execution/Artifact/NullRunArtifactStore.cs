namespace Orchitect.Engine.Execution.Artifact;

public sealed class NullRunArtifactStore : IRunArtifactStore
{
    public static readonly NullRunArtifactStore Instance = new();

    private NullRunArtifactStore()
    {
    }

    public bool IsEnabled => false;

    public Task SaveTextAsync(RunArtifactKind kind, string name, string content,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SaveFileAsync(RunArtifactKind kind, string name, string path,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
