using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Pipeline.Requests;

public sealed record PipelineQuery(
    OrganisationId OrganisationId,
    string? Id = null,
    string? Name = null,
    string? Url = null,
    string? OwnerName = null,
    PipelinePlatform? Platform = null) : BaseQuery(OrganisationId);
