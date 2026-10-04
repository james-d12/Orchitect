using Microsoft.Extensions.Logging;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Configuration.Score.Models;
using Orchitect.Engine.Execution.Shared.CommandLine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Orchitect.Engine.Execution.Configuration.Score;

public interface IScoreDriver
{
    Task<ScoreFile?> ParseAsync(RunDescriptor run, CancellationToken cancellationToken);
}

public sealed class ScoreDriver : IScoreDriver
{
    private readonly ILogger<ScoreDriver> _logger;
    private readonly IGitCommandLine _gitCommandLine;

    public ScoreDriver(ILogger<ScoreDriver> logger, IGitCommandLine gitCommandLine)
    {
        _logger = logger;
        _gitCommandLine = gitCommandLine;
    }

    public async Task<ScoreFile?> ParseAsync(RunDescriptor run, CancellationToken cancellationToken)
    {
        ScoreValidationResult scoreValidationResult = await ValidateAsync(run);

        if (scoreValidationResult.State != ScoreValidationResultState.Valid)
        {
            _logger.LogError("Score Validation for {RepositoryUrl} failed due to: {State}",
                run.RepositoryUrl, scoreValidationResult.State);
            return null;
        }

        var fileContents = await File.ReadAllTextAsync(scoreValidationResult.ScoreFilePath, cancellationToken);

        var scoreFile = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<ScoreFile>(fileContents);

        _logger.LogInformation("Score File: {Contents}", string.Join(",", scoreFile?.Resources?.Keys.ToList() ?? []));

        return scoreFile;
    }

    private async Task<ScoreValidationResult> ValidateAsync(RunDescriptor run)
    {
        _logger.LogInformation("Validating Score File for {RepositoryUrl} at {Commit}", run.RepositoryUrl,
            run.CommitId);

        var destination = Path.Combine(Path.GetTempPath(), "orchitect", "score", run.RunId.ToString("N"));

        var result = await _gitCommandLine.CloneCommitAsync(run.RepositoryUrl, run.CommitId, destination);

        if (!result)
        {
            _logger.LogError("Could not clone repository: {RepositoryUrl} for commit: {Commit}",
                run.RepositoryUrl, run.CommitId);
            return ScoreValidationResult.CloneFailed();
        }

        var scoreFile = Directory
            .GetFiles(destination, "score.yaml", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(scoreFile))
        {
            return ScoreValidationResult.ScoreFileNotFound();
        }

        return ScoreValidationResult.Valid(scoreFile);
    }
}