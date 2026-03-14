using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Engine.Environment;

public sealed record CreateEnvironmentRequest(string Name, string Description, OrganisationId OrganisationId);