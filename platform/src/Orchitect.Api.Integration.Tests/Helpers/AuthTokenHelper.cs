using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Orchitect.Api.Endpoints.Core.User;

namespace Orchitect.Api.Integration.Tests.Helpers;

/// <summary>
/// This class will register a user and get a token
/// that can be used in all tests, except the UserIntegrationTests
/// </summary>
public static class AuthTokenHelper
{
    private const string UsersUrl = "/users";
    private static string _accessToken = string.Empty;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<HttpClient> AddAuthorisationHeader(this HttpClient client)
    {
        var token = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> GetAccessTokenAsync(HttpClient client)
    {
        if (!string.IsNullOrEmpty(_accessToken))
        {
            return _accessToken;
        }

        var registerUserRequest = new RegisterUserEndpoint.RegisterUserRequest(
            Username: $"user_{Guid.NewGuid():N}"[..16],
            Email: "test@example.com",
            Password: "Password123!");

        await client.PostAsJsonAsync($"{UsersUrl}/register", registerUserRequest);

        var loginUserRequest = new LoginUserEndpoint.LoginUserRequest(
            Email: registerUserRequest.Email,
            Password: registerUserRequest.Password);

        var loginResponse = await client.PostAsJsonAsync($"{UsersUrl}/login", loginUserRequest);
        var body = await loginResponse.Content.ReadFromJsonAsync<LoginUserEndpoint.LoginUserResponse>(JsonOptions);

        ArgumentException.ThrowIfNullOrEmpty(body?.AccessToken);

        var accessToken = body.AccessToken;
        _accessToken = accessToken;
        return accessToken;
    }
}