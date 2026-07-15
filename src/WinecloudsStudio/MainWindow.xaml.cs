using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Pages;
using WinecloudsStudio.Services.Implementation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinecloudsStudio;

/// <summary>
/// The application window. It hosts the static first-stage navigation shell.
/// </summary>
public sealed partial class MainWindow : Window
{
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
        var pageType = navigationKey switch
        {
            "home" => typeof(HomePage),
            "module-a" => typeof(ModuleAPage),
            "module-b" => typeof(ModuleBPage),
            "module-c" => typeof(ModuleCPage),
            "module-d" => typeof(ModuleDPage),
            "module-e" => typeof(ModuleEPage),
            "module-f" => typeof(ModuleFPage),
            _ => typeof(UnavailablePage)
        };

        ContentFrame.Navigate(pageType);
    }
}
