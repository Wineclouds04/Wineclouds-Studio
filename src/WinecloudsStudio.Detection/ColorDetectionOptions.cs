namespace WinecloudsStudio.Detection;

public sealed record ColorDetectionOptions
{
    public byte TargetRed { get; init; } = 255;

    public byte TargetGreen { get; init; }

    public byte TargetBlue { get; init; }

    public double HueTolerance { get; init; } = 15;

    public double SaturationTolerance { get; init; } = 0.45;

    public double ValueTolerance { get; init; } = 0.70;

    public int MinimumTargetPixels { get; init; } = 20;

    public int MinimumConnectedArea { get; init; } = 15;

    public void Validate(long regionPixels)
    {
        if (HueTolerance is < 0 or > 180)
            throw new ArgumentOutOfRangeException(nameof(HueTolerance));
        if (SaturationTolerance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(SaturationTolerance));
        if (ValueTolerance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(ValueTolerance));
        if (MinimumTargetPixels < 1 || MinimumTargetPixels > regionPixels)
            throw new ArgumentOutOfRangeException(nameof(MinimumTargetPixels));
        if (MinimumConnectedArea < 1 || MinimumConnectedArea > regionPixels)
            throw new ArgumentOutOfRangeException(nameof(MinimumConnectedArea));
    }
}
