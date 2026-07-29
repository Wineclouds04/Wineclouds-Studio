using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Modules.Home.Pages;
using WinecloudsStudio.Modules.Reserved.ModuleD;
using WinecloudsStudio.Modules.Reserved.ModuleE;
using WinecloudsStudio.Modules.Reserved.ModuleF;
using WinecloudsStudio.Modules.ScreenDetection.Pages;
using WinecloudsStudio.Modules.Settings.Pages;
using WinecloudsStudio.Modules.Settings.Models;
using WinecloudsStudio.Modules.Navigation.Pages;
using WinecloudsStudio.Modules.SystemInformation.Pages;
using WinecloudsStudio.Modules.WindowManager.Pages;
using WinecloudsStudio.Shared;
using WinecloudsStudio.Shared.Logging;
using Microsoft.UI.Windowing;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinecloudsStudio;

/// <summary>
/// The application window. It hosts the static first-stage navigation shell.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Page> _modulePages = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        Logger.Info("MainWindow", "MainWindow initialization started");

        try
        {
            Logger.Info("MainWindow", "Loading XAML components");
            InitializeComponent();

            Logger.Info("MainWindow", "Configuring title bar");
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            Logger.Info("MainWindow", "Loading application icon");
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
            ConfigureInitialWindow();
            ApplyTheme((Application.Current as App)?.Settings.ThemeMode ?? AppThemeMode.System);
            VersionText.Text = BuildInfo.DisplayVersion;

            Logger.Info("MainWindow", "Navigating to home page");
            NavigateTo("home");
            NavigationView.SelectedItem = NavigationView.MenuItems[1];
            Logger.Info("MainWindow", "MainWindow initialization completed");
        }
        catch (Exception exception)
        {
            Logger.Error("MainWindow", $"MainWindow initialization failed: {exception}");
            throw;
        }
    }

    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is string navigationKey)
        {
            NavigateTo(navigationKey);
        }
    }

    private void NavigateTo(string navigationKey)
    {
        if (!_modulePages.TryGetValue(navigationKey, out Page? page))
        {
            page = navigationKey switch
            {
                "home" => new HomePage(),
                "system-information" => new SystemInformationPage(),
                "module-a" => new WindowManagerPage(),
                "module-b" => new ScreenDetectionPage(),
                "module-d" => new ModuleDPage(),
                "module-e" => new ModuleEPage(),
                "module-f" => new ModuleFPage(),
                "settings" => new SettingsPage(),
                _ => new UnavailablePage()
            };
            _modulePages[navigationKey] = page;
        }

        if (!ReferenceEquals(ContentFrame.Content, page))
            ContentFrame.Content = page;
    }

    internal async Task ShutdownModulesAsync()
    {
        foreach (ScreenDetectionPage page in _modulePages.Values.OfType<ScreenDetectionPage>())
            await page.ShutdownAsync();
    }

    internal void ApplyTheme(AppThemeMode themeMode)
    {
        RootLayout.RequestedTheme = themeMode switch
        {
            AppThemeMode.Light => ElementTheme.Light,
            AppThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void ConfigureInitialWindow()
    {
        const int preferredWidth = 1440;
        const int preferredHeight = 900;
        const int screenMargin = 48;

        DisplayArea displayArea = DisplayArea.GetFromWindowId(
            AppWindow.Id,
            DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;

        int width = Math.Min(preferredWidth, Math.Max(1, workArea.Width - (screenMargin * 2)));
        int height = Math.Min(preferredHeight, Math.Max(1, workArea.Height - (screenMargin * 2)));
        int x = workArea.X + ((workArea.Width - width) / 2);
        int y = workArea.Y + ((workArea.Height - height) / 2);

        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }
}
