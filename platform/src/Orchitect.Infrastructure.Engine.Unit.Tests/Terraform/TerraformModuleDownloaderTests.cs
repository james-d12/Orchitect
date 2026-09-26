using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Terraform;

public sealed class TerraformModuleDownloaderTests : IDisposable
{
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "orchitect-tests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_SameRepositoryDifferentFolders_ClonesOnce()
    {
        var git = new FakeGitCommandLine("storage", "network");
        var downloader = CreateDownloader(git);
        var storage = Source("https://example.com/modules.git", folderPath: "storage");
        var network = Source("https://example.com/modules.git", folderPath: "network");

        var results = await downloader.DownloadAsync([storage, network]);

        Assert.Equal(1, git.CloneCount);
        Assert.EndsWith("storage", results[storage].Directory);
        Assert.EndsWith("network", results[network].Directory);
        Assert.True(Directory.Exists(results[storage].Directory));
    }

    [Fact]
    public async Task DownloadAsync_DuplicateSources_ClonesOnce()
    {
        var git = new FakeGitCommandLine();
        var downloader = CreateDownloader(git);
        var source = Source("https://example.com/storage.git");

        var results = await downloader.DownloadAsync([source, source with { }]);

        Assert.Equal(1, git.CloneCount);
        Assert.Single(results);
    }

    [Fact]
    public async Task DownloadAsync_DifferentTags_ClonesEachTag()
    {
        var git = new FakeGitCommandLine();
        var downloader = CreateDownloader(git);

        var results = await downloader.DownloadAsync([
            Source("https://example.com/storage.git", tag: "v1.0.0"),
            Source("https://example.com/storage.git", tag: "v2.0.0")
        ]);

        Assert.Equal(2, git.CloneCount);
        Assert.Equal(2, results.Values.Select(r => r.Directory).Distinct().Count());
    }

    [Fact]
    public async Task DownloadAsync_CloneFails_LeavesNothingBehindAndRetriesNextTime()
    {
        var git = new FakeGitCommandLine { Succeeds = false };
        var downloader = CreateDownloader(git);
        var source = Source("https://example.com/storage.git");

        var failed = await downloader.DownloadAsync([source]);

        Assert.False(failed[source].IsSuccess);
        Assert.Empty(Directory.GetDirectories(_cacheRoot));

        git.Succeeds = true;
        var succeeded = await downloader.DownloadAsync([source]);

        Assert.True(succeeded[source].IsSuccess);
        Assert.Equal(2, git.CloneCount);
    }

    [Fact]
    public async Task DownloadAsync_AlreadyDownloaded_ReusesCache()
    {
        var git = new FakeGitCommandLine();
        var downloader = CreateDownloader(git);
        var source = Source("https://example.com/storage.git");

        var first = await downloader.DownloadAsync([source]);
        var second = await downloader.DownloadAsync([source]);

        Assert.Equal(1, git.CloneCount);
        Assert.Equal(first[source].Directory, second[source].Directory);
    }

    [Fact]
    public async Task DownloadAsync_MissingFolder_ReturnsFailure()
    {
        var downloader = CreateDownloader(new FakeGitCommandLine());
        var source = Source("https://example.com/modules.git", folderPath: "missing");

        var results = await downloader.DownloadAsync([source]);

        Assert.False(results[source].IsSuccess);
        Assert.Contains("missing", results[source].Error);
    }

    private TerraformModuleDownloader CreateDownloader(IGitCommandLine git) =>
        new(NullLogger<TerraformModuleDownloader>.Instance, git, _cacheRoot);

    private static ResourceTemplateVersionSource Source(string url, string tag = "", string folderPath = "") =>
        new() { BaseUrl = new Uri(url), Tag = tag, FolderPath = folderPath };

    internal sealed class FakeGitCommandLine : IGitCommandLine
    {
        private readonly string[] _folders;
        private int _cloneCount;

        public FakeGitCommandLine(params string[] folders)
        {
            _folders = folders;
        }

        public bool Succeeds { get; set; } = true;

        public int CloneCount => _cloneCount;

        public Task<bool> CloneAsync(Uri source, string destination) => Clone(destination);

        public Task<bool> CloneTagAsync(Uri source, string tag, string destination) => Clone(destination);

        public Task<bool> CloneCommitAsync(Uri source, string commit, string destination) => Clone(destination);

        private Task<bool> Clone(string destination)
        {
            Interlocked.Increment(ref _cloneCount);
            Directory.CreateDirectory(destination);

            if (!Succeeds)
            {
                return Task.FromResult(false);
            }

            foreach (var directory in _folders.Select(f => Path.Combine(destination, f)).Append(destination))
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "variables.tf"), string.Empty);
                File.WriteAllText(Path.Combine(directory, "outputs.tf"), string.Empty);
            }

            return Task.FromResult(true);
        }
    }
}
