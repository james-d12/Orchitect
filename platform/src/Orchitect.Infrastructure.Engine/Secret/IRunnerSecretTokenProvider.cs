namespace Orchitect.Infrastructure.Engine.Secret;

public interface IRunnerSecretTokenProvider
{
    /// <summary>
    /// Returns runner environment variables holding a short-lived token the runner uses to read its secret provider.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetEnvironmentAsync(SecretProviderOptions options,
        CancellationToken cancellationToken = default);
}
