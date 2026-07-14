using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using WinecloudsStudio.Services.Interface;
using WinecloudsStudio.Services.Interop;

namespace WinecloudsStudio.Views;

public abstract class ThumbnailView : Form, IThumbnailView
{
    private const int BorderWidth = 3;
    private const int GridSize = 8;
    private static readonly Color HighlightColor = Color.Lime;

    private readonly ThumbnailOverlay _overlay;
    private bool _customMouseMode;
    private bool _highlighted;
    private bool _showBorder;
    private bool _positionLocked;
    private bool _snapToGrid;
    private Point _baseMousePosition;
    private Point _baseWindowLocation;
    private Control? _captureControl;

    protected ThumbnailView(
        IWindowManager windowManager,
        IntPtr id,
        string processName,
        string title,
        Size size,
        Point location)
    {
        WindowManager = windowManager;
        Id = id;
        ProcessName = processName;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = location;
        ClientSize = size;
        Cursor = Cursors.Hand;

        _overlay = new ThumbnailOverlay(
            this,
            MouseDownHandler,
            MouseUpHandler,
            MouseMoveHandler,
            MouseEnterHandler,
            MouseLeaveHandler);

        Title = title;

        MouseDown += MouseDownHandler;
        MouseUp += MouseUpHandler;
        MouseMove += MouseMoveHandler;
        MouseEnter += MouseEnterHandler;
        MouseLeave += MouseLeaveHandler;
        Move += MoveHandler;
        Resize += ResizeHandler;
        FormClosed += FormClosedHandler;
    }

    protected IWindowManager WindowManager { get; }

    public IntPtr Id { get; }

    public string ProcessName { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => Text;
        set
        {
            Text = value;
            _overlay.SetTitle(value);
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Point ThumbnailLocation
    {
        get => Location;
        set => Location = value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Size ThumbnailSize
    {
        get => ClientSize;
        set => ClientSize = value;
    }

    public bool IsActive { get; private set; }

    public event Action<IntPtr>? ThumbnailActivated;

    public event Action<IntPtr>? ThumbnailMoved;

    public bool IsKnownHandle(IntPtr handle) =>
        handle == Id || handle == Handle || handle == _overlay.Handle;

    public void ShowThumbnail()
    {
        if (!Visible)
        {
            Show();
        }

        SyncOverlay();
        if (!_overlay.Visible)
        {
            _overlay.Show(this);
        }

        IsActive = true;
        RefreshThumbnail(true);
    }

    public void RefreshThumbnail(bool forceRefresh)
    {
        RefreshLiveThumbnail(forceRefresh);
        ResizeLiveThumbnail(GetThumbnailBounds());
        SyncOverlay();
    }

    public void SetOpacity(double opacity)
    {
        double normalized = Math.Clamp(opacity, 0.1, 1.0);
        if (normalized >= 0.9)
        {
            normalized = 1.0;
        }

        Opacity = normalized;
        _overlay.Opacity = normalized > 0.8 ? 1.0 : 1.0 - (1.0 - normalized) / 2.0;
    }

    public void SetTopMost(bool topMost)
    {
        TopMost = topMost;
        _overlay.TopMost = topMost;
    }

    public void SetPositionLocked(bool locked) => _positionLocked = locked;

    public void SetSnapToGrid(bool snapToGrid)
    {
        if (_snapToGrid == snapToGrid)
        {
            return;
        }

        _snapToGrid = snapToGrid;
        if (_snapToGrid)
        {
            Location = SnapToGrid(Location);
        }
    }

    public void SetHighlight(bool highlighted)
    {
        if (_highlighted == highlighted)
        {
            return;
        }

        _highlighted = highlighted;
        ApplyBorder();
    }

    public void SetShowBorder(bool showBorder)
    {
        if (_showBorder == showBorder)
        {
            return;
        }

        _showBorder = showBorder;
        ApplyBorder();
    }

    public void SetOverlayLabel(bool showLabel) => _overlay.SetLabelVisible(showLabel);

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= (int)InteropConstants.WS_EX_TOOLWINDOW;
            return parameters;
        }
    }

    protected abstract void RefreshLiveThumbnail(bool forceRefresh);

    protected abstract void ResizeLiveThumbnail(Rectangle destination);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThumbnailActivated = null;
            ThumbnailMoved = null;
            _overlay.Dispose();
        }

        base.Dispose(disposing);
    }

    private Rectangle GetThumbnailBounds()
    {
        int inset = _showBorder || _highlighted ? BorderWidth : 0;
        return new Rectangle(
            inset,
            inset,
            Math.Max(1, ClientSize.Width - inset * 2),
            Math.Max(1, ClientSize.Height - inset * 2));
    }

    private void ApplyBorder()
    {
        BackColor = _showBorder || _highlighted ? HighlightColor : Color.Black;
        ResizeLiveThumbnail(GetThumbnailBounds());
        Invalidate();
    }

    private void SyncOverlay()
    {
        if (IsDisposed || _overlay.IsDisposed)
        {
            return;
        }

        _overlay.Bounds = new Rectangle(PointToScreen(Point.Empty), ClientSize);
    }

    private void MouseEnterHandler(object? sender, EventArgs e)
    {
        ExitCustomMouseMode();
        _baseWindowLocation = Location;
    }

    private void MouseLeaveHandler(object? sender, EventArgs e)
    {
    }

    private void MouseDownHandler(object? sender, MouseEventArgs e)
    {
        switch (e.Button)
        {
            case MouseButtons.Left:
                ThumbnailActivated?.Invoke(Id);
                break;
            case MouseButtons.Right:
                if (!_positionLocked)
                {
                    EnterCustomMouseMode(sender as Control);
                }
                break;
        }
    }

    private void MouseMoveHandler(object? sender, MouseEventArgs e)
    {
        if (!_customMouseMode || !e.Button.HasFlag(MouseButtons.Right))
        {
            return;
        }

        Point mousePosition = Control.MousePosition;
        int offsetX = mousePosition.X - _baseMousePosition.X;
        int offsetY = mousePosition.Y - _baseMousePosition.Y;
        _baseMousePosition = mousePosition;

        Location = _snapToGrid
            ? SnapToGrid(new Point(Location.X + offsetX, Location.Y + offsetY))
            : new Point(Location.X + offsetX, Location.Y + offsetY);
        _baseWindowLocation = Location;
    }

    private void MouseUpHandler(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ExitCustomMouseMode();
        }
    }

    private void EnterCustomMouseMode(Control? source)
    {
        _customMouseMode = true;
        _baseMousePosition = Control.MousePosition;
        _baseWindowLocation = Location;
        _captureControl = source ?? this;
        _captureControl.Capture = true;
    }

    private void ExitCustomMouseMode()
    {
        _customMouseMode = false;
        if (_captureControl != null && _captureControl.Capture)
        {
            _captureControl.Capture = false;
        }
        _captureControl = null;
    }

    private void MoveHandler(object? sender, EventArgs e)
    {
        SyncOverlay();
        if (_customMouseMode && Location != _baseWindowLocation)
        {
            _baseWindowLocation = Location;
        }
        ThumbnailMoved?.Invoke(Id);
    }

    private void ResizeHandler(object? sender, EventArgs e)
    {
        ResizeLiveThumbnail(GetThumbnailBounds());
        SyncOverlay();
    }

    private void FormClosedHandler(object? sender, FormClosedEventArgs e)
    {
        IsActive = false;
        if (!_overlay.IsDisposed)
        {
            _overlay.Close();
        }
    }

    private static Point SnapToGrid(Point location) => new(
        (int)Math.Round(location.X / (double)GridSize) * GridSize,
        (int)Math.Round(location.Y / (double)GridSize) * GridSize);
}
