using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Engine.Execution.RunnerApi;

public interface IRunnerApiClient
{
    /// <summary>
    /// Gets what this runner's run should execute.
    /// </summary>
    Task<RunDescriptor> GetRunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Submits the parsed score file and returns the plan to execute.
    /// </summary>
    Task<RunPlan> SubmitScoreAsync(ScoreSubmission submission, CancellationToken cancellationToken);

    /// <summary>
    /// Reports the outcome of this runner's run.
    /// </summary>
    Task CompleteAsync(RunCompletion completion, CancellationToken cancellationToken);
}

public sealed class RunnerApiClient : IRunnerApiClient
{
    private readonly HttpClient _httpClient;
    private readonly Guid _runId;

    public RunnerApiClient(HttpClient httpClient, IOptions<RunnerApiOptions> options)
    {
        _httpClient = httpClient;
        _runId = options.Value.RunId;
    }

    public async Task<RunDescriptor> GetRunAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<RunDescriptor>(RunnerRoutes.ForRun(_runId),
                   RunnerContract.JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException($"The API returned no descriptor for run '{_runId}'.");
    }

    public async Task<RunPlan> SubmitScoreAsync(ScoreSubmission submission, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(RunnerRoutes.ForRun(_runId, RunnerRoutes.Plan),
            submission, RunnerContract.JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RunPlan>(RunnerContract.JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException($"The API returned no plan for run '{_runId}'.");
    }

    public async Task CompleteAsync(RunCompletion completion, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(RunnerRoutes.ForRun(_runId, RunnerRoutes.Complete),
            completion, RunnerContract.JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
