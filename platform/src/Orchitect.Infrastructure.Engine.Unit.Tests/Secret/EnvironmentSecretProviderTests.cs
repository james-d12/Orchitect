using Orchitect.Infrastructure.Engine.Secret;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Secret;

public sealed class EnvironmentSecretProviderTests
{
    [Fact]
    public async Task GetAsync_VariableSet_ReturnsItsValue()
    {
        var variable = $"ORCHITECT_TEST_{Guid.NewGuid():N}";
        System.Environment.SetEnvironmentVariable(variable, "s3cr3t");

        try
        {
            var secret = await new EnvironmentSecretProvider().GetAsync(variable);

            Assert.NotNull(secret);
            Assert.Equal(variable, secret.Name);
            Assert.Equal("s3cr3t", secret.Value);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task GetAsync_VariableNotSet_ReturnsNull()
    {
        var secret = await new EnvironmentSecretProvider().GetAsync($"ORCHITECT_TEST_{Guid.NewGuid():N}");

        Assert.Null(secret);
    }
}
