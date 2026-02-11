using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Engine.Domain.Environment;

public sealed record CreateEnvironmentRequest(string Name, string Description, OrganisationId OrganisationId);