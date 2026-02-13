namespace Orchitect.Core.Domain.Credential;

public readonly record struct CredentialId(Guid Value)
{
    public CredentialId() : this(Guid.NewGuid())
    {
    }
}
