using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Modules.Settings.Models;
using WinecloudsStudio.Shared;

namespace WinecloudsStudio.Modules.Settings.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isInitializing = true;
    private bool _isSynchronizingTheme;

    public SettingsPage()
    {
        InitializeComponent();

        ApplicationSettings settings = (Application.Current as App)?.Settings ?? new ApplicationSettings();
        CloseBehaviorOptions.SelectedIndex =
            settings.CloseBehavior == AppCloseBehavior.MinimizeToTray ? 0 : 1;
        ThemeOptions.SelectedIndex = settings.ThemeMode switch
        {
            AppThemeMode.Light => 0,
            AppThemeMode.Dark => 1,
            _ => 2
        };
        PaletteThemeOptions.SelectedIndex = settings.ThemeMode switch
        {
            AppThemeMode.Light => 0,
            AppThemeMode.Dark => 1,
            _ => -1
        };
        UpdatePaletteCurrentThemeText(settings.ThemeMode);
        SettingsNavigation.SelectedItem = SettingsNavigation.MenuItems[1];
        AboutVersionText.Text = $"版本 {BuildInfo.DisplayVersion}";
        _isInitializing = false;
    }

    private void SettingsNavigation_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string section)
            return;

        GeneralSection.Visibility = section == "general" ? Visibility.Visible : Visibility.Collapsed;
        PaletteSection.Visibility = section == "palette" ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = section == "about" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ThemeOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _isSynchronizingTheme ||
            ThemeOptions.SelectedItem is not ListViewItem selectedOption)
            return;

        AppThemeMode themeMode = (selectedOption.Tag as string) switch
        {
            "light" => AppThemeMode.Light,
            "dark" => AppThemeMode.Dark,
            _ => AppThemeMode.System
        };

        ApplyThemeMode(themeMode);
    }

    private void PaletteThemeOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _isSynchronizingTheme ||
            PaletteThemeOptions.SelectedItem is not ListViewItem selectedOption)
            return;

        AppThemeMode themeMode = selectedOption.Tag as string == "dark"
            ? AppThemeMode.Dark
            : AppThemeMode.Light;
        ApplyThemeMode(themeMode);
    }

    private void ApplyThemeMode(AppThemeMode themeMode)
    {
        _isSynchronizingTheme = true;

        try
        {
            (Application.Current as App)?.UpdateThemeMode(themeMode);
            ThemeOptions.SelectedIndex = themeMode switch
            {
                AppThemeMode.Light => 0,
                AppThemeMode.Dark => 1,
                _ => 2
            };
            PaletteThemeOptions.SelectedIndex = themeMode switch
            {
                AppThemeMode.Light => 0,
                AppThemeMode.Dark => 1,
                _ => -1
            };
            UpdatePaletteCurrentThemeText(themeMode);

            string message = themeMode switch
            {
                AppThemeMode.Light => "已应用“极简白”主题。",
                AppThemeMode.Dark => "已应用“极夜黑”主题。",
                _ => "外观将跟随 Windows 系统设置。"
            };
            ShowSaveResult(true, message);
            ShowPaletteResult(true, message);
        }
        catch (Exception exception)
        {
            ShowSaveResult(false, exception.Message);
            ShowPaletteResult(false, exception.Message);
        }
        finally
        {
            _isSynchronizingTheme = false;
        }
    }

    private void CloseBehaviorOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || CloseBehaviorOptions.SelectedItem is not RadioButton selectedOption)
            return;

        AppCloseBehavior closeBehavior = selectedOption.Tag as string == "tray"
            ? AppCloseBehavior.MinimizeToTray
            : AppCloseBehavior.ExitApplication;

        try
        {
            (Application.Current as App)?.UpdateCloseBehavior(closeBehavior);
            ShowSaveResult(
                true,
                closeBehavior == AppCloseBehavior.MinimizeToTray
                    ? "关闭主窗口时，应用将最小化到系统托盘。"
                    : "关闭主窗口时，应用将直接退出。");
        }
        catch (Exception exception)
        {
            ShowSaveResult(false, exception.Message);
        }
    }

    private async void OpenProjectPageButton_Click(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/Wineclouds04/Wineclouds-Studio"));
    }

    private async void OpenLicenseButton_Click(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/Wineclouds04/Wineclouds-Studio/blob/main/LICENSE"));
    }

    private void SponsorButton_Click(object sender, RoutedEventArgs e)
    {
        SponsorInfoBar.IsOpen = true;
    }

    private void ShowSaveResult(bool succeeded, string message)
    {
        SaveStatusInfoBar.Severity = succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        SaveStatusInfoBar.Title = succeeded ? "设置已保存" : "设置保存失败";
        SaveStatusInfoBar.Message = message;
        SaveStatusInfoBar.IsOpen = true;
    }

    private void ShowPaletteResult(bool succeeded, string message)
    {
        PaletteStatusInfoBar.Severity = succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        PaletteStatusInfoBar.Title = succeeded ? "主题已应用" : "主题应用失败";
        PaletteStatusInfoBar.Message = message;
        PaletteStatusInfoBar.IsOpen = true;
    }

    private void UpdatePaletteCurrentThemeText(AppThemeMode themeMode)
    {
        PaletteCurrentThemeText.Text = themeMode switch
        {
            AppThemeMode.Light => "当前：极简白",
            AppThemeMode.Dark => "当前：极夜黑",
            _ => "当前：跟随系统"
        };
    }
}
