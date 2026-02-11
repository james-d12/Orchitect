namespace Orchitect.Core.Domain.Organisation;

public readonly record struct OrganisationUserId(Guid Value)
{
    public OrganisationUserId() : this(Guid.NewGuid())
    {
    }
}
