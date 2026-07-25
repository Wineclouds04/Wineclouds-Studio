using System.Drawing;
using System.Windows.Forms;

namespace WinecloudsStudio.Modules.WindowManager.Views;

internal sealed class ThumbnailOverlay : Form
{
    private static readonly Color TransparencyColor = Color.FromArgb(0, 0, 1);
    private readonly PictureBox _surface;
    private readonly Label _titleLabel;
    private string _title = string.Empty;
    private bool _isExcludedFromCycleGroup;

    public ThumbnailOverlay(Form owner,
        MouseEventHandler mouseDown,
        MouseEventHandler mouseUp,
        MouseEventHandler mouseMove,
        EventHandler mouseEnter,
        EventHandler mouseLeave)
    {
        Owner = owner;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = TransparencyColor;
        TransparencyKey = TransparencyColor;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ControlBox = false;
        StartPosition = FormStartPosition.Manual;

        _surface = new PictureBox
        {
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            TabStop = false
        };

        _titleLabel = new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            BackColor = Color.FromArgb(150, 20, 20, 20),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Location = new Point(7, 6),
            Padding = new Padding(4, 2, 4, 2),
            Size = new Size(160, 23),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand
        };

        Controls.Add(_titleLabel);
        Controls.Add(_surface);
        _titleLabel.BringToFront();

        WireMouseEvents(_surface, mouseDown, mouseUp, mouseMove, mouseEnter, mouseLeave);
        WireMouseEvents(_titleLabel, mouseDown, mouseUp, mouseMove, mouseEnter, mouseLeave);
        WireMouseEvents(this, mouseDown, mouseUp, mouseMove, mouseEnter, mouseLeave);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= (int)(
                WinecloudsStudio.Modules.WindowManager.Services.Interop.InteropConstants.WS_EX_TOOLWINDOW
                | WinecloudsStudio.Modules.WindowManager.Services.Interop.InteropConstants.WS_EX_NOACTIVATE);
            return parameters;
        }
    }

    public void SyncBounds(Rectangle screenBounds)
    {
        Bounds = screenBounds;
        _titleLabel.Width = Math.Max(40, screenBounds.Width - 14);
    }

    public void SetTitle(string title)
    {
        _title = title;
        UpdateTitle();
    }

    public void SetCycleGroupExcluded(bool excluded)
    {
        _isExcludedFromCycleGroup = excluded;
        UpdateTitle();
    }

    public void SetLabelVisible(bool visible) => _titleLabel.Visible = visible;

    private void UpdateTitle()
    {
        string displayTitle = _title.StartsWith(
            "EVE - ",
            StringComparison.OrdinalIgnoreCase)
            ? _title[6..]
            : _title;
        _titleLabel.Text =
            _isExcludedFromCycleGroup ? $"[已排除] {displayTitle}" : displayTitle;
        _titleLabel.ForeColor = _isExcludedFromCycleGroup ? Color.OrangeRed : Color.White;
    }

    private static void WireMouseEvents(Control control,
        MouseEventHandler mouseDown,
        MouseEventHandler mouseUp,
        MouseEventHandler mouseMove,
        EventHandler mouseEnter,
        EventHandler mouseLeave)
    {
        control.MouseDown += mouseDown;
        control.MouseUp += mouseUp;
        control.MouseMove += mouseMove;
        control.MouseEnter += mouseEnter;
        control.MouseLeave += mouseLeave;
    }
}
