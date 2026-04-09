using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Core.User;
using Orchitect.Api.Integration.Tests.Helpers;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class UserIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string UsersUrl = "/users";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task UserApi_WhenRegisteringUser_ShouldReturn200Ok()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = _fixture.Create<RegisterUserEndpoint.RegisterUserRequest>();

        // Act
        var response = await client.PostAsJsonAsync($"{UsersUrl}/register", request);
        var body = await response.ReadFromJsonAsync<RegisterUserEndpoint.RegisterUserResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Id);
        Assert.Equal(request.Username, body.Username);
        Assert.Equal(request.Email, body.Email);
    }

    [Fact]
    public async Task UserApi_WhenLoggingInWithValidCredentials_ShouldReturn200OkWithToken()
    {
        // Arrange
        var client = factory.CreateClient();
        var registerUserRequest = _fixture.Create<RegisterUserEndpoint.RegisterUserRequest>();
        var loginUserRequest = new LoginUserEndpoint.LoginUserRequest(
            Email: registerUserRequest.Email,
            Password: registerUserRequest.Password);

        // Act
        var registerResponse = await client.PostAsJsonAsync($"{UsersUrl}/register", registerUserRequest);
        var loginResponse = await client.PostAsJsonAsync($"{UsersUrl}/login", loginUserRequest);
        var body = await loginResponse.ReadFromJsonAsync<LoginUserEndpoint.LoginUserResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
    }

    [Fact]
    public async Task UserApi_WhenLoggingInWithInvalidCredentials_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var loginRequest = _fixture.Create<LoginUserEndpoint.LoginUserRequest>();

        // Act
        var response = await client.PostAsJsonAsync($"{UsersUrl}/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}