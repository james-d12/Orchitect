using System.Formats.Tar;
using System.Text.Json;
using Orchitect.Infrastructure.Engine.Executor;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Executor;

public sealed class RunnerSecretsFileTests
{
    [Fact]
    public async Task CreateArchive_WritesOwnerReadOnlySecretsFileForRunnerUser()
    {
        var secrets = new Dictionary<string, string> { ["ARM_CLIENT_SECRET"] = "s3cr3t", ["A__B"] = "\"quoted\"" };

        await using var archive = RunnerSecretsFile.CreateArchive(secrets);
        await using var reader = new TarReader(archive);
        var entry = await reader.GetNextEntryAsync(copyData: true);

        Assert.NotNull(entry);
        Assert.Equal(RunnerSecretsFile.FileName, entry.Name);
        Assert.Equal(TarEntryType.RegularFile, entry.EntryType);
        Assert.Equal(UnixFileMode.UserRead, entry.Mode);
        Assert.Equal(RunnerSecretsFile.RunnerUserId, entry.Uid);
        Assert.Equal(RunnerSecretsFile.RunnerUserId, entry.Gid);
        Assert.Equal(secrets, await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(entry.DataStream!));
        Assert.Null(await reader.GetNextEntryAsync());
    }

    [Fact]
    public async Task LoadIntoEnvironment_SetsVariablesAndDeletesFile()
    {
        var key = $"ORCHITECT_TEST_{Guid.NewGuid():N}";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new Dictionary<string, string> { [key] = "value" }));

        try
        {
            var loaded = RunnerSecretsFile.LoadIntoEnvironment(path);

            Assert.Equal(1, loaded);
            Assert.Equal("value", Environment.GetEnvironmentVariable(key));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void LoadIntoEnvironment_MissingFile_LoadsNothing()
    {
        Assert.Equal(0, RunnerSecretsFile.LoadIntoEnvironment(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")));
    }
}
