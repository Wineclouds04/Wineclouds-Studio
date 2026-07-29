using Microsoft.UI.Xaml;
using WinecloudsStudio.Shared.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using WinecloudsStudio.Modules.Settings.Models;
using WinecloudsStudio.Modules.Settings.Services;
using WinecloudsStudio.Modules.WindowManager.Services.Implementation;

namespace WinecloudsStudio;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private TrayIconService? _trayIconService;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;
    private bool _exitRequested;

    internal Window? MainAppWindow => _window;
    internal ApplicationSettingsService SettingsService { get; } = new();
    internal ApplicationSettings Settings { get; private set; } = new();
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
        Settings = SettingsService.Load();
        WindowThumbnailManager = new ThumbnailManager();
        _window = new MainWindow();
        InitializeTrayIcon();
        _window.AppWindow.Closing += async (_, args) =>
        {
            if (_shutdownCompleted)
                return;

            args.Cancel = true;
            if (!_exitRequested && Settings.CloseBehavior == AppCloseBehavior.MinimizeToTray)
            {
                _window.AppWindow.Hide();
                _trayIconService?.ShowMinimizedNotification();
                Logger.Info("App", "MainWindow hidden to system tray");
                return;
            }

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
                _trayIconService?.Dispose();
                _trayIconService = null;
                WindowThumbnailManager.Stop();
                Logger.Shutdown();
                _shutdownCompleted = true;
                _window?.Close();
            }
        };
        _window.Activate();
        Logger.Info("App", "MainWindow activated");
    }

    internal void UpdateCloseBehavior(AppCloseBehavior closeBehavior)
    {
        ApplicationSettings updatedSettings = Settings with { CloseBehavior = closeBehavior };
        SettingsService.Save(updatedSettings);
        Settings = updatedSettings;
        _trayIconService?.SetVisible(closeBehavior == AppCloseBehavior.MinimizeToTray);
        Logger.Info("App", $"Close behavior changed to {closeBehavior}");
    }

    internal void UpdateThemeMode(AppThemeMode themeMode)
    {
        ApplicationSettings updatedSettings = Settings with { ThemeMode = themeMode };
        SettingsService.Save(updatedSettings);
        Settings = updatedSettings;

        if (_window is MainWindow mainWindow)
            mainWindow.ApplyTheme(themeMode);

        Logger.Info("App", $"Theme mode changed to {themeMode}");
    }

    private void InitializeTrayIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _trayIconService = new TrayIconService(iconPath);
        _trayIconService.SetVisible(Settings.CloseBehavior == AppCloseBehavior.MinimizeToTray);
        _trayIconService.ShowRequested += (_, _) => RunOnUiThread(ShowMainWindow);
        _trayIconService.ExitRequested += (_, _) => RunOnUiThread(RequestExit);
    }

    private void ShowMainWindow()
    {
        if (_window is null || _shutdownStarted)
            return;

        _window.AppWindow.Show();
        _window.Activate();
        Logger.Info("App", "MainWindow restored from system tray");
    }

    private void RequestExit()
    {
        if (_window is null || _shutdownStarted)
            return;

        _exitRequested = true;
        _window.Close();
    }

    private void RunOnUiThread(Action action)
    {
        if (_window?.DispatcherQueue.HasThreadAccess == true)
            action();
        else
            _window?.DispatcherQueue.TryEnqueue(() => action());
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
