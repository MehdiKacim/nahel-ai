using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ollamock.App.Services;
using System.Collections.ObjectModel;
using System.Timers;

namespace Ollamock.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly BridgeService _bridgeService;
    private readonly System.Timers.Timer _refreshTimer;

    [ObservableProperty]
    private string _bridgeStatus = "Checking...";

    [ObservableProperty]
    private string _bridgeStatusColor = "#8b949e";

    [ObservableProperty]
    private string _activeModel = "-";

    [ObservableProperty]
    private string _backendHealth = "Unknown";

    [ObservableProperty]
    private string _ttft = "-";

    [ObservableProperty]
    private string _tokPerSec = "-";

    [ObservableProperty]
    private long _ramUsed;

    [ObservableProperty]
    private ObservableCollection<ModelInfo> _models = new();

    [ObservableProperty]
    private ObservableCollection<BackendInfo> _backends = new();

    [ObservableProperty]
    private ObservableCollection<LauncherInfo> _launchers = new();

    public DashboardViewModel(BridgeService bridgeService)
    {
        _bridgeService = bridgeService;
        _refreshTimer = new System.Timers.Timer(3000);
        _refreshTimer.Elapsed += async (s, e) => await RefreshAsync();
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();

        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var status = await _bridgeService.GetStatusAsync();
            if (status != null)
            {
                BridgeStatus = "Online";
                BridgeStatusColor = "#238636";
                RamUsed = status.ramUsed;

                var runningBackends = status.backends.Count(b => b.running);
                BackendHealth = $"{runningBackends}/{status.backends.Count} running";
            }
            else
            {
                BridgeStatus = "Offline";
                BridgeStatusColor = "#f85149";
            }

            var models = await _bridgeService.GetModelsAsync();
            if (models != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Models.Clear();
                    foreach (var m in models) Models.Add(m);
                    var active = models.FirstOrDefault(m => m.status == "running");
                    if (active != null) ActiveModel = active.name;
                });
            }

            var backends = await _bridgeService.GetBackendsAsync();
            if (backends != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Backends.Clear();
                    foreach (var b in backends) Backends.Add(b);
                });
            }

            var launchers = await _bridgeService.GetLaunchersAsync();
            if (launchers != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Launchers.Clear();
                    foreach (var l in launchers) Launchers.Add(l);
                });
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task ToggleModelAsync(ModelInfo model)
    {
        await _bridgeService.ToggleModelAsync(model.name);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task StartBackendAsync(BackendInfo backend)
    {
        await _bridgeService.StartBackendAsync(backend.id);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task StopBackendAsync(BackendInfo backend)
    {
        await _bridgeService.StopBackendAsync(backend.id);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task LaunchToolAsync(LauncherInfo launcher)
    {
        await _bridgeService.LaunchToolAsync(launcher.name);
    }

    [RelayCommand]
    private async Task InstallToolAsync(LauncherInfo launcher)
    {
        await _bridgeService.InstallToolAsync(launcher.name);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ConfigureToolAsync(LauncherInfo launcher)
    {
        await _bridgeService.ConfigureToolAsync(launcher.name);
        await RefreshAsync();
    }
}
