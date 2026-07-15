namespace WinecloudsStudio.Detection;

public readonly record struct ScreenRegion(int X, int Y, int Width, int Height)
{
    public long PixelCount => (long)Width * Height;

    public bool IsValid => Width >= 2 && Height >= 2;

    public bool Contains(int x, int y) =>
        x >= X && y >= Y && (long)x < (long)X + Width && (long)y < (long)Y + Height;

    public bool IsInside(ScreenRegion bounds) =>
        IsValid && X >= bounds.X && Y >= bounds.Y &&
        (long)X + Width <= (long)bounds.X + bounds.Width &&
        (long)Y + Height <= (long)bounds.Y + bounds.Height;
}
