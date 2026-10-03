namespace Orchitect.Engine.Contracts.Runner.Api;

public sealed record RunDescriptor(
    Guid RunId,
    RunnerOperation Operation,
    Uri RepositoryUrl,
    string CommitId,
    Guid ApplicationId,
    Guid EnvironmentId);
