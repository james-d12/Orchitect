using Orchitect.Storage;

namespace Orchitect.Engine.Dispatch.Storage;

public interface IRunnerStorageTokenProvider
{
    /// <summary>
    /// Returns runner environment variables holding a short-lived token the runner uses to write its artifacts.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetEnvironmentAsync(StorageOptions options,
        CancellationToken cancellationToken = default);
}
