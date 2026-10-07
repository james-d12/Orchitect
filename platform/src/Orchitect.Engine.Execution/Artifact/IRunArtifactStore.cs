namespace Orchitect.Engine.Execution.Artifact;

public enum RunArtifactKind
{
    Plan,
    Log
}

public interface IRunArtifactStore
{
    /// <summary>
    /// Whether artifacts are kept anywhere. When false, saving does nothing.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Saves text as an artifact of the current run. Failures are logged, not thrown.
    /// </summary>
    Task SaveTextAsync(RunArtifactKind kind, string name, string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a file as an artifact of the current run. Failures are logged, not thrown.
    /// </summary>
    Task SaveFileAsync(RunArtifactKind kind, string name, string path, CancellationToken cancellationToken = default);
}
