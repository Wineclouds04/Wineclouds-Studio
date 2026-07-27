using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WinecloudsStudio.Modules.Settings.Services;

internal sealed class TrayIconService : IDisposable
{
    private readonly Drawing.Icon _icon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private bool _disposed;

    public TrayIconService(string iconPath)
    {
        _icon = new Drawing.Icon(iconPath);

        var showMenuItem = new Forms.ToolStripMenuItem("显示主窗口");
        showMenuItem.Click += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);

        var exitMenuItem = new Forms.ToolStripMenuItem("退出");
        exitMenuItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu = new Forms.ContextMenuStrip();
        _contextMenu.Items.Add(showMenuItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(exitMenuItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = _icon,
            Text = "Wineclouds Studio",
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ShowRequested;

    public event EventHandler? ExitRequested;

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _notifyIcon.Visible = visible;
    }

    public void ShowMinimizedNotification()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _notifyIcon.ShowBalloonTip(
            2500,
            "Wineclouds Studio",
            "应用仍在后台运行，双击托盘图标可重新打开。",
            Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _icon.Dispose();
    }
}
