namespace Orchitect.Infrastructure.Engine.Secret;

public record Secret(string Name, string Value);

public interface ISecretProvider
{
    /// <summary>
    /// Returns a secret by name, or null when it does not exist.
    /// </summary>
    public Task<Secret?> GetAsync(string name, CancellationToken cancellationToken = default);
}