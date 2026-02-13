using System.Text.Json;
using Orchitect.Core.Domain.Credential.Payloads;

namespace Orchitect.Core.Domain.Credential;

public sealed class CredentialPayloadResolver(IEncryptionService encryptionService)
{
    public T Resolve<T>(Credential credential) where T : class
    {
        var decryptedJson = encryptionService.Decrypt(credential.EncryptedPayload);
        return JsonSerializer.Deserialize<T>(decryptedJson)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize credential '{credential.Name}' payload as {typeof(T).Name}.");
    }

    public PersonalAccessTokenPayload ResolvePersonalAccessToken(Credential credential)
    {
        ValidateType(credential, CredentialType.PersonalAccessToken);
        return Resolve<PersonalAccessTokenPayload>(credential);
    }

    public OAuthPayload ResolveOAuth(Credential credential)
    {
        ValidateType(credential, CredentialType.OAuth);
        return Resolve<OAuthPayload>(credential);
    }

    public ServicePrincipalPayload ResolveServicePrincipal(Credential credential)
    {
        ValidateType(credential, CredentialType.ServicePrincipal);
        return Resolve<ServicePrincipalPayload>(credential);
    }

    public BasicAuthPayload ResolveBasicAuth(Credential credential)
    {
        ValidateType(credential, CredentialType.BasicAuth);
        return Resolve<BasicAuthPayload>(credential);
    }

    private static void ValidateType(Credential credential, CredentialType expectedType)
    {
        if (credential.Type != expectedType)
        {
            throw new InvalidOperationException(
                $"Credential '{credential.Name}' is of type {credential.Type}, expected {expectedType}.");
        }
    }
}
