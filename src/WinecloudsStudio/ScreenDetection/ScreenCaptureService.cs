using System.Buffers;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WinecloudsStudio.Detection;

namespace WinecloudsStudio.ScreenDetection;

public sealed class ScreenCaptureService : IDisposable
{
    private readonly object _gate = new();
    private Bitmap? _bitmap;
    private Graphics? _graphics;

    public CapturedFrame Capture(ScreenRegion region)
    {
        if (!region.IsValid)
            throw new ArgumentException("检测区域无效。", nameof(region));
        if (!region.IsInside(VirtualScreenService.GetBounds()))
            throw new ArgumentOutOfRangeException(nameof(region), "检测区域超出当前虚拟桌面。");

        lock (_gate)
        {
            EnsureSurface(region.Width, region.Height);
            _graphics!.CopyFromScreen(
                region.X,
                region.Y,
                0,
                0,
                new Size(region.Width, region.Height),
                CopyPixelOperation.SourceCopy);

            Rectangle rectangle = new(0, 0, region.Width, region.Height);
            BitmapData data = _bitmap!.LockBits(
                rectangle,
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            int stride = Math.Abs(data.Stride);
            byte[] rented = ArrayPool<byte>.Shared.Rent(stride * region.Height);

            try
            {
                if (data.Stride > 0)
                {
                    Marshal.Copy(data.Scan0, rented, 0, stride * region.Height);
                }
                else
                {
                    for (int y = 0; y < region.Height; y++)
                    {
                        IntPtr source = IntPtr.Add(data.Scan0, data.Stride * y);
                        Marshal.Copy(source, rented, (region.Height - 1 - y) * stride, stride);
                    }
                }

                return new CapturedFrame(rented, region.Width, region.Height, stride);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: false);
                throw;
            }
            finally
            {
                _bitmap.UnlockBits(data);
            }
        }
    }

    private void EnsureSurface(int width, int height)
    {
        if (_bitmap?.Width == width && _bitmap.Height == height)
            return;

        _graphics?.Dispose();
        _bitmap?.Dispose();
        _bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        _graphics = Graphics.FromImage(_bitmap);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _graphics?.Dispose();
            _bitmap?.Dispose();
            _graphics = null;
            _bitmap = null;
        }
    }
}
