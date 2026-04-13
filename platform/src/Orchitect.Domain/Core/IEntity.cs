using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Core;

public interface IEntity
{
    OrganisationId OrganisationId { get; }
}