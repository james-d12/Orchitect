namespace Orchitect.Engine.Execution.RunnerApi;

public sealed class RunnerApiOptions
{
    public Uri? BaseUrl { get; set; }
    public Guid RunId { get; set; }
    public string Token { get; set; } = string.Empty;
    public int MaxRetryAttempts { get; set; } = 5;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    public string? GetValidationError()
    {
        if (BaseUrl is null)
        {
            return "The runner API base URL is not configured.";
        }

        if (RunId == Guid.Empty)
        {
            return "The runner's run ID is not configured.";
        }

        return string.IsNullOrWhiteSpace(Token) ? "The runner's run token is not configured." : null;
    }
}
