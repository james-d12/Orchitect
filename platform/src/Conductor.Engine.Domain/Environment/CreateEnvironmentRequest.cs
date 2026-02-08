using Conductor.Engine.Domain.Organisation;

namespace Conductor.Engine.Domain.Environment;

public sealed record CreateEnvironmentRequest(string Name, string Description, OrganisationId OrganisationId);