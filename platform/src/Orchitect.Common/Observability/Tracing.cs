using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Orchitect.Common.Observability;

public readonly struct Tracing
{
    private static readonly ActivitySource ActivitySource = new(AppDomain.CurrentDomain.FriendlyName);

    public static Activity? StartActivity(
        [CallerMemberName]
        string? caller = null,
        [CallerFilePath]
        string? filePath = null)
    {
        return ActivitySource.StartActivity(GetActivityName(caller, filePath));
    }

    /// <summary>
    /// Starts an activity under an explicit parent, for work that has lost the caller's ambient trace context.
    /// </summary>
    public static Activity? StartActivity(
        ActivityContext parentContext,
        [CallerMemberName]
        string? caller = null,
        [CallerFilePath]
        string? filePath = null)
    {
        return ActivitySource.StartActivity(GetActivityName(caller, filePath), ActivityKind.Internal, parentContext);
    }

    private static string GetActivityName(string? caller, string? filePath)
    {
        var className =
            Path.GetFileNameWithoutExtension(filePath?.Split(Path.DirectorySeparatorChar).LastOrDefault() ??
                                             string.Empty);
        return $"{className}.{caller}";
    }
}