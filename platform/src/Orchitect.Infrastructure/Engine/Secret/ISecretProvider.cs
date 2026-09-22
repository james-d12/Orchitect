namespace Orchitect.Infrastructure.Engine.Secret;

public record Secret(string Name, string Value);

public interface ISecretProvider
{
    /// <summary>
    /// Returns a secret by name
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<Secret?> GetAsync(string name, CancellationToken cancellationToken = default);
}