using Ollamock.App.Platforms.Windows;
using Ollamock.App.Services;
using Ollamock.App.ViewModels;

namespace Ollamock.App;

public partial class App : Application
{
    private readonly BridgeService _bridgeService;
    private TrayService? _trayService;

    public App()
    {
        InitializeComponent();
        _bridgeService = new BridgeService("http://localhost:11434");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Title = "Ollamock";
        window.Width = 1200;
        window.Height = 800;

#if WINDOWS
        _trayService = new TrayService(this, _bridgeService);
        _trayService.Initialize();

        window.Destroying += (s, e) =>
        {
            // Hide instead of close when clicking X
            if (_trayService != null)
            {
                HideWindow();
                return;
            }
        };
#endif

        return window;
    }

    public void ShowWindow()
    {
        if (Windows?.FirstOrDefault() is Window window)
        {
            window.Show();
            window.BringToFront();
        }
    }

    public void HideWindow()
    {
        if (Windows?.FirstOrDefault() is Window window)
        {
            window.Hide();
        }
    }

    public void SetTrayStatus(string status)
    {
        _trayService?.SetStatus(status);
    }
}
