namespace Orchitect.Domain.Core.Organisation;

public readonly record struct OrganisationId(Guid Value)
{
    public OrganisationId() : this(Guid.NewGuid())
    {
    }
}