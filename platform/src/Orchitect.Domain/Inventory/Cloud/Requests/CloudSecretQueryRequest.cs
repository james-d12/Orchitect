using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Cloud.Requests;

public sealed record CloudSecretQueryRequest(
    OrganisationId OrganisationId,
    string? Name = null,
    string? Location = null,
    string? Url = null,
    CloudSecretPlatform? Platform = null) : BaseRequest(OrganisationId);