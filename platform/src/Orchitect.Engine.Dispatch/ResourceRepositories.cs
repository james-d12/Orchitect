using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;

namespace Orchitect.Engine.Dispatch.Plan;

public sealed record ResourceRepositories(
    IResourceRepository Resources,
    IResourceInstanceRepository Instances,
    IResourceDependencyGraphRepository DependencyGraphs);
