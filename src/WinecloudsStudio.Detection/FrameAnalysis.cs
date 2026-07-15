namespace WinecloudsStudio.Detection;

public readonly record struct FrameAnalysis(
    bool IsMatched,
    int TargetPixelCount,
    int LargestConnectedArea,
    double TargetPixelRatio,
    TimeSpan Elapsed);
