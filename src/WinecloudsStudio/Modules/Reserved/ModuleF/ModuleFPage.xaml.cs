using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using WinecloudsStudio.Shared;
using WinecloudsStudio.Shared.Logging;

namespace WinecloudsStudio.Modules.Reserved.ModuleF;

public sealed partial class ModuleFPage : Page
{
    private readonly GitHubUpdateService _updateService = new();
    private CancellationTokenSource? _operationCancellation;
    private UpdateReleaseInfo? _latestRelease;
    private bool _automaticCheckStarted;
    private bool _isBusy;

    public ModuleFPage()
    {
        InitializeComponent();
        CurrentVersionText.Text = BuildInfo.DisplayVersion;
        Loaded += ModuleFPage_Loaded;
    }

    private async void ModuleFPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_automaticCheckStarted)
        {
            return;
        }

        _automaticCheckStarted = true;
        await CheckForUpdatesAsync();
    }

    private async void CheckUpdatesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_isBusy)
        {
            return;
        }

        CancellationToken token = BeginOperation();
        SetBusy(true);
        SetStatus(
            InfoBarSeverity.Informational,
            "正在检查",
            "正在连接 GitHub Releases 获取最新正式版本。");

        try
        {
            UpdateReleaseInfo release =
                await _updateService.GetLatestReleaseAsync(token);
            _latestRelease = release;
            ShowRelease(release);

            if (release.Version
                > GitHubUpdateService.CurrentVersion)
            {
                DownloadAndInstallButton.IsEnabled = true;
                SetStatus(
                    InfoBarSeverity.Success,
                    "发现新版本",
                    $"可以从当前版本更新到 {release.TagName}。");
            }
            else
            {
                DownloadAndInstallButton.IsEnabled = false;
                SetStatus(
                    InfoBarSeverity.Success,
                    "已经是最新版本",
                    $"当前版本不低于 GitHub 正式版 {release.TagName}。");
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus(
                InfoBarSeverity.Warning,
                "检查已取消",
                "更新检查已取消。");
        }
        catch (Exception exception)
        {
            Logger.Error(
                "Updater",
                $"Update check failed: {exception}");
            SetStatus(
                InfoBarSeverity.Error,
                "检查失败",
                GetFriendlyErrorMessage(exception));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void DownloadAndInstallButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_latestRelease == null || _isBusy)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"安装 {_latestRelease.TagName}",
            Content =
                "安装包下载并通过更新清单中的 SHA-256 校验后，"
                + "Wineclouds Studio 将自动退出并启动安装向导。"
                + "是否继续？",
            PrimaryButtonText = "下载并安装",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        CancellationToken token = BeginOperation();
        SetBusy(true);
        DownloadProgressPanel.Visibility = Visibility.Visible;
        CancelDownloadButton.Visibility = Visibility.Visible;
        DownloadProgressBar.IsIndeterminate = true;
        DownloadProgressBar.Value = 0;
        DownloadProgressText.Text = "正在连接下载服务器…";
        SetStatus(
            InfoBarSeverity.Informational,
            "正在下载",
            $"正在下载 {_latestRelease.Installer.Name}。");

        var progress =
            new Progress<UpdateDownloadProgress>(
                UpdateDownloadProgressChanged);

        try
        {
            string installerPath =
                await _updateService.DownloadInstallerAsync(
                    _latestRelease,
                    progress,
                    token);

            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = 100;
            DownloadProgressText.Text =
                "下载完成，SHA-256 校验通过。";
            SetStatus(
                InfoBarSeverity.Success,
                "准备安装",
                "程序退出后将自动启动 NSIS 安装向导。");

            UpdateInstallerLauncher
                .LaunchAfterCurrentProcessExits(installerPath);

            await Task.Delay(300);
            ((App)Application.Current).MainAppWindow?.Close();
        }
        catch (OperationCanceledException)
        {
            SetStatus(
                InfoBarSeverity.Warning,
                "下载已取消",
                "未安装任何更新。");
            DownloadProgressText.Text = "下载已取消。";
        }
        catch (Exception exception)
        {
            Logger.Error(
                "Updater",
                $"Update download failed: {exception}");
            SetStatus(
                InfoBarSeverity.Error,
                "下载失败",
                GetFriendlyErrorMessage(exception));
            DownloadProgressText.Text = "下载失败，请重试。";
        }
        finally
        {
            CancelDownloadButton.Visibility =
                Visibility.Collapsed;
            SetBusy(false);
            if (_latestRelease != null
                && _latestRelease.Version
                > GitHubUpdateService.CurrentVersion)
            {
                DownloadAndInstallButton.IsEnabled = true;
            }
        }
    }

    private void CancelDownloadButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _operationCancellation?.Cancel();
    }

    private async void OpenReleasePageButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_latestRelease == null)
        {
            return;
        }

        await Launcher.LaunchUriAsync(
            _latestRelease.ReleasePageUri);
    }

    private void ShowRelease(UpdateReleaseInfo release)
    {
        LatestVersionText.Text = release.TagName;
        PublishedAtText.Text = release.PublishedAt.HasValue
            ? $"发布于 {release.PublishedAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
            : "GitHub 正式版本";
        ReleaseNameText.Text = release.Name;
        ReleaseNotesText.Text =
            string.IsNullOrWhiteSpace(release.ReleaseNotes)
                ? "此版本没有填写发布说明。"
                : release.ReleaseNotes.Trim();
        InstallerInfoText.Text =
            $"{release.Installer.Name} · "
            + $"{FormatBytes(release.Installer.Size)}";
        ReleaseDetailsCard.Visibility = Visibility.Visible;
        OpenReleasePageButton.IsEnabled = true;
    }

    private void UpdateDownloadProgressChanged(
        UpdateDownloadProgress progress)
    {
        if (progress.Percentage.HasValue)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value =
                progress.Percentage.Value;
        }
        else
        {
            DownloadProgressBar.IsIndeterminate = true;
        }

        string total = progress.TotalBytes.HasValue
            ? $" / {FormatBytes(progress.TotalBytes.Value)}"
            : string.Empty;
        DownloadProgressText.Text =
            $"{FormatBytes(progress.BytesReceived)}{total}";
    }

    private CancellationToken BeginOperation()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation =
            new CancellationTokenSource();
        return _operationCancellation.Token;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        CheckUpdatesButton.IsEnabled = !busy;
        CheckProgressRing.IsActive = busy;
        CheckProgressRing.Visibility =
            busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            DownloadAndInstallButton.IsEnabled = false;
        }
    }

    private void SetStatus(
        InfoBarSeverity severity,
        string title,
        string message)
    {
        StatusInfoBar.Severity = severity;
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private static string GetFriendlyErrorMessage(
        Exception exception) =>
        exception switch
        {
            HttpRequestException =>
                "无法连接 GitHub，请检查网络或代理设置。",
            InvalidDataException dataError =>
                dataError.Message,
            InvalidOperationException operationError =>
                operationError.Message,
            _ => "更新服务发生异常，请稍后重试并查看日志。"
        };

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        double value = Math.Max(0, bytes);
        int unitIndex = 0;
        while (value >= 1024
               && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
