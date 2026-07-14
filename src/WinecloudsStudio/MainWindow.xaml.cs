using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Pages;

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
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        NavigateTo("home");
        NavigationView.SelectedItem = NavigationView.MenuItems[1];
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
