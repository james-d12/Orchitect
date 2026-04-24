using Orchitect.Domain.Core;

namespace Orchitect.Domain.Inventory.Shared;

public interface IQueryRepository<out T, in TQuery>
    where T : IEntity
    where TQuery : BaseQuery
{
    IReadOnlyList<T> GetByQuery(TQuery query);
}