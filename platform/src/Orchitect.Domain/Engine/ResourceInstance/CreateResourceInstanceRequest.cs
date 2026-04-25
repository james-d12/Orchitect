using System.Text.Json;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Domain.Engine.ResourceInstance;

public sealed record CreateResourceInstanceRequest(
    ResourceId ResourceId,
    OrganisationId OrganisationId,
    string Name,
    ResourceTemplateVersionId TemplateVersionId,
    EnvironmentId EnvironmentId,
    IReadOnlyDictionary<string, JsonElement>? InputParameters = null);
