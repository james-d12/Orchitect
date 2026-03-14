namespace Orchitect.Domain.Inventory.Shared;

public abstract record BaseRequest(int Page = 0, int PageSize = 0);