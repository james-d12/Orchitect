namespace Conductor.Engine.Domain.Organisation;

public readonly record struct OrganisationServiceId(Guid Value)
{
    public OrganisationServiceId() : this(Guid.NewGuid())
    {
    }
}