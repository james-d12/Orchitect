namespace Orchitect.Engine.Domain.Organisation;

public readonly record struct OrganisationUserId(Guid Value)
{
    public OrganisationUserId() : this(Guid.NewGuid())
    {
    }
}
