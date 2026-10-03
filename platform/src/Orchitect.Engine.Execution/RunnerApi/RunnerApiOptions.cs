using Orchitect.Engine.Contracts.Runner;

namespace Orchitect.Engine.Execution.RunnerApi;

public sealed class RunnerApiOptions
{
    public Uri? BaseUrl { get; set; }
    public Guid RunId { get; set; }
    public string Token { get; set; } = string.Empty;
    public int MaxRetryAttempts { get; set; } = 5;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public string? GetValidationError()
    {
        if (BaseUrl is null)
        {
            return $"{RunnerEnvironment.ApiBaseUrl} must be an absolute URL.";
        }

        if (RunId == Guid.Empty)
        {
            return $"{RunnerEnvironment.RunId} must be a non-empty GUID.";
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            return $"{RunnerEnvironment.RunToken} must be set.";
        }

        if (MaxRetryAttempts < 0)
        {
            return "The runner API retry count must not be negative.";
        }

        return AttemptTimeout <= TimeSpan.Zero || TotalTimeout <= TimeSpan.Zero
            ? "The runner API timeouts must be greater than zero."
            : null;
    }
}
