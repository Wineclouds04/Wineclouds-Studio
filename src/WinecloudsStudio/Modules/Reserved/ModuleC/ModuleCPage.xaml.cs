using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Shared.Logging;

namespace WinecloudsStudio.Modules.Reserved.ModuleC;

public sealed partial class ModuleCPage : Page
{
    private readonly List<SyncWindowItem> _windows = [];
    private readonly List<BlockedKeyOption> _blockedKeyOptions = CreateBlockedKeyOptions();
    private bool _isLoaded;
    private MultiWindowSyncEngine Engine => ((App)Application.Current).ModuleCSyncEngine;

    public ModuleCPage()
    {
        Logger.Info("ModuleC", "Page constructor started");
        try
        {
            InitializeComponent();
            Logger.Info("ModuleC", "Page XAML initialized");
            Loaded += ModuleCPage_Loaded;
            Unloaded += ModuleCPage_Unloaded;
            Logger.Info("ModuleC", "Page constructor completed");
        }
        catch (Exception exception)
        {
            Logger.Error("ModuleC", $"Page constructor failed: {exception}");
            throw;
        }
    }

    private void ModuleCPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        Engine.StatusChanged -= Engine_StatusChanged;
        Engine.StatusChanged += Engine_StatusChanged;
        KeyboardToggle.IsOn = Engine.KeyboardEnabled;
        MouseToggle.IsOn = Engine.MouseEnabled;
        Logger.Info("ModuleC", "Page loaded; refreshing windows");
        try
        {
            RefreshWindows();
            ApplyEngineState();
            Logger.Info("ModuleC", "Initial window refresh completed");
        }
        catch (Exception exception)
        {
            Logger.Error("ModuleC", $"Initial window refresh failed: {exception}");
            StatusLabel.Text = $"加载窗口列表失败：{exception.Message}";
        }
    }

    private void ModuleCPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        Engine.StatusChanged -= Engine_StatusChanged;
    }

    private void RefreshWindowsButton_Click(object sender, RoutedEventArgs e) => RefreshWindows();

    private void RefreshWindows()
    {
        IntPtr selectedPrimary = _windows.FirstOrDefault(item => item.IsPrimary)?.Handle
            ?? Engine.PrimaryWindow;
        var selectedTargets = _windows.Where(item => item.IsTarget).Select(item => item.Handle).ToHashSet();
        if (selectedTargets.Count == 0)
            selectedTargets.UnionWith(Engine.TargetWindows);

        _windows.Clear();
        _windows.AddRange(SyncWindowEnumerator.GetCandidates());

        foreach (SyncWindowItem item in _windows)
        {
            item.IsPrimary = item.Handle == selectedPrimary;
            item.IsTarget = selectedTargets.Contains(item.Handle) && !item.IsPrimary;
        }

        ApplyFilter();
        if (!Engine.IsRunning)
        {
            StatusLabel.Text = _windows.Count == 0
                ? "未发现符合条件的窗口。"
                : "请选择主控窗口和至少一个受控窗口。";
        }
    }

    private void WindowFilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        string filter = WindowFilterBox?.Text.Trim() ?? string.Empty;
        List<SyncWindowItem> visible = string.IsNullOrEmpty(filter)
            ? _windows
            : _windows.Where(item => item.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.WindowTitle.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        PrimaryWindowList.ItemsSource = null;
        TargetWindowList.ItemsSource = null;
        PrimaryWindowList.ItemsSource = visible;
        TargetWindowList.ItemsSource = visible;
        WindowCountLabel.Text = string.IsNullOrEmpty(filter)
            ? $"共 {_windows.Count} 个可同步窗口"
            : $"显示 {visible.Count} / 共 {_windows.Count} 个可同步窗口";
    }

    private void PrimaryWindow_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SyncWindowItem primary }) return;

        foreach (SyncWindowItem item in _windows)
            item.IsPrimary = item.Handle == primary.Handle;
    }

    private void TargetWindow_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SyncWindowItem target } && !target.IsPrimary)
            target.IsTarget = true;
    }

    private void TargetWindow_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SyncWindowItem target })
            target.IsTarget = false;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        SyncWindowItem? primary = _windows.SingleOrDefault(item => item.IsPrimary);
        List<IntPtr> targets = _windows
            .Where(item => item.IsTarget && item.Handle != primary?.Handle)
            .Select(item => item.Handle)
            .Distinct()
            .ToList();
        Logger.Info("ModuleC", $"Start requested: primary={(primary == null ? "none" : primary.ProcessId)}, targets={targets.Count}");
        if (primary == null || targets.Count == 0)
        {
            StatusLabel.Text = "请先选择主控窗口和至少一个受控窗口。";
            Logger.Warn("ModuleC", "Start rejected because the primary window or target windows were not selected.");
            return;
        }

        try
        {
            Engine.KeyboardEnabled = KeyboardToggle.IsOn;
            Engine.MouseEnabled = MouseToggle.IsOn;
            Engine.Start(primary.Handle, targets, GetBlockedKeys());
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            SetEditingEnabled(false);
            Logger.Info("ModuleC", $"Started sync: primary={primary.ProcessId}, targets={targets.Count}");
        }
        catch (Exception exception)
        {
            StatusLabel.Text = exception.Message;
            Logger.Error("ModuleC", $"Failed to start sync: {exception}");
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopSync();

    private void StopSync()
    {
        Engine.Stop();
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        SetEditingEnabled(true);
    }

    private void SetEditingEnabled(bool enabled)
    {
        WindowFilterBox.IsEnabled = enabled;
        RefreshWindowsButton.IsEnabled = enabled;
        PrimaryWindowList.IsEnabled = enabled;
        TargetWindowList.IsEnabled = enabled;
        KeyboardToggle.IsEnabled = enabled;
        MouseToggle.IsEnabled = enabled;
        EditBlockedKeysButton.IsEnabled = enabled;
    }

    private void ApplyEngineState()
    {
        bool isRunning = Engine.IsRunning;
        StartButton.IsEnabled = !isRunning;
        StopButton.IsEnabled = isRunning;
        SetEditingEnabled(!isRunning);
        if (isRunning)
            StatusLabel.Text = "同步中：切换到主控窗口后开始操作。";
    }

    private IEnumerable<uint> GetBlockedKeys()
        => _blockedKeyOptions.Where(option => option.IsBlocked).SelectMany(option => option.VirtualKeys);

    private async void EditBlockedKeysButton_Click(object sender, RoutedEventArgs e)
    {
        var choices = new List<(BlockedKeyOption Option, CheckBox CheckBox)>();
        var content = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var selectAll = new Button { Content = "全选" };
        var clearAll = new Button { Content = "清空" };
        actions.Children.Add(selectAll);
        actions.Children.Add(clearAll);
        content.Children.Add(actions);

        foreach (IGrouping<string, BlockedKeyOption> group in _blockedKeyOptions.GroupBy(option => option.Group))
        {
            content.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 2)
            });

            var grid = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
            for (int column = 0; column < 4; column++)
                grid.ColumnDefinitions.Add(new ColumnDefinition());

            BlockedKeyOption[] options = group.ToArray();
            for (int index = 0; index < options.Length; index++)
            {
                if (index % 4 == 0)
                    grid.RowDefinitions.Add(new RowDefinition());

                BlockedKeyOption option = options[index];
                var checkBox = new CheckBox
                {
                    Content = option.Name,
                    IsChecked = option.IsBlocked,
                    MinWidth = 125
                };
                Grid.SetRow(checkBox, index / 4);
                Grid.SetColumn(checkBox, index % 4);
                grid.Children.Add(checkBox);
                choices.Add((option, checkBox));
            }

            content.Children.Add(grid);
        }

        selectAll.Click += (_, _) =>
        {
            foreach ((_, CheckBox checkBox) in choices)
                checkBox.IsChecked = true;
        };
        clearAll.Click += (_, _) =>
        {
            foreach ((_, CheckBox checkBox) in choices)
                checkBox.IsChecked = false;
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "不转发按键",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 440,
                MinWidth = 620
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        foreach ((BlockedKeyOption option, CheckBox checkBox) in choices)
            option.IsBlocked = checkBox.IsChecked == true;

    }

    private static List<BlockedKeyOption> CreateBlockedKeyOptions()
    {
        var options = new List<BlockedKeyOption>();
        static void Add(List<BlockedKeyOption> items, string group, string name, params uint[] virtualKeys) =>
            items.Add(new BlockedKeyOption(group, name, virtualKeys));

        for (uint key = 0x70; key <= 0x7B; key++)
            Add(options, "功能键", $"F{key - 0x6F}", key);

        Add(options, "修饰键", "Ctrl", 0xA2, 0xA3);
        Add(options, "修饰键", "Alt", 0xA4, 0xA5);
        Add(options, "修饰键", "Shift", 0xA0, 0xA1);
        Add(options, "修饰键", "Win", 0x5B, 0x5C);

        Add(options, "导航键", "↑ Up", 0x26);
        Add(options, "导航键", "↓ Down", 0x28);
        Add(options, "导航键", "← Left", 0x25);
        Add(options, "导航键", "→ Right", 0x27);
        Add(options, "导航键", "Insert", 0x2D);
        Add(options, "导航键", "Delete", 0x2E);
        Add(options, "导航键", "Home", 0x24);
        Add(options, "导航键", "End", 0x23);
        Add(options, "导航键", "Page Up", 0x21);
        Add(options, "导航键", "Page Down", 0x22);

        Add(options, "特殊键", "Esc", 0x1B);
        Add(options, "特殊键", "Tab", 0x09);
        Add(options, "特殊键", "Caps Lock", 0x14);
        Add(options, "特殊键", "Backspace", 0x08);
        Add(options, "特殊键", "Enter", 0x0D);
        Add(options, "特殊键", "Space", 0x20);
        Add(options, "特殊键", "Print Screen", 0x2C);
        Add(options, "特殊键", "Scroll Lock", 0x91);
        Add(options, "特殊键", "Pause", 0x13);
        Add(options, "特殊键", "Apps/Menu", 0x5D);

        for (uint key = 0x30; key <= 0x39; key++)
            Add(options, "数字键", ((char)key).ToString(), key);

        Add(options, "标点符号", "` (Backtick)", 0xC0);
        Add(options, "标点符号", "- (Minus)", 0xBD);
        Add(options, "标点符号", "= (Equals)", 0xBB);
        Add(options, "标点符号", "[ (Bracket)", 0xDB);
        Add(options, "标点符号", "] (Bracket)", 0xDD);
        Add(options, "标点符号", "\\ (Backslash)", 0xDC);
        Add(options, "标点符号", "; (Semicolon)", 0xBA);
        Add(options, "标点符号", "' (Quote)", 0xDE);
        Add(options, "标点符号", ", (Comma)", 0xBC);
        Add(options, "标点符号", ". (Period)", 0xBE);
        Add(options, "标点符号", "/ (Slash)", 0xBF);

        for (uint key = 0x41; key <= 0x5A; key++)
            Add(options, "字母键", ((char)key).ToString(), key);

        Add(options, "数字键盘", "Num Lock", 0x90);
        Add(options, "数字键盘", "Num /", 0x6F);
        Add(options, "数字键盘", "Num *", 0x6A);
        Add(options, "数字键盘", "Num -", 0x6D);
        Add(options, "数字键盘", "Num +", 0x6B);
        Add(options, "数字键盘", "Num . (Del)", 0x6E);
        return options;
    }

    private void Engine_StatusChanged(object? sender, string text)
    {
        if (!_isLoaded) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isLoaded) StatusLabel.Text = text;
        });
    }
}

internal sealed class BlockedKeyOption(string group, string name, uint[] virtualKeys)
{
    public string Group { get; } = group;
    public string Name { get; } = name;
    public uint[] VirtualKeys { get; } = virtualKeys;
    public bool IsBlocked { get; set; }
}
