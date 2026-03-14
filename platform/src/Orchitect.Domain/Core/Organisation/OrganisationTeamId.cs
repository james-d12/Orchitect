namespace Orchitect.Domain.Core.Organisation;

public readonly record struct OrganisationTeamId(Guid Value)
{
    public OrganisationTeamId() : this(Guid.NewGuid())
    {
    }
}