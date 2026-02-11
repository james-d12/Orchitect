using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Shared;

namespace Orchitect.Core.Application.User;

public sealed class RegisterUserEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/register", HandleAsync)
        .WithSummary("Registers a new user.");

    private sealed record RegisterUserResponse(string Id, string Username, string Email);

    private sealed record RegisterUserRequest(string Username, string Email, string Password);

    private static async Task<Results<Ok<RegisterUserResponse>, BadRequest<ErrorResponse>>> HandleAsync(
        [FromBody]
        RegisterUserRequest request,
        [FromServices]
        UserManager<IdentityUser> userManager)
    {
        var user = new IdentityUser()
        {
            UserName = request.Username,
            Email = request.Email
        };
        var registeredUser = await userManager.CreateAsync(user, request.Password);

        if (!registeredUser.Succeeded)
        {
            var errors = registeredUser.Errors.Select(e => new Error()
            {
                Code = e.Code,
                Message = e.Description
            }).ToList();

            var errorResponse = new ErrorResponse() { Errors = errors };
            return TypedResults.BadRequest(errorResponse);
        }

        return TypedResults.Ok(new RegisterUserResponse(user.Id, user.UserName, user.Email));
    }
}