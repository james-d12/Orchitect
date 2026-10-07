using Microsoft.AspNetCore.Identity;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

public sealed record OrganisationMemberResponse(Guid Id, string UserId, string? Username, string? Email)
{
    public static OrganisationMemberResponse From(OrganisationUser member, IdentityUser? user) =>
        new(member.Id.Value, member.IdentityUserId, user?.UserName, user?.Email);
}
