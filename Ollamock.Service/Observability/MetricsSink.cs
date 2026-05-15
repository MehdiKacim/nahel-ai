namespace Ollamock.Service.Observability;

public class MetricsSink
{
    private readonly string _metricsDir;
    private readonly string _csvFile;

    public MetricsSink()
    {
        _metricsDir = Path.Combine(AppContext.BaseDirectory, "metrics");
        Directory.CreateDirectory(_metricsDir);
        _csvFile = Path.Combine(_metricsDir, "metrics.csv");

        if (!File.Exists(_csvFile))
            File.WriteAllText(_csvFile, "timestamp,req_id,model,provider,device,ttft_ms,tok_s,ram_mb,duration_ms" + Environment.NewLine);
    }

    public void Record(string reqId, string model, string provider, string device, double ttft, double tokS, long ramMb, double durationMs)
    {
        var line = $"{DateTime.Now:O},{reqId},{model},{provider},{device},{ttft:F1},{tokS:F2},{ramMb},{durationMs:F1}";
        _ = File.AppendAllTextAsync(_csvFile, line + Environment.NewLine);
    }

    public IEnumerable<string> GetRecent(int count = 50) =>
        File.Exists(_csvFile) ? File.ReadLines(_csvFile).Skip(1).TakeLast(count) : Enumerable.Empty<string>();
}
