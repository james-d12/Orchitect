namespace Conductor.Engine.Domain.Organisation;

public readonly record struct OrganisationTeamId(Guid Value)
{
    public OrganisationTeamId() : this(Guid.NewGuid())
    {
    }
}