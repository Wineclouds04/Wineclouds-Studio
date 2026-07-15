using Microsoft.UI.Xaml;
using WinecloudsStudio.Shared.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace WinecloudsStudio;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    internal Window? MainAppWindow => _window;

    public App()
    {
        if (!EnsureElevated())
        {
            Environment.Exit(0);
            return;
        }

        InitializeComponent();
        Logger.Init();
        Logger.Info("App", "Application starting");
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Closed += (_, _) =>
        {
            Logger.Info("App", "Window closed, shutting down");
            Logger.Shutdown();
        };
        _window.Activate();
        Logger.Info("App", "MainWindow activated");
    }

    private static bool EnsureElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            return true;
        }

        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            // The user cancelled the UAC prompt. End the medium-integrity launcher.
        }

        return false;
    }
}
