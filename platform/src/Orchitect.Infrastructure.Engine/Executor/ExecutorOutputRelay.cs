using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Orchitect.Infrastructure.Engine.Executor;

public sealed class ExecutorOutputRelay
{
    private const int MaxRawLines = 200;

    private readonly ILogger _logger;
    private readonly string _containerId;
    private readonly LogLevel _rawLevel;
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _pendingLine = new();
    private readonly List<string> _rawLines = [];
    private char[] _chars = [];

    public ExecutorOutputRelay(ILogger logger, string containerId, LogLevel rawLevel)
    {
        _logger = logger;
        _containerId = containerId;
        _rawLevel = rawLevel;
    }

    public int EntryCount { get; private set; }

    public void Append(byte[] buffer, int count)
    {
        var charCount = _decoder.GetCharCount(buffer, 0, count);

        if (_chars.Length < charCount)
        {
            _chars = new char[charCount];
        }

        var decoded = _decoder.GetChars(buffer, 0, count, _chars, 0);

        for (var i = 0; i < decoded; i++)
        {
            if (_chars[i] == '\n')
            {
                HandleLine(_pendingLine.ToString().TrimEnd('\r'));
                _pendingLine.Clear();
            }
            else
            {
                _pendingLine.Append(_chars[i]);
            }
        }
    }

    public void FlushRaw()
    {
        if (_rawLines.Any(line => !string.IsNullOrWhiteSpace(line)))
        {
            _logger.Log(_rawLevel, "Runner {ContainerId} output:\n{Output}", _containerId,
                string.Join('\n', _rawLines).Trim('\n'));
            EntryCount++;
        }

        _rawLines.Clear();
    }

    public void Complete()
    {
        if (_pendingLine.Length > 0)
        {
            HandleLine(_pendingLine.ToString().TrimEnd('\r'));
            _pendingLine.Clear();
        }

        FlushRaw();
    }

    private void HandleLine(string line)
    {
        if (TryParseEntry(line, out var level, out var category, out var message))
        {
            FlushRaw();
            _logger.Log(level, "Runner {ContainerId} {Category}: {Message}", _containerId, category, message);
            EntryCount++;
            return;
        }

        _rawLines.Add(line);

        if (_rawLines.Count >= MaxRawLines)
        {
            FlushRaw();
        }
    }

    private static bool TryParseEntry(string line, out LogLevel level, out string category, out string message)
    {
        level = LogLevel.None;
        category = string.Empty;
        message = string.Empty;

        if (!line.StartsWith('{'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (!root.TryGetProperty("LogLevel", out var levelElement) ||
                !Enum.TryParse(levelElement.GetString(), out level) ||
                !root.TryGetProperty("Message", out var messageElement))
            {
                return false;
            }

            category = root.TryGetProperty("Category", out var categoryElement)
                ? categoryElement.GetString() ?? string.Empty
                : string.Empty;

            message = messageElement.GetString() ?? string.Empty;

            if (root.TryGetProperty("Exception", out var exceptionElement) &&
                exceptionElement.GetString() is { Length: > 0 } exception)
            {
                message = $"{message}\n{exception}";
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
