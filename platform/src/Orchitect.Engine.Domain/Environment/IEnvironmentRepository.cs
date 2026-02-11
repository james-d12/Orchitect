using Orchitect.Shared;

namespace Orchitect.Engine.Domain.Environment;

public interface IEnvironmentRepository : IRepository<Environment, EnvironmentId>
{
    Task<Environment?> UpdateAsync(Environment environment, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(EnvironmentId id, CancellationToken cancellationToken = default);
}