namespace Orchitect.Api.Authentication;

/// <summary>Values that mark a JWT as an Orchitect user access token (RFC 9068), set at issue and required at validation.</summary>
public static class AccessTokenDefaults
{
    public const string TokenType = "at+jwt";
    public const string ScopeClaim = "scope";
    public const string Scope = "orchitect.api";
}
