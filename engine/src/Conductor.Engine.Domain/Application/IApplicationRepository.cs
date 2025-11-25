using Conductor.Engine.Domain.Shared;

namespace Conductor.Engine.Domain.Application;

public interface IApplicationRepository : IRepository<Application, ApplicationId>;