using System.ComponentModel;

namespace WinecloudsStudio.Modules.WindowManager.Models;

/// <summary>
/// Lightweight display model for a window group in the ListView.
/// </summary>
public sealed class GroupItem : INotifyPropertyChanged
{
    private string _groupName = string.Empty;
    private string _detailsText = string.Empty;
    private string _forwardHotkeyText = string.Empty;
    private string _backwardHotkeyText = string.Empty;

    public string GroupName
    {
        get => _groupName;
        set { if (_groupName != value) { _groupName = value; OnPropertyChanged(); } }
    }

    public string DetailsText
    {
        get => _detailsText;
        set { if (_detailsText != value) { _detailsText = value; OnPropertyChanged(); } }
    }

    public string ForwardHotkeyText
    {
        get => _forwardHotkeyText;
        set { if (_forwardHotkeyText != value) { _forwardHotkeyText = value; OnPropertyChanged(); } }
    }

    public string BackwardHotkeyText
    {
        get => _backwardHotkeyText;
        set { if (_backwardHotkeyText != value) { _backwardHotkeyText = value; OnPropertyChanged(); } }
    }

    /// <summary>Index in the backing _groups list, used for edit/delete.</summary>
    public int GroupIndex { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
