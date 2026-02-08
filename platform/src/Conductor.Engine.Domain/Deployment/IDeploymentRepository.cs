using Conductor.Engine.Domain.Shared;

namespace Conductor.Engine.Domain.Deployment;

public interface IDeploymentRepository : IRepository<Deployment, DeploymentId>;