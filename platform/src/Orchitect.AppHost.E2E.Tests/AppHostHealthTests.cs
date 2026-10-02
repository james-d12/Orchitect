using System.Net;
using Orchitect.AppHost.E2E.Tests.Helpers;

namespace Orchitect.AppHost.E2E.Tests;

[Collection("AppHost")]
public sealed class AppHostHealthTests(AppHostFixture fixture)
{
    [Theory]
    [InlineData(AppHostResources.Postgres)]
    [InlineData(AppHostResources.Database)]
    [InlineData(AppHostResources.Api)]
    [InlineData(AppHostResources.PortalWeb)]
    public async Task AppHost_WhenStarted_ResourceShouldBecomeHealthy(string resourceName)
    {
        // Arrange
        using var cts = new CancellationTokenSource(AppHostFixture.StartupTimeout);

        // Act
        var resourceEvent = await fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync(resourceName, cts.Token);

        // Assert
        Assert.Equal(KnownResourceStates.Running, resourceEvent.Snapshot.State?.Text);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Api_WhenHealthy_HealthEndpointShouldReturn200Ok(string path)
    {
        // Arrange
        using var cts = new CancellationTokenSource(AppHostFixture.StartupTimeout);
        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync(AppHostResources.Api, cts.Token);
        using var client = fixture.App.CreateHttpClient(AppHostResources.Api, "http");

        // Act
        using var response = await client.GetAsync(path, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PortalWeb_WhenHealthy_RootShouldReturn200Ok()
    {
        // Arrange
        using var cts = new CancellationTokenSource(AppHostFixture.StartupTimeout);
        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync(AppHostResources.PortalWeb, cts.Token);
        using var client = fixture.App.CreateHttpClient(AppHostResources.PortalWeb, "http");

        // Act
        using var response = await client.GetAsync("/", cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

}
