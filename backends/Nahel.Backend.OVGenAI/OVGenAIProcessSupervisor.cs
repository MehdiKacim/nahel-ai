using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Nahel.Engine.OVGenAI;

public sealed class OVGenAIProcessSupervisor : IDisposable
{
    private Process? _process;
    private readonly ILogger<OVGenAIProcessSupervisor> _logger;

    public bool IsRunning => _process != null && !_process.HasExited;

    public OVGenAIProcessSupervisor(ILogger<OVGenAIProcessSupervisor> logger)
    {
        _logger = logger;
    }

    public bool Start(OVGenAIOptions options)
    {
        if (IsRunning) return true;

        var pythonExe = FindPythonExecutable();
        var bridgeScript = FindBridgeScript();

        if (!File.Exists(pythonExe))
        {
            _logger.LogError("Python executable not found at {PythonPath}. Run scripts/build.ps1 to set up the OVGenAI backend.", pythonExe);
            return false;
        }
        if (!File.Exists(bridgeScript))
        {
            _logger.LogError("Bridge script not found at {BridgePath}.", bridgeScript);
            return false;
        }

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"\"{bridgeScript}\" --engine {options.Engine} --model_path \"{options.ModelPath}\" --model_name \"{options.ModelName}\" --device {options.Device} --port {options.Port}",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) _logger.LogInformation("[OVGenAI] {Line}", e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) _logger.LogError("[OVGenAI] {Line}", e.Data); };

        try
        {
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _logger.LogInformation("OVGenAI backend started on port {Port} for model '{ModelName}' using engine '{Engine}'.", options.Port, options.ModelName, options.Engine);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OVGenAI backend.");
            return false;
        }
    }

    public void Stop()
    {
        if (_process == null || _process.HasExited) return;
        try
        {
            _process.Kill(true);
            _process.WaitForExit(5000);
            _logger.LogInformation("OVGenAI backend stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping OVGenAI backend.");
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private static string FindPythonExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "backends", "ovgenai", "bin", "Scripts", "python.exe"),
        };
        foreach (var c in candidates) { var full = Path.GetFullPath(c); if (File.Exists(full)) return full; }
        return candidates[0];
    }

    private static string FindBridgeScript()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "backends", "ovgenai", "bin", "bridge", "ovgenai_bridge.py"),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "backends", "Nahel.Backend.OVGenAI", "Bridge", "ovgenai_bridge.py"),
        };
        foreach (var c in candidates) { var full = Path.GetFullPath(c); if (File.Exists(full)) return full; }
        return candidates[0];
    }
}
