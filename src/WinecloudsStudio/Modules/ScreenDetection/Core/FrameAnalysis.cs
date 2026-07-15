namespace WinecloudsStudio.Modules.ScreenDetection.Core;

public readonly record struct FrameAnalysis(
    bool IsMatched,
    int TargetPixelCount,
    int LargestConnectedArea,
    double TargetPixelRatio,
    TimeSpan Elapsed);
