using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orchitect.Infrastructure.Engine.Secret;

public interface ISecretEnvironmentLoader
{
    /// <summary>
    /// Loads the mapped secrets into the process environment so terraform inherits them.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class SecretEnvironmentLoader : ISecretEnvironmentLoader
{
    private readonly ILogger<SecretEnvironmentLoader> _logger;
    private readonly ISecretProvider _secretProvider;
    private readonly SecretProviderOptions _options;

    public SecretEnvironmentLoader(ILogger<SecretEnvironmentLoader> logger, ISecretProvider secretProvider,
        IOptions<SecretProviderOptions> options)
    {
        _logger = logger;
        _secretProvider = secretProvider;
        _options = options.Value;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (variable, secretName) in _options.Mappings)
        {
            Secret? secret = await _secretProvider.GetAsync(secretName, cancellationToken);

            if (secret is null)
            {
                throw new InvalidOperationException(
                    $"Secret '{secretName}' for environment variable '{variable}' was not found " +
                    $"using secret provider '{_options.Type}'.");
            }

            Environment.SetEnvironmentVariable(variable, secret.Value);
            _logger.LogInformation("Loaded secret {SecretName} into {Variable}", secretName, variable);
        }
    }
}
