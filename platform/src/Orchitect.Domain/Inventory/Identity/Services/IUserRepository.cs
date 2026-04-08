using Orchitect.Domain.Core;

namespace Orchitect.Domain.Inventory.Identity.Services;

public interface IUserRepository : IRepository<User, UserId>
{
    Task UpsertAsync(
        User user,
        CancellationToken cancellationToken = default);
}