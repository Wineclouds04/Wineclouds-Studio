using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Services.Implementation;

namespace WinecloudsStudio.Pages;

/// <summary>
/// Window Manager — Module A control panel.
/// Lists all active windowed processes, lets the user select which ones
/// to monitor, and creates live DWM thumbnail popups for each.
/// </summary>
public sealed partial class ModuleAPage : Page
{
    private readonly ThumbnailManager _thumbnailManager;
    private readonly List<ProcessItem> _processItems = new();

    public ModuleAPage()
    {
        InitializeComponent();
        _thumbnailManager = new ThumbnailManager();

        Unloaded += (s, e) => _thumbnailManager.Stop();

        OpacitySlider.ValueChanged += (s, e) =>
        {
            double val = OpacitySlider.Value / 10.0;
            OpacityValueLabel.Text = val.ToString("F1");
            _thumbnailManager.ThumbnailOpacity = val;
        };

        Loaded += (s, e) => RefreshProcessList();
    }

    private void ShowBorderCheck_Click(object sender, RoutedEventArgs e)
    {
        _thumbnailManager.ShowBorder = ShowBorderCheck.IsChecked ?? false;
    }

    private void RefreshProcessList()
    {
        _processItems.Clear();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                IntPtr hwnd = process.MainWindowHandle;
                string windowTitle = process.MainWindowTitle;

                if (hwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(windowTitle))
                    continue;

                string processName = process.ProcessName;

                // Skip our own process
                if (processName.Equals("WinecloudsStudio", StringComparison.OrdinalIgnoreCase))
                    continue;

                _processItems.Add(new ProcessItem
                {
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    DisplayText = $"{processName}  —  {windowTitle}"
                });
            }
            catch
            {
                continue;
            }
        }

        _processItems.Sort((a, b) => string.CompareOrdinal(
            a.DisplayText, b.DisplayText));

        ProcessList.ItemsSource = null;
        ProcessList.ItemsSource = _processItems;

        ProcessCountLabel.Text = $"共 {_processItems.Count} 个带窗口的进程";
    }

    private void RefreshProcessesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProcessList();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _processItems.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0) return;

        ApplySettings();

        foreach (var item in selected)
        {
            _thumbnailManager.AddMonitoredProcess(item.ProcessName);
        }

        _thumbnailManager.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _thumbnailManager.Stop();

        // Clear registrations
        var selected = _processItems.Where(p => p.IsSelected).ToList();
        foreach (var item in selected)
        {
            _thumbnailManager.RemoveMonitoredProcess(item.ProcessName);
        }

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    private void ApplySettings()
    {
        _thumbnailManager.ThumbnailWidth = (int)ThumbWidthBox.Value;
        _thumbnailManager.ThumbnailHeight = (int)ThumbHeightBox.Value;
        _thumbnailManager.ThumbnailOpacity = OpacitySlider.Value / 10.0;
        _thumbnailManager.AlwaysOnTop = AlwaysOnTopCheck.IsChecked ?? true;
        _thumbnailManager.ShowBorder = ShowBorderCheck.IsChecked ?? false;
    }
}
