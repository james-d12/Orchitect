using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Core.Domain.Credential;

public interface ICredentialRepository : IRepository<Credential, CredentialId>
{
    IEnumerable<Credential> GetAllByOrganisationId(OrganisationId organisationId);
    Task<Credential?> UpdateAsync(Credential credential, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(CredentialId id, CancellationToken cancellationToken = default);
}
