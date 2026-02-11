namespace Orchitect.Core.Domain.Organisation;

public sealed record OrganisationUser
{
    public required OrganisationUserId Id { get; init; }
    public required string IdentityUserId { get; init; }
    public required OrganisationId OrganisationId { get; init; }

    private OrganisationUser()
    {
    }

    public static OrganisationUser Create(string identityUserId, OrganisationId organisationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(identityUserId);

        return new OrganisationUser
        {
            Id = new OrganisationUserId(),
            IdentityUserId = identityUserId,
            OrganisationId = organisationId
        };
    }
}