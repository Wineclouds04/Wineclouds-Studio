using WinecloudsStudio.Detection;

namespace WinecloudsStudio.ScreenDetection;

public sealed record ScreenDetectionSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public ScreenRegion ScreenRegion { get; init; } = new(100, 100, 200, 100);

    public ColorDetectionOptions ColorDetectionOptions { get; init; } = new();

    public int ScanIntervalMs { get; init; } = 50;

    public int PresentConfirmationFrames { get; init; } = 3;

    public int AbsentConfirmationFrames { get; init; } = 3;

    public string AudioFilePath { get; init; } = string.Empty;
}
