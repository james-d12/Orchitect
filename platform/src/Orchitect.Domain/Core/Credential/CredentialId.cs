namespace Orchitect.Domain.Core.Credential;

public readonly record struct CredentialId(Guid Value)
{
    public CredentialId() : this(Guid.NewGuid())
    {
    }
}
