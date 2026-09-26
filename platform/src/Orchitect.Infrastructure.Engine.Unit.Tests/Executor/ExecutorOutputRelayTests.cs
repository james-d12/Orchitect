using System.Text;
using Microsoft.Extensions.Logging;
using Orchitect.Infrastructure.Engine.Executor;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Executor;

public sealed class ExecutorOutputRelayTests
{
    [Fact]
    public void Append_JsonEntry_LogsOneEntryWithOriginalLevelAndMultiLineMessage()
    {
        var logger = new ListLogger();
        var relay = new ExecutorOutputRelay(logger, "abc", LogLevel.Information);

        Append(relay,
            """{"EventId":0,"LogLevel":"Debug","Category":"Orchitect.Builder","Message":"Render output: module {\n  name = \"x\"\n}"}""" +
            "\n");
        relay.Complete();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("Orchitect.Builder", entry.Message);
        Assert.Contains("module {\n  name = \"x\"\n}", entry.Message);
    }

    [Fact]
    public void Append_JsonEntryWithException_IncludesException()
    {
        var logger = new ListLogger();
        var relay = new ExecutorOutputRelay(logger, "abc", LogLevel.Information);

        Append(relay, """{"LogLevel":"Error","Category":"C","Message":"failed","Exception":"System.Exception: boom"}""" + "\n");
        relay.Complete();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("failed\nSystem.Exception: boom", entry.Message);
    }

    [Fact]
    public void Append_RawLines_AreBatchedIntoOneEntryUntilFlushed()
    {
        var logger = new ListLogger();
        var relay = new ExecutorOutputRelay(logger, "abc", LogLevel.Warning);

        Append(relay, "Plan: 2 to add\nmodule.a: Creating...\n");

        Assert.Empty(logger.Entries);

        relay.FlushRaw();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Plan: 2 to add\nmodule.a: Creating...", entry.Message);
    }

    [Fact]
    public void Append_JsonEntryAfterRawLines_FlushesRawLinesFirst()
    {
        var logger = new ListLogger();
        var relay = new ExecutorOutputRelay(logger, "abc", LogLevel.Information);

        Append(relay, "raw one\nraw two\n");
        Append(relay, """{"LogLevel":"Information","Category":"C","Message":"done"}""" + "\n");
        relay.Complete();

        Assert.Equal(2, logger.Entries.Count);
        Assert.Contains("raw one\nraw two", logger.Entries[0].Message);
        Assert.Contains("done", logger.Entries[1].Message);
    }

    [Fact]
    public void Append_LineSplitAcrossChunks_IsReassembled()
    {
        var logger = new ListLogger();
        var relay = new ExecutorOutputRelay(logger, "abc", LogLevel.Information);
        var bytes = Encoding.UTF8.GetBytes("""{"LogLevel":"Information","Category":"C","Message":"héllo"}""" + "\n");
        var split = Array.IndexOf(bytes, (byte)0xC3) + 1;

        relay.Append(bytes[..split], split);
        relay.Append(bytes[split..], bytes.Length - split);
        relay.Complete();

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("héllo", entry.Message);
    }

    [Fact]
    public void Complete_BlankRawLinesOnly_LogsNothing()
    {
        var logger = new ListLogger();
        var relay = new ExecutorOutputRelay(logger, "abc", LogLevel.Information);

        Append(relay, "\n\n");
        relay.Complete();

        Assert.Empty(logger.Entries);
    }

    private static void Append(ExecutorOutputRelay relay, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        relay.Append(bytes, bytes.Length);
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
