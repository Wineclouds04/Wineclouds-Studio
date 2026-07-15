using System.Buffers;

namespace WinecloudsStudio.Modules.ScreenDetection.Services;

public sealed class CapturedFrame : IDisposable
{
    private byte[]? _buffer;

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public ReadOnlySpan<byte> Pixels => _buffer.AsSpan(0, Stride * Height);

    internal CapturedFrame(byte[] buffer, int width, int height, int stride)
    {
        _buffer = buffer;
        Width = width;
        Height = height;
        Stride = stride;
    }

    public void Dispose()
    {
        byte[]? buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null)
            ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
    }
}
