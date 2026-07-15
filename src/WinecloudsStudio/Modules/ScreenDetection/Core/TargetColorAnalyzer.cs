using System.Buffers;
using System.Diagnostics;

namespace WinecloudsStudio.Modules.ScreenDetection.Core;

public sealed class TargetColorAnalyzer
{
    public FrameAnalysis Analyze(
        ReadOnlySpan<byte> bgra,
        int width,
        int height,
        int stride,
        ColorDetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if ((long)stride < (long)width * 4) throw new ArgumentOutOfRangeException(nameof(stride));
        if ((long)bgra.Length < (long)stride * height)
            throw new ArgumentException("像素缓冲区长度不足。", nameof(bgra));

        int pixelCount = checked(width * height);
        options.Validate(pixelCount);
        long started = Stopwatch.GetTimestamp();
        byte[] maskArray = ArrayPool<byte>.Shared.Rent(pixelCount);
        Span<byte> mask = maskArray.AsSpan(0, pixelCount);
        mask.Clear();

        try
        {
            RgbToHsv(
                options.TargetRed,
                options.TargetGreen,
                options.TargetBlue,
                out double targetHue,
                out double targetSaturation,
                out double targetValue);
            int targetCount = BuildMask(
                bgra,
                width,
                height,
                stride,
                options,
                targetHue,
                targetSaturation,
                targetValue,
                mask);
            int largest = targetCount >= options.MinimumTargetPixels
                ? FindLargestConnectedArea(mask, width, height)
                : 0;
            bool matched = targetCount >= options.MinimumTargetPixels &&
                           largest >= options.MinimumConnectedArea;
            return new FrameAnalysis(
                matched,
                targetCount,
                largest,
                (double)targetCount / pixelCount,
                Stopwatch.GetElapsedTime(started));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(maskArray, clearArray: false);
        }
    }

    public static bool IsTargetColor(
        byte red,
        byte green,
        byte blue,
        ColorDetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        RgbToHsv(red, green, blue, out double hue, out double saturation, out double value);
        RgbToHsv(
            options.TargetRed,
            options.TargetGreen,
            options.TargetBlue,
            out double targetHue,
            out double targetSaturation,
            out double targetValue);

        return IsTargetColor(
            hue,
            saturation,
            value,
            targetHue,
            targetSaturation,
            targetValue,
            options);
    }

    private static bool IsTargetColor(
        double hue,
        double saturation,
        double value,
        double targetHue,
        double targetSaturation,
        double targetValue,
        ColorDetectionOptions options)
    {
        double directHueDistance = Math.Abs(hue - targetHue);
        double hueDistance = Math.Min(directHueDistance, 360 - directHueDistance);
        return hueDistance <= options.HueTolerance &&
               Math.Abs(saturation - targetSaturation) <= options.SaturationTolerance &&
               Math.Abs(value - targetValue) <= options.ValueTolerance;
    }

    public static void RgbToHsv(
        byte red,
        byte green,
        byte blue,
        out double hue,
        out double saturation,
        out double value)
    {
        double r = red / 255d;
        double g = green / 255d;
        double b = blue / 255d;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        value = max;
        saturation = max <= 0 ? 0 : delta / max;
        if (delta <= 0)
        {
            hue = 0;
            return;
        }

        if (max == r) hue = 60 * (((g - b) / delta) % 6);
        else if (max == g) hue = 60 * (((b - r) / delta) + 2);
        else hue = 60 * (((r - g) / delta) + 4);
        if (hue < 0) hue += 360;
    }

    private static int BuildMask(
        ReadOnlySpan<byte> bgra,
        int width,
        int height,
        int stride,
        ColorDetectionOptions options,
        double targetHue,
        double targetSaturation,
        double targetValue,
        Span<byte> mask)
    {
        int targetCount = 0;
        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            int maskRow = y * width;
            for (int x = 0; x < width; x++)
            {
                int offset = row + x * 4;
                RgbToHsv(
                    bgra[offset + 2],
                    bgra[offset + 1],
                    bgra[offset],
                    out double hue,
                    out double saturation,
                    out double value);
                if (!IsTargetColor(
                        hue,
                        saturation,
                        value,
                        targetHue,
                        targetSaturation,
                        targetValue,
                        options))
                    continue;

                mask[maskRow + x] = 1;
                targetCount++;
            }
        }

        return targetCount;
    }

    private static int FindLargestConnectedArea(Span<byte> mask, int width, int height)
    {
        int[] queueArray = ArrayPool<int>.Shared.Rent(mask.Length);
        try
        {
            Span<int> queue = queueArray.AsSpan(0, mask.Length);
            int largest = 0;
            for (int index = 0; index < mask.Length; index++)
            {
                if (mask[index] != 1) continue;

                int head = 0;
                int tail = 0;
                queue[tail++] = index;
                mask[index] = 2;

                while (head < tail)
                {
                    int current = queue[head++];
                    int x = current % width;
                    int y = current / width;
                    int minY = Math.Max(0, y - 1);
                    int maxY = Math.Min(height - 1, y + 1);
                    int minX = Math.Max(0, x - 1);
                    int maxX = Math.Min(width - 1, x + 1);

                    for (int neighborY = minY; neighborY <= maxY; neighborY++)
                    {
                        for (int neighborX = minX; neighborX <= maxX; neighborX++)
                        {
                            int neighbor = neighborY * width + neighborX;
                            if (mask[neighbor] != 1) continue;
                            mask[neighbor] = 2;
                            queue[tail++] = neighbor;
                        }
                    }
                }

                largest = Math.Max(largest, tail);
            }

            return largest;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queueArray, clearArray: false);
        }
    }
}
