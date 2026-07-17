using Microsoft.UI.Xaml;
using WinecloudsStudio.Shared.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using WinecloudsStudio.Modules.Reserved.ModuleC;
using WinecloudsStudio.Modules.WindowManager.Services.Implementation;

namespace WinecloudsStudio;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;

    internal Window? MainAppWindow => _window;
    internal MultiWindowSyncEngine ModuleCSyncEngine { get; } = new();
    internal ThumbnailManager WindowThumbnailManager { get; private set; } = null!;

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
        WindowThumbnailManager = new ThumbnailManager();
        _window = new MainWindow();
        _window.AppWindow.Closing += async (_, args) =>
        {
            if (_shutdownCompleted)
                return;

            args.Cancel = true;
            if (_shutdownStarted)
                return;

            _shutdownStarted = true;
            Logger.Info("App", "Window closed, shutting down");
            try
            {
                if (_window is MainWindow mainWindow)
                    await mainWindow.ShutdownModulesAsync();
            }
            catch (Exception exception)
            {
                Logger.Error("App", $"Module shutdown failed: {exception}");
            }
            finally
            {
                WindowThumbnailManager.Stop();
                ModuleCSyncEngine.Dispose();
                Logger.Shutdown();
                _shutdownCompleted = true;
                _window?.Close();
            }
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
