namespace Orchitect.Infrastructure.Engine.Secret;

public sealed class EnvironmentSecretProvider : ISecretProvider
{
    public Task<Secret?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return Task.FromResult(string.IsNullOrEmpty(value) ? null : new Secret(name, value));
    }
}
