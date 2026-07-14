using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinecloudsStudio.Pages;

/// <summary>
/// View-model item for the process selection list.
/// </summary>
public sealed class ProcessItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string DisplayText { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
