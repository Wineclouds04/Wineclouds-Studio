using WinecloudsStudio.Modules.ScreenDetection.Core;

var tests = new (string Name, Action Run)[]
{
    ("默认目标与容差正确", DefaultOptionsAreRed),
    ("纯目标色被识别", PureTargetColorIsDetected),
    ("色相距离跨越零度", HueDistanceWrapsAroundZero),
    ("色相容差边界包含", HueToleranceBoundaryIsInclusive),
    ("饱和度容差边界包含", SaturationToleranceBoundaryIsInclusive),
    ("亮度容差边界包含", ValueToleranceBoundaryIsInclusive),
    ("非目标颜色被排除", NonTargetColorIsRejected),
    ("最少像素数十九与二十边界", MinimumPixelBoundaryWorks),
    ("离散噪点不构成连通块", IsolatedNoiseIsRejected),
    ("八邻域连接对角像素", EightNeighborhoodConnectsDiagonals),
    ("负坐标屏幕区域边界有效", NegativeScreenCoordinatesWork),
    ("连续三帧只触发一次", StateMachineAlertsOnce),
    ("稳定消失后重新布防", StateMachineRearms)
};

int failures = 0;
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL  {name}: {ex.Message}");
    }
}

Console.WriteLine($"\n{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void DefaultOptionsAreRed()
{
    ColorDetectionOptions options = new();
    Assert(options.TargetRed == 255 && options.TargetGreen == 0 && options.TargetBlue == 0,
        "默认目标色应为纯红");
    Assert(options.HueTolerance == 15 && options.SaturationTolerance == 0.45 && options.ValueTolerance == 0.70,
        "默认 HSV 容差不正确");
}

static void PureTargetColorIsDetected()
{
    ColorDetectionOptions options = MinimumOptions(20, 15) with
    {
        TargetRed = 30,
        TargetGreen = 120,
        TargetBlue = 220
    };
    FrameAnalysis result = AnalyzeSolid(5, 5, 30, 120, 220, options);
    Assert(result.IsMatched && result.TargetPixelCount == 25 && result.LargestConnectedArea == 25,
        "纯目标色分析结果不正确");
}

static void HueDistanceWrapsAroundZero()
{
    ColorDetectionOptions options = MinimumOptions(1, 1) with
    {
        TargetRed = 255,
        TargetGreen = 0,
        TargetBlue = 0,
        HueTolerance = 15,
        SaturationTolerance = 0,
        ValueTolerance = 0
    };
    Assert(TargetColorAnalyzer.IsTargetColor(255, 0, 43, options),
        "约 350° 的颜色应通过环形距离命中 0° 目标色");
}

static void HueToleranceBoundaryIsInclusive()
{
    ColorDetectionOptions atBoundary = MinimumOptions(1, 1) with
    {
        HueTolerance = 60,
        SaturationTolerance = 0,
        ValueTolerance = 0
    };
    Assert(TargetColorAnalyzer.IsTargetColor(255, 255, 0, atBoundary), "恰好位于色相容差边界应命中");
    Assert(!TargetColorAnalyzer.IsTargetColor(255, 255, 0, atBoundary with { HueTolerance = 59.999 }),
        "越过色相容差边界应排除");
}

static void SaturationToleranceBoundaryIsInclusive()
{
    double boundary = 127d / 255d;
    ColorDetectionOptions atBoundary = MinimumOptions(1, 1) with
    {
        HueTolerance = 0,
        SaturationTolerance = boundary,
        ValueTolerance = 0
    };
    Assert(TargetColorAnalyzer.IsTargetColor(255, 127, 127, atBoundary),
        "恰好位于饱和度容差边界应命中");
    Assert(!TargetColorAnalyzer.IsTargetColor(255, 127, 127,
            atBoundary with { SaturationTolerance = boundary - 0.000001 }),
        "越过饱和度容差边界应排除");
}

static void ValueToleranceBoundaryIsInclusive()
{
    double boundary = 127d / 255d;
    ColorDetectionOptions atBoundary = MinimumOptions(1, 1) with
    {
        HueTolerance = 0,
        SaturationTolerance = 0,
        ValueTolerance = boundary
    };
    Assert(TargetColorAnalyzer.IsTargetColor(128, 0, 0, atBoundary),
        "恰好位于亮度容差边界应命中");
    Assert(!TargetColorAnalyzer.IsTargetColor(128, 0, 0,
            atBoundary with { ValueTolerance = boundary - 0.000001 }),
        "越过亮度容差边界应排除");
}

static void NonTargetColorIsRejected()
{
    ColorDetectionOptions options = MinimumOptions(1, 1) with
    {
        TargetRed = 0,
        TargetGreen = 0,
        TargetBlue = 255,
        HueTolerance = 10,
        SaturationTolerance = 0.1,
        ValueTolerance = 0.1
    };
    Assert(!TargetColorAnalyzer.IsTargetColor(0, 255, 0, options), "绿色不应命中蓝色目标");
}

static void MinimumPixelBoundaryWorks()
{
    byte[] frame = CreateFrame(5, 5, (0, 0, 0));
    for (int i = 0; i < 19; i++) SetPixel(frame, 5, i % 5, i / 5, 255, 0, 0);
    ColorDetectionOptions options = MinimumOptions(20, 15);
    FrameAnalysis below = new TargetColorAnalyzer().Analyze(frame, 5, 5, 20, options);
    Assert(!below.IsMatched && below.TargetPixelCount == 19, "19 像素不应命中");

    SetPixel(frame, 5, 4, 3, 255, 0, 0);
    FrameAnalysis boundary = new TargetColorAnalyzer().Analyze(frame, 5, 5, 20, options);
    Assert(boundary.IsMatched && boundary.TargetPixelCount == 20, "20 像素应命中");
}

static void IsolatedNoiseIsRejected()
{
    byte[] frame = CreateFrame(9, 9, (0, 0, 0));
    int count = 0;
    for (int y = 0; y < 9 && count < 20; y += 2)
        for (int x = 0; x < 9 && count < 20; x += 2, count++)
            SetPixel(frame, 9, x, y, 255, 0, 0);

    FrameAnalysis result = new TargetColorAnalyzer().Analyze(frame, 9, 9, 36, MinimumOptions(20, 15));
    Assert(!result.IsMatched && result.TargetPixelCount == 20 && result.LargestConnectedArea == 1,
        "离散点被错误连接");
}

static void EightNeighborhoodConnectsDiagonals()
{
    byte[] frame = CreateFrame(20, 20, (0, 0, 0));
    for (int i = 0; i < 15; i++) SetPixel(frame, 20, i, i, 255, 0, 0);
    for (int i = 0; i < 5; i++) SetPixel(frame, 20, 19, i * 3, 255, 0, 0);

    FrameAnalysis result = new TargetColorAnalyzer().Analyze(frame, 20, 20, 80, MinimumOptions(20, 15));
    Assert(result.IsMatched && result.LargestConnectedArea == 15, "对角连通面积错误");
}

static void NegativeScreenCoordinatesWork()
{
    ScreenRegion bounds = new(-1920, -200, 3840, 1280);
    Assert(new ScreenRegion(-1800, -100, 100, 100).IsInside(bounds), "负坐标合法区域被拒绝");
    Assert(!new ScreenRegion(1900, 10, 100, 100).IsInside(bounds), "越界区域被接受");
    Assert(bounds.Contains(-1920, -200) && !bounds.Contains(1920, 1080), "区域包含边界判断错误");
}

static void StateMachineAlertsOnce()
{
    DetectionStateMachine machine = new(3, 3);
    bool[] inputs = [false, true, true, true, true];
    bool[] alerts = inputs.Select(machine.Push).ToArray();
    Assert(alerts.Count(x => x) == 1 && alerts[3] && machine.State == DetectionState.Present,
        "持续出现应只触发一次");
}

static void StateMachineRearms()
{
    DetectionStateMachine machine = new(3, 3);
    bool[] sequence = [true, true, true, false, false, false, true, true, true];
    Assert(sequence.Count(machine.Push) == 2 && machine.State == DetectionState.Present,
        "稳定消失后未重新布防");
}

static ColorDetectionOptions MinimumOptions(int pixels, int area) => new()
{
    MinimumTargetPixels = pixels,
    MinimumConnectedArea = area
};

static FrameAnalysis AnalyzeSolid(
    int width,
    int height,
    byte red,
    byte green,
    byte blue,
    ColorDetectionOptions options)
{
    byte[] frame = CreateFrame(width, height, (red, green, blue));
    return new TargetColorAnalyzer().Analyze(frame, width, height, width * 4, options);
}

static byte[] CreateFrame(int width, int height, (byte R, byte G, byte B) color)
{
    byte[] frame = new byte[width * height * 4];
    for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            SetPixel(frame, width, x, y, color.R, color.G, color.B);
    return frame;
}

static void SetPixel(byte[] frame, int width, int x, int y, byte red, byte green, byte blue)
{
    int offset = (y * width + x) * 4;
    frame[offset] = blue;
    frame[offset + 1] = green;
    frame[offset + 2] = red;
    frame[offset + 3] = 255;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
