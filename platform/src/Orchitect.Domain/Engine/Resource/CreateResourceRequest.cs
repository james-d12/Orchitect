using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.ResourceTemplate;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Engine.Resource;

public sealed record CreateResourceRequest(
    OrganisationId OrganisationId,
    string Name,
    string Description,
    ResourceTemplateId ResourceTemplateId,
    EnvironmentId EnvironmentId,
    ResourceKind Kind,
    ApplicationId? ApplicationId = null);
