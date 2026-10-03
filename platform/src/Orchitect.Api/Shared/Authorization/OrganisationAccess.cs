using Microsoft.AspNetCore.Http;
using Orchitect.Api.Extensions;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Shared.Authorization;

public sealed class OrganisationAccess : IOrganisationAccess
{
    private readonly IOrganisationRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private IReadOnlySet<OrganisationId>? _organisationIds;

    public OrganisationAccess(IOrganisationRepository repository, IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlySet<OrganisationId>> GetOrganisationIdsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_organisationIds is not null)
        {
            return _organisationIds;
        }

        var user = _httpContextAccessor.HttpContext?.User
                   ?? throw new InvalidOperationException("Organisation access requires an HTTP request.");

        var organisationIds = await _repository.GetIdsForMemberAsync(user.GetUserId(), cancellationToken);
        _organisationIds = organisationIds.ToHashSet();
        return _organisationIds;
    }

    public async Task<bool> IsMemberAsync(OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        var organisationIds = await GetOrganisationIdsAsync(cancellationToken);
        return organisationIds.Contains(organisationId);
    }
}