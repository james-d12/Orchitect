using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Dispatch.Auth;

namespace Orchitect.Api.Authentication;

public sealed class RunnerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string BearerPrefix = "Bearer ";

    private readonly IDeploymentRunRepository _runs;

    public RunnerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDeploymentRunRepository runs)
        : base(options, logger, encoder)
    {
        _runs = runs;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header[BearerPrefix.Length..].Trim();

        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var run = await _runs.GetByTokenHashAsync(RunnerToken.ComputeHash(token), Context.RequestAborted);

        if (run is null)
        {
            return AuthenticateResult.Fail("The runner token is not valid.");
        }

        if (!run.HasValidToken(TimeProvider.GetUtcNow().UtcDateTime))
        {
            return AuthenticateResult.Fail("The runner token has expired or its run is no longer active.");
        }

        if (!Guid.TryParse(Context.GetRouteValue(RunnerAuthenticationDefaults.RunIdRouteValue)?.ToString(),
                out var routeRunId) || routeRunId != run.Id.Value)
        {
            return AuthenticateResult.Fail("The runner token is not valid for this run.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(RunnerAuthenticationDefaults.RunIdClaim, run.Id.Value.ToString())], Scheme.Name);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
