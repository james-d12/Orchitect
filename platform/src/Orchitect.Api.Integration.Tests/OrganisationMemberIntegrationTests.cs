using System.Net;
using Orchitect.Api.Endpoints.Core.Organisation;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Api.Shared;

namespace Orchitect.Api.Integration.Tests;

public sealed class OrganisationMemberIntegrationTests(WebApplicationFactoryWithPostgres factory) : IClassFixture<WebApplicationFactoryWithPostgres>
{
    private static string MembersUrl(Guid organisationId) => $"/organisations/{organisationId}/members";

    private async Task<(HttpClient Client, Guid OrganisationId)> CreateOrganisationAsync()
    {
        var client = await factory.CreateClient().AddAuthorisationHeaderForNewUser();
        var organisation = await client.CreateOrganisationAsync();
        return (client, organisation.Id);
    }

    private async Task<(HttpClient Client, string Email)> CreateUserAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var client = await factory.CreateClient().AddAuthorisationHeaderForNewUser(email);
        return (client, email);
    }

    private static async Task<OrganisationMemberResponse> AddMemberAsync(HttpClient client, Guid organisationId,
        string email)
    {
        var response = await client.PostAsJsonAsync(MembersUrl(organisationId),
            new AddOrganisationMemberEndpoint.AddOrganisationMemberRequest(email));
        var member = await response.ReadFromJsonAsync<OrganisationMemberResponse>();
        ArgumentNullException.ThrowIfNull(member);
        return member;
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenGettingMembersOfNewOrganisation_ShouldReturnCreator()
    {
        // Arrange
        var (creator, creatorEmail) = await CreateUserAsync();
        var organisation = await creator.CreateOrganisationAsync();

        // Act
        var response = await creator.GetAsync(MembersUrl(organisation.Id));
        var body = await response.ReadFromJsonAsync<GetOrganisationMembersEndpoint.GetOrganisationMembersResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var member = Assert.Single(body.Members);
        Assert.Equal(creatorEmail, member.Email);
        Assert.NotNull(member.Username);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenAddingMember_ShouldGiveThemAccess()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();
        var (newMember, email) = await CreateUserAsync();

        // Act
        var response = await client.PostAsJsonAsync(MembersUrl(organisationId),
            new AddOrganisationMemberEndpoint.AddOrganisationMemberRequest(email));
        var body = await response.ReadFromJsonAsync<OrganisationMemberResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(email, body.Email);

        var members = await (await client.GetAsync(MembersUrl(organisationId)))
            .ReadFromJsonAsync<GetOrganisationMembersEndpoint.GetOrganisationMembersResponse>();
        Assert.NotNull(members);
        Assert.Equal(2, members.Members.Count);
        Assert.Contains(members.Members, m => m.Id == body.Id);

        var organisationResponse = await newMember.GetAsync($"/organisations/{organisationId}");
        Assert.Equal(HttpStatusCode.OK, organisationResponse.StatusCode);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenAddingUnknownEmail_ShouldReturn400BadRequest()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();

        // Act
        var response = await client.PostAsJsonAsync(MembersUrl(organisationId),
            new AddOrganisationMemberEndpoint.AddOrganisationMemberRequest($"{Guid.NewGuid():N}@example.com"));
        var body = await response.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("USER_NOT_FOUND", Assert.Single(body.Errors).Code);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenAddingExistingMember_ShouldReturn409Conflict()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();
        var (_, email) = await CreateUserAsync();
        await AddMemberAsync(client, organisationId, email);

        // Act
        var response = await client.PostAsJsonAsync(MembersUrl(organisationId),
            new AddOrganisationMemberEndpoint.AddOrganisationMemberRequest(email));
        var body = await response.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("ALREADY_A_MEMBER", Assert.Single(body.Errors).Code);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenRemovingMember_ShouldRevokeTheirAccess()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();
        var (removedUser, email) = await CreateUserAsync();
        var member = await AddMemberAsync(client, organisationId, email);

        // Act
        var response = await client.DeleteAsync($"{MembersUrl(organisationId)}/{member.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var members = await (await client.GetAsync(MembersUrl(organisationId)))
            .ReadFromJsonAsync<GetOrganisationMembersEndpoint.GetOrganisationMembersResponse>();
        Assert.NotNull(members);
        Assert.DoesNotContain(members.Members, m => m.Id == member.Id);

        var organisationResponse = await removedUser.GetAsync($"/organisations/{organisationId}");
        Assert.Equal(HttpStatusCode.NotFound, organisationResponse.StatusCode);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenMemberRemovesThemselves_ShouldReturn204NoContent()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();
        var (leaver, email) = await CreateUserAsync();
        var member = await AddMemberAsync(client, organisationId, email);

        // Act
        var response = await leaver.DeleteAsync($"{MembersUrl(organisationId)}/{member.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await leaver.GetAsync(MembersUrl(organisationId))).StatusCode);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenRemovingLastMember_ShouldReturn409Conflict()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();
        var members = await (await client.GetAsync(MembersUrl(organisationId)))
            .ReadFromJsonAsync<GetOrganisationMembersEndpoint.GetOrganisationMembersResponse>();
        Assert.NotNull(members);
        var lastMember = Assert.Single(members.Members);

        // Act
        var response = await client.DeleteAsync($"{MembersUrl(organisationId)}/{lastMember.Id}");
        var body = await response.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("LAST_MEMBER", Assert.Single(body.Errors).Code);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenRemovingUnknownMember_ShouldReturn404NotFound()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();

        // Act
        var response = await client.DeleteAsync($"{MembersUrl(organisationId)}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenRemovingMemberOfAnotherOrganisation_ShouldReturn404NotFound()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();
        var otherOrganisation = await client.CreateOrganisationAsync();
        var (_, email) = await CreateUserAsync();
        var otherMember = await AddMemberAsync(client, otherOrganisation.Id, email);

        // Act
        var response = await client.DeleteAsync($"{MembersUrl(organisationId)}/{otherMember.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrganisationMembersApi_WhenNotAMember_ShouldReturn404NotFound()
    {
        // Arrange
        var (client, organisationId) = await CreateOrganisationAsync();
        var (outsider, outsiderEmail) = await CreateUserAsync();
        var members = await (await client.GetAsync(MembersUrl(organisationId)))
            .ReadFromJsonAsync<GetOrganisationMembersEndpoint.GetOrganisationMembersResponse>();
        Assert.NotNull(members);
        var memberId = Assert.Single(members.Members).Id;

        // Act
        var getResponse = await outsider.GetAsync(MembersUrl(organisationId));
        var addResponse = await outsider.PostAsJsonAsync(MembersUrl(organisationId),
            new AddOrganisationMemberEndpoint.AddOrganisationMemberRequest(outsiderEmail));
        var removeResponse = await outsider.DeleteAsync($"{MembersUrl(organisationId)}/{memberId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, addResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removeResponse.StatusCode);
        Assert.Single((await (await client.GetAsync(MembersUrl(organisationId)))
            .ReadFromJsonAsync<GetOrganisationMembersEndpoint.GetOrganisationMembersResponse>())!.Members);
    }
}
