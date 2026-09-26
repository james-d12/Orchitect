using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformModuleDownloader
{
    /// <summary>
    /// Downloads each unique repository and ref once and returns the local module directory for every source.
    /// </summary>
    Task<IReadOnlyDictionary<ResourceTemplateVersionSource, TerraformModuleDownloadResult>> DownloadAsync(
        IEnumerable<ResourceTemplateVersionSource> sources, CancellationToken cancellationToken = default);
}

public sealed class TerraformModuleDownloader : ITerraformModuleDownloader
{
    private const int MaxConcurrentDownloads = 4;

    private readonly ILogger<TerraformModuleDownloader> _logger;
    private readonly IGitCommandLine _gitCommandLine;
    private readonly string _cacheRoot;

    public TerraformModuleDownloader(ILogger<TerraformModuleDownloader> logger, IGitCommandLine gitCommandLine,
        string? cacheRoot = null)
    {
        _logger = logger;
        _gitCommandLine = gitCommandLine;
        _cacheRoot = cacheRoot ?? Path.Combine(Path.GetTempPath(), "orchitect", "terraform", "modules");
    }

    public async Task<IReadOnlyDictionary<ResourceTemplateVersionSource, TerraformModuleDownloadResult>> DownloadAsync(
        IEnumerable<ResourceTemplateVersionSource> sources, CancellationToken cancellationToken = default)
    {
        var distinctSources = sources.Distinct().ToList();
        var repositories = distinctSources
            .Select(source => new Repository(source.BaseUrl, source.Tag))
            .Distinct()
            .ToList();

        var repositoryDirectories = new Dictionary<Repository, string?>();

        await Parallel.ForEachAsync(repositories,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentDownloads,
                CancellationToken = cancellationToken
            },
            async (repository, _) =>
            {
                var directory = await DownloadRepositoryAsync(repository);

                lock (repositoryDirectories)
                {
                    repositoryDirectories[repository] = directory;
                }
            });

        _logger.LogInformation("Downloaded {RepositoryCount} unique repositories for {SourceCount} sources.",
            repositories.Count, distinctSources.Count);

        return distinctSources.ToDictionary(source => source, source =>
            ResolveModuleDirectory(source, repositoryDirectories[new Repository(source.BaseUrl, source.Tag)]));
    }

    private async Task<string?> DownloadRepositoryAsync(Repository repository)
    {
        var directory = Path.Combine(_cacheRoot, repository.CacheKey);

        if (Directory.Exists(directory))
        {
            _logger.LogDebug("Using cached repository {Url}@{Tag} at {Directory}.", repository.BaseUrl,
                repository.Tag, directory);
            return directory;
        }

        Directory.CreateDirectory(_cacheRoot);
        var stagingDirectory = $"{directory}.tmp-{Guid.NewGuid():N}";

        try
        {
            var cloned = string.IsNullOrEmpty(repository.Tag)
                ? await _gitCommandLine.CloneAsync(repository.BaseUrl, stagingDirectory)
                : await _gitCommandLine.CloneTagAsync(repository.BaseUrl, repository.Tag, stagingDirectory);

            if (!cloned)
            {
                _logger.LogWarning("Could not download repository {Url}@{Tag}.", repository.BaseUrl, repository.Tag);
                return null;
            }

            try
            {
                Directory.Move(stagingDirectory, directory);
            }
            catch (IOException) when (Directory.Exists(directory))
            {
                _logger.LogDebug("Repository {Url}@{Tag} was downloaded concurrently, using the existing copy.",
                    repository.BaseUrl, repository.Tag);
            }

            _logger.LogInformation("Downloaded repository {Url}@{Tag} to {Directory}.", repository.BaseUrl,
                repository.Tag, directory);
            return directory;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static TerraformModuleDownloadResult ResolveModuleDirectory(ResourceTemplateVersionSource source,
        string? repositoryDirectory)
    {
        if (repositoryDirectory is null)
        {
            return TerraformModuleDownloadResult.Failure($"Could not download {source.BaseUrl}@{source.Tag}.");
        }

        if (string.IsNullOrEmpty(source.FolderPath))
        {
            return TerraformModuleDownloadResult.Success(repositoryDirectory);
        }

        var moduleDirectory = Path.Combine(repositoryDirectory, source.FolderPath);

        return Directory.Exists(moduleDirectory)
            ? TerraformModuleDownloadResult.Success(moduleDirectory)
            : TerraformModuleDownloadResult.Failure(
                $"Folder '{source.FolderPath}' does not exist in {source.BaseUrl}@{source.Tag}.");
    }

    private sealed record Repository(Uri BaseUrl, string Tag)
    {
        public string CacheKey =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{BaseUrl}@{Tag}")))[..16];
    }
}
