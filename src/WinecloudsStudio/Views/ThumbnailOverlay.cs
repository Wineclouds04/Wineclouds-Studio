using System.Drawing;
using System.Windows.Forms;

namespace WinecloudsStudio.Views;

internal sealed class ThumbnailOverlay : Form
{
    private static readonly Color TransparencyColor = Color.FromArgb(0, 0, 1);
    private readonly PictureBox _surface;
    private readonly Label _titleLabel;

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
            AutoSize = true,
            BackColor = Color.FromArgb(150, 20, 20, 20),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Location = new Point(7, 6),
            Padding = new Padding(4, 2, 4, 2),
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
            parameters.ExStyle |= (int)Services.Interop.InteropConstants.WS_EX_TOOLWINDOW;
            return parameters;
        }
    }

    public void SetTitle(string title) => _titleLabel.Text = title;

    public void SetLabelVisible(bool visible) => _titleLabel.Visible = visible;

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
