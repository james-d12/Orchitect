using System.Diagnostics;

namespace Orchitect.Infrastructure.Inventory.Shared.Observability;

public static class ActivityExtensions
{
    public static void RecordException(this Activity? activity, Exception exception)
    {
        activity?.AddException(exception);
        activity?.SetStatus(ActivityStatusCode.Error);
    }
}