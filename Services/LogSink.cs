using System.Collections.Concurrent;

namespace OllamaBridge.Services;

public class LogSink
{
    private readonly ConcurrentQueue<string> _ring = new();
    private readonly string _logDir;
    private const int RING_SIZE = 500;

    public LogSink()
    {
        _logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDir);
    }

    public void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
        _ring.Enqueue(line);
        while (_ring.Count > RING_SIZE) _ring.TryDequeue(out _);

        _ = File.AppendAllTextAsync(GetLogFile(), line + Environment.NewLine);
    }

    public IEnumerable<string> GetRecent(int count = 100) => _ring.TakeLast(count);

    public string GetLogFilePath(string? date = null) =>
        Path.Combine(_logDir, $"bridge-{date ?? DateTime.Now:yyyyMMdd}.log");

    private string GetLogFile() => GetLogFilePath();
}
