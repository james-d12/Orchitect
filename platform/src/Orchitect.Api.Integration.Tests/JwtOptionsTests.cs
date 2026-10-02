using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orchitect.Api.Settings;

namespace Orchitect.Api.Integration.Tests;

public sealed class JwtOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short-secret")]
    [InlineData("0123456789abcdef0123456789abcde")]
    public void AddJwtOptions_WhenSecretIsMissingOrShort_ShouldFailValidation(string? secret)
    {
        // Arrange
        using var provider = BuildProvider(secret);

        // Act
        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<JwtOptions>>().Value);

        // Assert
        Assert.Contains("JwtOptions:Secret", exception.Message);
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [InlineData("integration-test-jwt-secret-key-orchitect-platform")]
    public void AddJwtOptions_WhenSecretIsLongEnough_ShouldBind(string secret)
    {
        // Arrange
        using var provider = BuildProvider(secret);

        // Act
        var options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;

        // Assert
        Assert.Equal(secret, options.Secret);
    }

    [Fact]
    public void IsSecretLongEnough_WhenMultiByteCharactersReachMinimumBytes_ShouldReturnTrue()
    {
        // Arrange
        var secret = new string('é', JwtOptions.MinimumSecretBytes / 2);

        // Act
        var result = JwtOptions.IsSecretLongEnough(secret);

        // Assert
        Assert.True(result);
    }

    private static ServiceProvider BuildProvider(string? secret)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtOptions:Issuer"] = "orchitect-tests",
                ["JwtOptions:Audience"] = "orchitect-tests",
                ["JwtOptions:ExpirationInMinutes"] = "60",
                ["JwtOptions:Secret"] = secret
            })
            .Build();

        return new ServiceCollection()
            .AddJwtOptions(configuration)
            .BuildServiceProvider();
    }
}
