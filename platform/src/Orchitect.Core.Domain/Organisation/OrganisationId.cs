namespace Orchitect.Core.Domain.Organisation;

public readonly record struct OrganisationId(Guid Value)
{
    public OrganisationId() : this(Guid.NewGuid())
    {
    }
}