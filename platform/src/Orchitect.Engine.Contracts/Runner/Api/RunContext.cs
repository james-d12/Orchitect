namespace Orchitect.Engine.Contracts.Runner.Api;

public sealed record RunContext(string ProjectName, Guid ApplicationId, Guid EnvironmentId);
