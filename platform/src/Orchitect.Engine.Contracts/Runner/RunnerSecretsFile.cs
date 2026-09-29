using System.Formats.Tar;
using System.Text.Json;

namespace Orchitect.Engine.Contracts.Runner;

public static class RunnerSecretsFile
{
    public const string DirectoryPath = "/run/orchitect";
    public const string FileName = "secrets.json";
    public const string FilePath = $"{DirectoryPath}/{FileName}";
    public const int RunnerUserId = 1654;

    public static MemoryStream CreateArchive(IReadOnlyDictionary<string, string> secrets)
    {
        var archive = new MemoryStream();

        using (var writer = new TarWriter(archive, TarEntryFormat.Pax, leaveOpen: true))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, FileName)
            {
                Mode = UnixFileMode.UserRead,
                Uid = RunnerUserId,
                Gid = RunnerUserId,
                ModificationTime = DateTimeOffset.UtcNow,
                DataStream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(secrets))
            };
            writer.WriteEntry(entry);
        }

        archive.Position = 0;
        return archive;
    }

    public static int LoadIntoEnvironment(string path = FilePath)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(path)) ?? [];
        File.Delete(path);

        foreach (var (key, value) in secrets)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        return secrets.Count;
    }
}
