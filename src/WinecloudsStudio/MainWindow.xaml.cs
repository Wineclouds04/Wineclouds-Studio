using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Modules.Home.Pages;
using WinecloudsStudio.Modules.Reserved.ModuleC;
using WinecloudsStudio.Modules.Reserved.ModuleD;
using WinecloudsStudio.Modules.Reserved.ModuleE;
using WinecloudsStudio.Modules.Reserved.ModuleF;
using WinecloudsStudio.Modules.ScreenDetection.Pages;
using WinecloudsStudio.Modules.Navigation.Pages;
using WinecloudsStudio.Modules.WindowManager.Pages;
using WinecloudsStudio.Shared.Logging;

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
                "module-a" => new WindowManagerPage(),
                "module-b" => new ScreenDetectionPage(),
                "module-c" => new ModuleCPage(),
                "module-d" => new ModuleDPage(),
                "module-e" => new ModuleEPage(),
                "module-f" => new ModuleFPage(),
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
}
