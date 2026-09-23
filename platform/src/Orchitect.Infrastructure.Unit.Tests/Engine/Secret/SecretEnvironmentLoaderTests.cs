using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchitect.Infrastructure.Engine.Secret;
using SecretValue = Orchitect.Infrastructure.Engine.Secret.Secret;

namespace Orchitect.Infrastructure.Unit.Tests.Engine.Secret;

public sealed class SecretEnvironmentLoaderTests
{
    [Fact]
    public async Task LoadAsync_SetsEnvironmentVariablesFromSecrets()
    {
        var variable = $"ORCHITECT_TEST_{Guid.NewGuid():N}";
        var loader = CreateLoader(new Dictionary<string, string> { [variable] = "arm-client-secret" },
            new Dictionary<string, string> { ["arm-client-secret"] = "s3cr3t" });

        try
        {
            await loader.LoadAsync();

            Assert.Equal("s3cr3t", Environment.GetEnvironmentVariable(variable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task LoadAsync_MissingSecret_Throws()
    {
        var variable = $"ORCHITECT_TEST_{Guid.NewGuid():N}";
        var loader = CreateLoader(new Dictionary<string, string> { [variable] = "does-not-exist" }, []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync());

        Assert.Contains("does-not-exist", exception.Message);
        Assert.Null(Environment.GetEnvironmentVariable(variable));
    }

    private static SecretEnvironmentLoader CreateLoader(Dictionary<string, string> mappings,
        Dictionary<string, string> secrets) =>
        new(NullLogger<SecretEnvironmentLoader>.Instance, new InMemorySecretProvider(secrets),
            Options.Create(new SecretProviderOptions { Mappings = mappings }));

    private sealed class InMemorySecretProvider(Dictionary<string, string> secrets) : ISecretProvider
    {
        public Task<SecretValue?> GetAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(secrets.TryGetValue(name, out var value) ? new SecretValue(name, value) : null);
    }
}
