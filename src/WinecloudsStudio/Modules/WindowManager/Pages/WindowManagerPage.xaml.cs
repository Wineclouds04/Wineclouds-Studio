using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Shared.Logging;
using WinecloudsStudio.Modules.WindowManager.Configuration;
using WinecloudsStudio.Modules.WindowManager.Models;
using WinecloudsStudio.Modules.WindowManager.Services.Implementation;
using WindowGroupItem = WinecloudsStudio.Modules.WindowManager.Models.GroupItem;

namespace WinecloudsStudio.Modules.WindowManager.Pages;

/// <summary>
/// Window Manager — Module A control panel.
/// Lists all active windowed processes, lets the user select which ones
/// to monitor, creates live DWM thumbnail popups, and manages window
/// groups with hotkey cycling.
/// </summary>
public sealed partial class WindowManagerPage : Page
{
    private readonly ThumbnailManager _thumbnailManager;
    private readonly WindowManagerConfigStore _configStore;
    private readonly List<ProcessItem> _processItems = new();

    // ---- Group state ----
    private readonly List<WindowGroupConfig> _groups = new();
    private readonly List<WindowGroupItem> _groupItems = new();
    private int _editingGroupIndex = -1; // -1 = new group
    private WindowGroupConfig? _editingGroupDraft;
    private int _selectedGroupIndex = -1;
    private int _selectedWindowIndex = -1; // for reordering within group editor

    public WindowManagerPage()
    {
        InitializeComponent();
        _thumbnailManager = new ThumbnailManager();
        _configStore = new WindowManagerConfigStore();

        // Restore saved settings from the previous session
        LoadSavedSettings();

        Unloaded += (s, e) =>
        {
            SaveCurrentSettings();
            _thumbnailManager.Stop();
        };

        OpacitySlider.ValueChanged += (s, e) =>
        {
            double val = OpacitySlider.Value / 10.0;
            OpacityValueLabel.Text = val.ToString("F1");
            _thumbnailManager.ThumbnailOpacity = val;
        };

        Loaded += (s, e) => RefreshProcessList();
    }

    // ---- Process list ----

    private void ShowBorderCheck_Click(object sender, RoutedEventArgs e)
    {
        _thumbnailManager.ShowBorder = ShowBorderCheck.IsChecked ?? false;
    }

    private void LockThumbnailPositionCheck_Click(object sender, RoutedEventArgs e)
    {
        _thumbnailManager.LockThumbnailPosition = LockThumbnailPositionCheck.IsChecked ?? false;
    }

    private void SnapThumbnailsToGridCheck_Click(object sender, RoutedEventArgs e)
    {
        _thumbnailManager.SnapThumbnailsToGrid = SnapThumbnailsToGridCheck.IsChecked ?? false;
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

        ApplyProcessFilter();
    }

    private void RefreshProcessesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProcessList();
    }

    private void ProcessFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyProcessFilter();
    }

    private void ApplyProcessFilter()
    {
        string filterText = ProcessFilterBox.Text.Trim();
        List<ProcessItem> filteredItems = string.IsNullOrEmpty(filterText)
            ? _processItems
            : _processItems.Where(item =>
                    item.ProcessName.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    || item.WindowTitle.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        ProcessList.ItemsSource = null;
        ProcessList.ItemsSource = filteredItems;
        ProcessCountLabel.Text = string.IsNullOrEmpty(filterText)
            ? $"共 {_processItems.Count} 个带窗口的进程"
            : $"显示 {filteredItems.Count} / 共 {_processItems.Count} 个带窗口的进程";
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _processItems.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0) return;

        Logger.Info("ModuleA", $"Start monitoring: {selected.Count} processes, {_groups.Count} groups");
        ApplySettings();

        foreach (var item in selected)
        {
            _thumbnailManager.AddMonitoredProcess(item.ProcessName);
        }

        // Sync group config to the thumbnail manager before starting
        _thumbnailManager.SetGroups(_groups);

        _thumbnailManager.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        Logger.Info("ModuleA", "Stop monitoring requested");
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
        _thumbnailManager.LockThumbnailPosition = LockThumbnailPositionCheck.IsChecked ?? false;
        _thumbnailManager.SnapThumbnailsToGrid = SnapThumbnailsToGridCheck.IsChecked ?? false;
        _thumbnailManager.ShowBorder = ShowBorderCheck.IsChecked ?? false;
    }

    // ---- Settings persistence ----

    private void LoadSavedSettings()
    {
        var config = _configStore.Load();

        ThumbWidthBox.Value = config.ThumbnailWidth;
        ThumbHeightBox.Value = config.ThumbnailHeight;
        OpacitySlider.Value = config.ThumbnailOpacity * 10.0;
        AlwaysOnTopCheck.IsChecked = config.AlwaysOnTop;
        LockThumbnailPositionCheck.IsChecked = config.LockThumbnailPosition;
        SnapThumbnailsToGridCheck.IsChecked = config.SnapThumbnailsToGrid;
        ShowBorderCheck.IsChecked = config.ShowBorder;

        _thumbnailManager.ThumbnailOpacity = config.ThumbnailOpacity;

        // Restore groups
        _groups.Clear();
        if (config.Groups != null)
        {
            _groups.AddRange(config.Groups);
            foreach (WindowGroupConfig group in _groups)
            {
                NormalizeSingleKey(group.ForwardHotkey);
                NormalizeSingleKey(group.BackwardHotkey);
            }
        }

        RefreshGroupList();
    }

    private void SaveCurrentSettings()
    {
        var config = new WindowManagerConfig
        {
            ThumbnailWidth = (int)ThumbWidthBox.Value,
            ThumbnailHeight = (int)ThumbHeightBox.Value,
            ThumbnailOpacity = OpacitySlider.Value / 10.0,
            AlwaysOnTop = AlwaysOnTopCheck.IsChecked ?? true,
            LockThumbnailPosition = LockThumbnailPositionCheck.IsChecked ?? false,
            SnapThumbnailsToGrid = SnapThumbnailsToGridCheck.IsChecked ?? false,
            ShowBorder = ShowBorderCheck.IsChecked ?? false,
            Groups = new List<WindowGroupConfig>(_groups),
        };

        _configStore.Save(config);
        Logger.Info("ModuleA", $"Settings saved: {ThumbWidthBox.Value}x{ThumbHeightBox.Value}, {_groups.Count} groups");
    }

    // ---- Group list UI ----

    private void RefreshGroupList()
    {
        _groupItems.Clear();

        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];
            _groupItems.Add(new WindowGroupItem
            {
                GroupIndex = i,
                GroupName = string.IsNullOrWhiteSpace(g.Name) ? "(未命名)" : g.Name,
                DetailsText = $"{g.WindowKeys.Count} 个窗口",
                ForwardHotkeyText = FormatHotkeyDisplay(g.ForwardHotkey),
                BackwardHotkeyText = FormatHotkeyDisplay(g.BackwardHotkey),
            });
        }

        GroupList.ItemsSource = null;
        GroupList.ItemsSource = _groupItems;
        GroupCountLabel.Text = $"共 {_groups.Count} 个分组";
    }

    private void GroupList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WindowGroupItem item)
        {
            _selectedGroupIndex = item.GroupIndex;
            EditGroupButton.IsEnabled = true;
            DeleteGroupButton.IsEnabled = true;
        }
    }

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        _editingGroupIndex = -1;
        _editingGroupDraft = new WindowGroupConfig();
        ClearGroupEditor();
        PopulateGroupWindowList(_editingGroupDraft);
        PopulateAvailableWindowsCombo();
        GroupEditorPanel.Visibility = Visibility.Visible;
    }

    private void EditGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroupIndex < 0 || _selectedGroupIndex >= _groups.Count) return;

        _editingGroupIndex = _selectedGroupIndex;
        _editingGroupDraft = CloneGroup(_groups[_selectedGroupIndex]);
        var group = _editingGroupDraft;

        GroupNameBox.Text = group.Name;
        ForwardHotkeyDisplay.Text = FormatHotkeyDisplay(group.ForwardHotkey);
        BackwardHotkeyDisplay.Text = FormatHotkeyDisplay(group.BackwardHotkey);

        PopulateGroupWindowList(group);
        PopulateAvailableWindowsCombo();

        GroupEditorPanel.Visibility = Visibility.Visible;
    }

    private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroupIndex < 0 || _selectedGroupIndex >= _groups.Count) return;

        _groups.RemoveAt(_selectedGroupIndex);
        SaveCurrentSettings();

        // Update hotkeys if monitoring is running
        if (_thumbnailManager.IsRunning)
            _thumbnailManager.SetGroups(_groups);

        _selectedGroupIndex = -1;
        EditGroupButton.IsEnabled = false;
        DeleteGroupButton.IsEnabled = false;

        RefreshGroupList();
        ClearGroupEditor();
        GroupEditorPanel.Visibility = Visibility.Collapsed;
    }

    // ---- Group editor ----

    private void ClearGroupEditor()
    {
        GroupNameBox.Text = string.Empty;
        ForwardHotkeyDisplay.Text = "未设置";
        BackwardHotkeyDisplay.Text = "未设置";
        _selectedWindowIndex = -1;
        GroupValidationLabel.Visibility = Visibility.Collapsed;

        // Clear window list
        GroupWindowList.ItemsSource = null;
        AddWindowComboBox.ItemsSource = null;
    }

    private void PopulateGroupWindowList(WindowGroupConfig group)
    {
        var items = new List<ProcessItem>();
        foreach (var key in group.WindowKeys)
        {
            items.Add(new ProcessItem
            {
                DisplayText = key,
                ProcessName = key,
                WindowTitle = string.Empty,
            });
        }
        GroupWindowList.ItemsSource = null;
        GroupWindowList.ItemsSource = items;
    }

    private void PopulateAvailableWindowsCombo()
    {
        // Show all processes from the main list, excluding ones already in the editing group
        var editingGroup = _editingGroupDraft;

        var existingKeys = editingGroup?.WindowKeys
            .Select(k => k.ToLowerInvariant())
            .ToHashSet() ?? new HashSet<string>();

        var items = new List<string>();
        foreach (var p in _processItems)
        {
            string key = MakeWindowKey(p);
            if (!existingKeys.Contains(key.ToLowerInvariant()))
                items.Add(key);
        }

        AddWindowComboBox.ItemsSource = null;
        AddWindowComboBox.ItemsSource = items;
        if (items.Count > 0)
            AddWindowComboBox.SelectedIndex = 0;
    }

    private void AddWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (AddWindowComboBox.SelectedItem is string key)
        {
            var group = GetEditingGroup();
            if (!group.WindowKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                group.WindowKeys.Add(key);
                _selectedWindowIndex = group.WindowKeys.Count - 1;
                PopulateGroupWindowList(group);
                PopulateAvailableWindowsCombo(); // refresh combo to exclude this item
            }
        }
    }

    private void RemoveWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var group = GetEditingGroup();
        if (_selectedWindowIndex < 0 || _selectedWindowIndex >= group.WindowKeys.Count) return;

        group.WindowKeys.RemoveAt(_selectedWindowIndex);
        if (_selectedWindowIndex >= group.WindowKeys.Count)
            _selectedWindowIndex = group.WindowKeys.Count - 1;

        PopulateGroupWindowList(group);
        PopulateAvailableWindowsCombo(); // refresh combo to include this item again
    }

    private void GroupWindowList_ItemClick(object sender, ItemClickEventArgs e)
    {
        // Track the selected index for reordering and removal
        if (e.ClickedItem is ProcessItem item)
        {
            var group = GetEditingGroup();
            _selectedWindowIndex = group.WindowKeys.FindIndex(
                k => k.Equals(item.ProcessName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        var group = GetEditingGroup();
        if (_selectedWindowIndex <= 0 || _selectedWindowIndex >= group.WindowKeys.Count) return;

        // Swap with previous
        var temp = group.WindowKeys[_selectedWindowIndex];
        group.WindowKeys[_selectedWindowIndex] = group.WindowKeys[_selectedWindowIndex - 1];
        group.WindowKeys[_selectedWindowIndex - 1] = temp;
        _selectedWindowIndex--;

        PopulateGroupWindowList(group);
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        var group = GetEditingGroup();
        if (_selectedWindowIndex < 0 || _selectedWindowIndex >= group.WindowKeys.Count - 1) return;

        // Swap with next
        var temp = group.WindowKeys[_selectedWindowIndex];
        group.WindowKeys[_selectedWindowIndex] = group.WindowKeys[_selectedWindowIndex + 1];
        group.WindowKeys[_selectedWindowIndex + 1] = temp;
        _selectedWindowIndex++;

        PopulateGroupWindowList(group);
    }

    private void SaveGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var group = GetEditingGroup();
        group.Name = GroupNameBox.Text.Trim();

        if (!ValidateSingleKeyBindings(group, out string validationMessage))
        {
            GroupValidationLabel.Text = validationMessage;
            GroupValidationLabel.Visibility = Visibility.Visible;
            return;
        }

        if (_editingGroupIndex < 0)
        {
            _groups.Add(group);
        }
        else if (_editingGroupIndex < _groups.Count)
        {
            _groups[_editingGroupIndex] = group;
        }

        SaveCurrentSettings();

        // Update hotkeys if monitoring is running
        if (_thumbnailManager.IsRunning)
            _thumbnailManager.SetGroups(_groups);

        _editingGroupIndex = -1;
        _editingGroupDraft = null;

        RefreshGroupList();
        ClearGroupEditor();
        GroupEditorPanel.Visibility = Visibility.Collapsed;
    }

    private void CancelGroupButton_Click(object sender, RoutedEventArgs e)
    {
        _editingGroupIndex = -1;
        _editingGroupDraft = null;
        ClearGroupEditor();
        GroupEditorPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Gets or creates the group currently being edited.
    /// </summary>
    private WindowGroupConfig GetEditingGroup()
    {
        return _editingGroupDraft
               ?? throw new InvalidOperationException("No window group is currently being edited.");
    }

    private static WindowGroupConfig CloneGroup(WindowGroupConfig source)
    {
        return new WindowGroupConfig
        {
            Name = source.Name,
            WindowKeys = new List<string>(source.WindowKeys),
            ForwardHotkey = CloneHotkey(source.ForwardHotkey),
            BackwardHotkey = CloneHotkey(source.BackwardHotkey),
        };
    }

    private static HotkeyBinding? CloneHotkey(HotkeyBinding? source)
    {
        return source == null
            ? null
            : new HotkeyBinding
            {
                Modifiers = 0,
                VirtualKey = source.VirtualKey,
            };
    }

    private void SelectForwardKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement target)
        {
            ShowKeySelectionMenu(target, forward: true);
        }
    }

    private void SelectBackwardKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement target)
        {
            ShowKeySelectionMenu(target, forward: false);
        }
    }

    private void ShowKeySelectionMenu(FrameworkElement target, bool forward)
    {
        var menu = new MenuFlyout();
        var clearItem = new MenuFlyoutItem { Text = "清除绑定" };
        clearItem.Click += (_, _) => SetSelectedKey(forward, null);
        menu.Items.Add(clearItem);
        menu.Items.Add(new MenuFlyoutSeparator());

        foreach (KeyboardKeyCategory category in KeyboardKeyCatalog.Categories)
        {
            var categoryItem = new MenuFlyoutSubItem { Text = category.Name };
            foreach (KeyboardKeyOption key in category.Keys)
            {
                var keyItem = new MenuFlyoutItem { Text = key.Name };
                keyItem.Click += (_, _) => SetSelectedKey(forward, key);
                categoryItem.Items.Add(keyItem);
            }
            menu.Items.Add(categoryItem);
        }

        menu.ShowAt(target);
    }

    private void SetSelectedKey(bool forward, KeyboardKeyOption? key)
    {
        WindowGroupConfig group = GetEditingGroup();
        HotkeyBinding? binding = key == null
            ? null
            : new HotkeyBinding { Modifiers = 0, VirtualKey = key.VirtualKey };

        if (forward)
        {
            group.ForwardHotkey = binding;
            ForwardHotkeyDisplay.Text = FormatHotkeyDisplay(binding);
        }
        else
        {
            group.BackwardHotkey = binding;
            BackwardHotkeyDisplay.Text = FormatHotkeyDisplay(binding);
        }

        GroupValidationLabel.Visibility = Visibility.Collapsed;
    }

    private bool ValidateSingleKeyBindings(WindowGroupConfig group, out string message)
    {
        uint forwardKey = group.ForwardHotkey?.VirtualKey ?? 0;
        uint backwardKey = group.BackwardHotkey?.VirtualKey ?? 0;
        if (forwardKey != 0 && forwardKey == backwardKey)
        {
            message = "正向和反向循环不能使用同一个按键。";
            return false;
        }

        var usedKeys = new HashSet<uint>();
        for (int index = 0; index < _groups.Count; index++)
        {
            if (index == _editingGroupIndex)
            {
                continue;
            }

            AddConfiguredKey(usedKeys, _groups[index].ForwardHotkey);
            AddConfiguredKey(usedKeys, _groups[index].BackwardHotkey);
        }

        if (forwardKey != 0 && usedKeys.Contains(forwardKey))
        {
            message = $"按键 {KeyboardKeyCatalog.GetDisplayName(forwardKey)} 已被其他分组使用。";
            return false;
        }

        if (backwardKey != 0 && usedKeys.Contains(backwardKey))
        {
            message = $"按键 {KeyboardKeyCatalog.GetDisplayName(backwardKey)} 已被其他分组使用。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static void AddConfiguredKey(HashSet<uint> usedKeys, HotkeyBinding? binding)
    {
        if (binding?.VirtualKey > 0)
        {
            usedKeys.Add(binding.VirtualKey);
        }
    }

    private static void NormalizeSingleKey(HotkeyBinding? binding)
    {
        if (binding != null)
        {
            binding.Modifiers = 0;
        }
    }

    private static string FormatHotkeyDisplay(HotkeyBinding? binding)
    {
        if (binding == null || binding.VirtualKey == 0)
            return "未设置";

        return KeyboardKeyCatalog.GetDisplayName(binding.VirtualKey);
    }

    /// <summary>
    /// Builds a stable window key from a process item.
    /// Format: "processName::windowTitle"
    /// </summary>
    private static string MakeWindowKey(ProcessItem item)
    {
        return $"{item.ProcessName}::{item.WindowTitle}";
    }
}
