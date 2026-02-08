using Conductor.Engine.Domain.Application;
using Conductor.Engine.Domain.Deployment;
using Conductor.Engine.Infrastructure.CommandLine;
using Conductor.Engine.Infrastructure.Score.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Conductor.Engine.Infrastructure.Score;

public interface IScoreDriver
{
    Task<ScoreFile?> ParseAsync(Deployment deployment, Application application, CancellationToken cancellationToken);
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

    public async Task<ScoreFile?> ParseAsync(Deployment deployment, Application application,
        CancellationToken cancellationToken)
    {
        ScoreValidationResult scoreValidationResult =
            await ValidateAsync(deployment, application, cancellationToken: cancellationToken);

        if (scoreValidationResult.State != ScoreValidationResultState.Valid)
        {
            _logger.LogError("Score Validation for {Application} failed due to: {State}",
                application.Name, scoreValidationResult.State);
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

    private async Task<ScoreValidationResult> ValidateAsync(Deployment deployment, Application application,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating Score File for Application: {Application}", application.Name);

        var commit = deployment.CommitId.Value;
        var safeDirectoryName = string.Join("_", application.Name.Replace(" ", "_").Trim(),
            deployment.CommitId.Value.Take(6), DateTime.Now);
        var destination = Path.Combine(Path.GetTempPath(), "conductor", "score", safeDirectoryName);

        var result = await _gitCommandLine.CloneCommitAsync(application.Repository.Url, commit, destination);

        if (!result)
        {
            _logger.LogError("Could not clone repository: {RepositoryUrl} for commit: {Commit}",
                application.Repository.Url, commit);
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