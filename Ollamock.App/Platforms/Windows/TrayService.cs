using System.Drawing;
using System.Windows.Forms;

namespace Ollamock.App.Platforms.Windows;

public class TrayService
{
    private readonly App _app;
    private readonly BridgeService _bridgeService;
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _bridgeMenuItem;

    public TrayService(App app, BridgeService bridgeService)
    {
        _app = app;
        _bridgeService = bridgeService;
    }

    public void Initialize()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Ollamock - Bridge Running",
            Visible = true
        };

        _trayIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _app.ShowWindow();
            }
        };

        var contextMenu = new ContextMenuStrip();

        // Status indicator
        var statusItem = new ToolStripMenuItem("🟢 Bridge Running")
        {
            Enabled = false,
            Font = new Font(contextMenu.Font, FontStyle.Bold)
        };
        contextMenu.Items.Add(statusItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        // Open Dashboard
        contextMenu.Items.Add("Open Ollamock", null, (s, e) => _app.ShowWindow());
        contextMenu.Items.Add(new ToolStripSeparator());

        // Launchers
        contextMenu.Items.Add("Launch Codex", null, async (s, e) => await LaunchTool("codex"));
        contextMenu.Items.Add("Launch Claude", null, async (s, e) => await LaunchTool("claude"));
        contextMenu.Items.Add(new ToolStripSeparator());

        // Bridge control
        _bridgeMenuItem = new ToolStripMenuItem("Stop Bridge", null, async (s, e) => await ToggleBridge());
        contextMenu.Items.Add(_bridgeMenuItem);

        contextMenu.Items.Add("Restart Backends", null, async (s, e) => await RestartBackends());
        contextMenu.Items.Add(new ToolStripSeparator());

        // Utilities
        contextMenu.Items.Add("Open Logs", null, (s, e) => OpenLogs());
        contextMenu.Items.Add("Settings", null, (s, e) => _app.ShowWindow());
        contextMenu.Items.Add(new ToolStripSeparator());

        // Exit
        contextMenu.Items.Add("Quit Ollamock", null, (s, e) => Exit());

        _trayIcon.ContextMenuStrip = contextMenu;

        // Start status polling
        _ = PollStatusAsync();
    }

    private async Task PollStatusAsync()
    {
        while (_trayIcon?.Visible == true)
        {
            try
            {
                var status = await _bridgeService.GetStatusAsync();
                if (status != null)
                {
                    var running = status.backends.Count(b => b.running);
                    var total = status.backends.Count;

                    if (running == total && total > 0)
                        SetStatus("running");
                    else if (running > 0)
                        SetStatus("warming");
                    else
                        SetStatus("down");
                }
                else
                {
                    SetStatus("down");
                }
            }
            catch
            {
                SetStatus("down");
            }

            await Task.Delay(5000);
        }
    }

    public void SetStatus(string status)
    {
        if (_trayIcon == null) return;

        switch (status)
        {
            case "running":
                _trayIcon.Icon = SystemIcons.Shield;
                _trayIcon.Text = "Ollamock - 🟢 Bridge Running";
                break;
            case "warming":
                _trayIcon.Icon = SystemIcons.Warning;
                _trayIcon.Text = "Ollamock - 🟡 Backend Warming";
                break;
            case "down":
                _trayIcon.Icon = SystemIcons.Error;
                _trayIcon.Text = "Ollamock - 🔴 Backend Down";
                break;
        }
    }

    private async Task LaunchTool(string tool)
    {
        await _bridgeService.LaunchToolAsync(tool);
    }

    private async Task ToggleBridge()
    {
        // Toggle bridge service
        if (_bridgeMenuItem != null)
        {
            _bridgeMenuItem.Text = _bridgeMenuItem.Text == "Stop Bridge" ? "Start Bridge" : "Stop Bridge";
        }
    }

    private async Task RestartBackends()
    {
        var backends = await _bridgeService.GetBackendsAsync();
        if (backends != null)
        {
            foreach (var backend in backends)
            {
                await _bridgeService.RestartBackendAsync(backend.id);
            }
        }
    }

    private void OpenLogs()
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
        if (Directory.Exists(logPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = logPath,
                UseShellExecute = true
            });
        }
    }

    private void Exit()
    {
        _trayIcon?.Dispose();
        Application.Current?.Quit();
    }
}
