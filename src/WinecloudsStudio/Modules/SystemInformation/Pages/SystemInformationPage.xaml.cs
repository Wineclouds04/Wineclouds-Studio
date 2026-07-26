using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Modules.SystemInformation.Models;
using WinecloudsStudio.Modules.SystemInformation.Services;
using WinecloudsStudio.Shared.Logging;

namespace WinecloudsStudio.Modules.SystemInformation.Pages;

public sealed partial class SystemInformationPage : Page
{
    private readonly HardwareInformationService _service =
        new();
    private readonly DispatcherQueueTimer _uptimeTimer;
    private CancellationTokenSource? _loadCancellation;
    private bool _initialLoadStarted;

    public SystemInformationPage()
    {
        InitializeComponent();

        _uptimeTimer = DispatcherQueue.CreateTimer();
        _uptimeTimer.Interval = TimeSpan.FromSeconds(1);
        _uptimeTimer.Tick += (_, _) => UpdateUptime();

        Loaded += SystemInformationPage_Loaded;
        Unloaded += SystemInformationPage_Unloaded;
    }

    private async void SystemInformationPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        UpdateUptime();
        _uptimeTimer.Start();

        if (_initialLoadStarted)
        {
            return;
        }

        _initialLoadStarted = true;
        await LoadHardwareInformationAsync();
    }

    private void SystemInformationPage_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _loadCancellation?.Cancel();
        _uptimeTimer.Stop();
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await LoadHardwareInformationAsync();
    }

    private async Task LoadHardwareInformationAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken =
            _loadCancellation.Token;

        SetLoadingState(isLoading: true);
        StatusInfoBar.IsOpen = true;
        StatusInfoBar.Severity =
            InfoBarSeverity.Informational;
        StatusInfoBar.Title = "正在读取";
        StatusInfoBar.Message =
            "正在从 Windows 获取本机硬件信息。";

        try
        {
            HardwareInformationSnapshot snapshot =
                await _service.LoadAsync(
                    cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplySnapshot(snapshot);

            StatusInfoBar.IsOpen = true;
            if (snapshot.Warnings.Count == 0)
            {
                StatusInfoBar.Severity =
                    InfoBarSeverity.Success;
                StatusInfoBar.Title = "读取完成";
                StatusInfoBar.Message =
                    "本机硬件信息已更新。";
            }
            else
            {
                StatusInfoBar.Severity =
                    InfoBarSeverity.Warning;
                StatusInfoBar.Title = "部分信息不可用";
                StatusInfoBar.Message = string.Join(
                    "、",
                    snapshot.Warnings);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.Error(
                "HardwareInformation",
                $"Unable to load hardware information: {exception}");
            StatusInfoBar.IsOpen = true;
            StatusInfoBar.Severity =
                InfoBarSeverity.Error;
            StatusInfoBar.Title = "读取失败";
            StatusInfoBar.Message =
                "无法读取本机硬件信息，请稍后重试。";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SetLoadingState(isLoading: false);
            }
        }
    }

    private void ApplySnapshot(
        HardwareInformationSnapshot snapshot)
    {
        ModelSummaryText.Text = snapshot.ModelSummary;
        OperatingSystemText.Text =
            snapshot.OperatingSystemSummary;
        ProcessorText.Text = snapshot.Processor;
        MotherboardText.Text = snapshot.Motherboard;
        MemoryText.Text = snapshot.Memory;
        GraphicsText.Text = JoinLines(
            snapshot.GraphicsAdapters);
        DisplaysText.Text = JoinLines(snapshot.Displays);
        StorageText.Text = JoinLines(
            snapshot.StorageDevices);
        AudioText.Text = JoinLines(snapshot.AudioDevices);
        NetworkText.Text = JoinLines(
            snapshot.NetworkAdapters);
        RefreshedAtText.Text =
            $"更新于 {snapshot.RefreshedAt:HH:mm:ss}";
        UpdateUptime();
    }

    private void SetLoadingState(bool isLoading)
    {
        RefreshButton.IsEnabled = !isLoading;
        LoadingRing.IsActive = isLoading;
        LoadingRing.Visibility = isLoading
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateUptime()
    {
        TimeSpan uptime = TimeSpan.FromMilliseconds(
            Environment.TickCount64);
        UptimeText.Text =
            $"{uptime.Days}天"
            + $"{uptime.Hours}小时"
            + $"{uptime.Minutes}分钟"
            + $"{uptime.Seconds}秒";
    }

    private static string JoinLines(
        IReadOnlyList<string> values) =>
        string.Join(
            Environment.NewLine,
            values);
}
