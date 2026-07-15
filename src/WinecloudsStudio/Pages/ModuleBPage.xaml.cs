using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinecloudsStudio.Detection;
using WinecloudsStudio.ScreenDetection;
using WinecloudsStudio.Services.Implementation;
using Windows.UI;
using Forms = System.Windows.Forms;

namespace WinecloudsStudio.Pages;

public sealed partial class ModuleBPage : Page
{
    private readonly ScreenCaptureService _capture = new();
    private readonly TargetColorAnalyzer _analyzer = new();
    private readonly AudioLoopPlayer _audio = new();
    private readonly ScreenDetectionSettingsService _settingsService = new();
    private ScreenDetectionSettings _settings;
    private CancellationTokenSource? _previewCancellation;
    private Task? _previewTask;
    private CancellationTokenSource? _detectionCancellation;
    private Task? _detectionTask;
    private bool _disposed;

    public ModuleBPage()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        PopulateControls(_settings);
        _audio.PlaybackFailed += Audio_PlaybackFailed;

        if (File.Exists(_settings.AudioFilePath))
        {
            try
            {
                _audio.SetFile(_settings.AudioFilePath);
            }
            catch (Exception exception)
            {
                ErrorText.Text = "声音文件加载失败：" + exception.Message;
            }
        }
    }

    private void PopulateControls(ScreenDetectionSettings settings)
    {
        XBox.Text = settings.ScreenRegion.X.ToString(CultureInfo.InvariantCulture);
        YBox.Text = settings.ScreenRegion.Y.ToString(CultureInfo.InvariantCulture);
        WidthBox.Text = settings.ScreenRegion.Width.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = settings.ScreenRegion.Height.ToString(CultureInfo.InvariantCulture);
        HueToleranceBox.Text = settings.ColorDetectionOptions.HueTolerance.ToString(CultureInfo.InvariantCulture);
        SaturationToleranceBox.Text = settings.ColorDetectionOptions.SaturationTolerance.ToString(CultureInfo.InvariantCulture);
        ValueToleranceBox.Text = settings.ColorDetectionOptions.ValueTolerance.ToString(CultureInfo.InvariantCulture);
        MinimumPixelsBox.Text = settings.ColorDetectionOptions.MinimumTargetPixels.ToString(CultureInfo.InvariantCulture);
        MinimumAreaBox.Text = settings.ColorDetectionOptions.MinimumConnectedArea.ToString(CultureInfo.InvariantCulture);
        ScanIntervalBox.Text = settings.ScanIntervalMs.ToString(CultureInfo.InvariantCulture);
        PresentFramesBox.Text = settings.PresentConfirmationFrames.ToString(CultureInfo.InvariantCulture);
        AbsentFramesBox.Text = settings.AbsentConfirmationFrames.ToString(CultureInfo.InvariantCulture);
        TargetColorPicker.Color = Color.FromArgb(
            255,
            settings.ColorDetectionOptions.TargetRed,
            settings.ColorDetectionOptions.TargetGreen,
            settings.ColorDetectionOptions.TargetBlue);
        UpdateTargetColorDisplay(TargetColorPicker.Color);
        AudioFileText.Text = string.IsNullOrWhiteSpace(settings.AudioFilePath)
            ? "未选择 MP3"
            : Path.GetFileName(settings.AudioFilePath);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_disposed)
            StartPreview(GetRegionFromControlsOrSettings());
    }

    private async void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopPreviewAsync();
        await StopDetectionAsync(updateUi: false);
        _audio.PlaybackFailed -= Audio_PlaybackFailed;
        _audio.Dispose();
        _capture.Dispose();
    }

    private async void SelectRegionButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        await StopPreviewAsync();
        Window? mainWindow = (Application.Current as App)?.MainAppWindow;
        mainWindow?.AppWindow.Hide();
        try
        {
            RegionSelectionWindow selector = new();
            ScreenRegion? selected = await selector.ShowAsync();
            if (selected is { } region)
            {
                XBox.Text = region.X.ToString(CultureInfo.InvariantCulture);
                YBox.Text = region.Y.ToString(CultureInfo.InvariantCulture);
                WidthBox.Text = region.Width.ToString(CultureInfo.InvariantCulture);
                HeightBox.Text = region.Height.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (Exception exception)
        {
            ErrorText.Text = "区域框选失败：" + exception.Message;
        }
        finally
        {
            mainWindow?.AppWindow.Show(true);
            mainWindow?.Activate();
            StartPreview(GetRegionFromControlsOrSettings());
        }
    }

    private void TargetColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args) =>
        UpdateTargetColorDisplay(args.NewColor);

    private void UpdateTargetColorDisplay(Color color)
    {
        TargetColorSwatch.Background = new SolidColorBrush(color);
        TargetColorText.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void SelectAudioButton_Click(object sender, RoutedEventArgs e)
    {
        using Forms.OpenFileDialog dialog = new()
        {
            Title = "选择循环播放的声音",
            Filter = "MP3 音频文件 (*.mp3)|*.mp3",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
            return;

        try
        {
            _audio.SetFile(dialog.FileName);
            _settings = _settings with { AudioFilePath = dialog.FileName };
            AudioFileText.Text = Path.GetFileName(dialog.FileName);
            ErrorText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            ErrorText.Text = "声音文件加载失败：" + exception.Message;
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ScreenDetectionSettings settings = ReadAndValidateSettings();
            _settingsService.Save(settings);
            _settings = settings;
            _audio.SetFile(settings.AudioFilePath);
            await StopPreviewAsync();

            _detectionCancellation = new CancellationTokenSource();
            SetRunning(true);
            StateText.Text = "正在确认…";
            StateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 217, 119, 6));
            ErrorText.Text = string.Empty;
            Logger.Info("ScreenDetection", "Detection started");
            _detectionTask = Task.Run(() => DetectionLoopAsync(settings, _detectionCancellation.Token));
        }
        catch (Exception exception)
        {
            ErrorText.Text = exception.Message;
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        await StopDetectionAsync(updateUi: true);
        StartPreview(_settings.ScreenRegion);
    }

    private async Task DetectionLoopAsync(ScreenDetectionSettings settings, CancellationToken token)
    {
        DetectionStateMachine stateMachine = new(
            settings.PresentConfirmationFrames,
            settings.AbsentConfirmationFrames);
        long lastUiUpdate = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                long started = Stopwatch.GetTimestamp();
                FrameAnalysis analysis;
                byte[]? previewPixels = null;
                int previewWidth = 0;
                int previewHeight = 0;
                bool updateUi = lastUiUpdate == 0 ||
                    Stopwatch.GetElapsedTime(lastUiUpdate) >= TimeSpan.FromMilliseconds(100);

                using (CapturedFrame frame = _capture.Capture(settings.ScreenRegion))
                {
                    analysis = _analyzer.Analyze(
                        frame.Pixels,
                        frame.Width,
                        frame.Height,
                        frame.Stride,
                        settings.ColorDetectionOptions);
                    if (updateUi)
                    {
                        previewPixels = PackFrame(frame);
                        previewWidth = frame.Width;
                        previewHeight = frame.Height;
                    }
                }

                DetectionState previousState = stateMachine.State;
                bool enteredPresent = stateMachine.Push(analysis.IsMatched);
                if (enteredPresent)
                {
                    await RunOnUiThreadAsync(_audio.StartLoop);
                    Logger.Info("ScreenDetection", "Target color entered present state");
                }
                else if (previousState == DetectionState.Present && stateMachine.State == DetectionState.Absent)
                {
                    await RunOnUiThreadAsync(_audio.Stop);
                    Logger.Info("ScreenDetection", "Target color entered absent state");
                }

                if (updateUi && previewPixels is not null)
                {
                    lastUiUpdate = Stopwatch.GetTimestamp();
                    byte[] pixels = previewPixels;
                    int width = previewWidth;
                    int height = previewHeight;
                    await RunOnUiThreadAsync(() =>
                    {
                        UpdateStatus(stateMachine.State, analysis);
                        UpdatePreview(pixels, width, height, settings.ScreenRegion);
                    });
                }

                TimeSpan remaining = TimeSpan.FromMilliseconds(settings.ScanIntervalMs) -
                                     Stopwatch.GetElapsedTime(started);
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.Error("ScreenDetection", exception.ToString());
            await RunOnUiThreadAsync(() =>
            {
                SafeStopAudio();
                SetRunning(false);
                StateText.Text = "检测错误";
                StateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                ErrorText.Text = "检测已停止：" + exception.Message;
            });
        }
    }

    private void StartPreview(ScreenRegion region)
    {
        if (_disposed || _detectionTask is { IsCompleted: false })
            return;

        if (!region.IsValid || !region.IsInside(VirtualScreenService.GetBounds()))
        {
            PreviewImage.Source = null;
            PreviewPlaceholder.Visibility = Visibility.Visible;
            PreviewInfoText.Text = "区域无效";
            return;
        }

        _previewCancellation?.Cancel();
        _previewCancellation = new CancellationTokenSource();
        _previewTask = Task.Run(() => PreviewLoopAsync(region, _previewCancellation.Token));
    }

    private async Task PreviewLoopAsync(ScreenRegion region, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                byte[] pixels;
                int width;
                int height;
                using (CapturedFrame frame = _capture.Capture(region))
                {
                    pixels = PackFrame(frame);
                    width = frame.Width;
                    height = frame.Height;
                }

                await RunOnUiThreadAsync(() => UpdatePreview(pixels, width, height, region));
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() =>
            {
                PreviewImage.Source = null;
                PreviewPlaceholder.Visibility = Visibility.Visible;
                PreviewPlaceholder.Text = "预览不可用";
                PreviewInfoText.Text = exception.Message;
            });
        }
    }

    private static byte[] PackFrame(CapturedFrame frame)
    {
        int rowLength = checked(frame.Width * 4);
        byte[] packed = new byte[checked(rowLength * frame.Height)];
        for (int y = 0; y < frame.Height; y++)
        {
            frame.Pixels.Slice(y * frame.Stride, rowLength)
                .CopyTo(packed.AsSpan(y * rowLength, rowLength));
        }

        return packed;
    }

    private void UpdatePreview(byte[] pixels, int width, int height, ScreenRegion region)
    {
        WriteableBitmap bitmap = new(width, height);
        using (Stream stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(pixels, 0, pixels.Length);
        }
        bitmap.Invalidate();
        PreviewImage.Source = bitmap;
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        PreviewInfoText.Text = $"X {region.X}, Y {region.Y}｜{region.Width} × {region.Height}｜实时预览";
    }

    private void UpdateStatus(DetectionState state, FrameAnalysis analysis)
    {
        StateText.Text = state switch
        {
            DetectionState.Present => "检测到目标色块",
            DetectionState.Absent => "未检测到目标色块",
            _ => "正在确认…"
        };
        StateText.Foreground = new SolidColorBrush(state switch
        {
            DetectionState.Present => Color.FromArgb(255, 220, 38, 38),
            DetectionState.Absent => Color.FromArgb(255, 5, 150, 105),
            _ => Color.FromArgb(255, 217, 119, 6)
        });
        MetricsText.Text =
            $"目标色像素 {analysis.TargetPixelCount:N0}｜最大连通面积 {analysis.LargestConnectedArea:N0}｜分析 {analysis.Elapsed.TotalMilliseconds:F1} ms";
    }

    private ScreenDetectionSettings ReadAndValidateSettings()
    {
        int ParseInt(TextBox box, string name) =>
            int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : throw new ArgumentException($"{name}必须是整数。");
        double ParseDouble(TextBox box, string name) =>
            double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : throw new ArgumentException($"{name}必须是数字。");

        ScreenRegion region = new(
            ParseInt(XBox, "X"),
            ParseInt(YBox, "Y"),
            ParseInt(WidthBox, "宽度"),
            ParseInt(HeightBox, "高度"));
        if (!region.IsValid)
            throw new ArgumentException("检测区域宽和高必须至少为 2 像素。");
        ScreenRegion virtualScreen = VirtualScreenService.GetBounds();
        if (!region.IsInside(virtualScreen))
            throw new ArgumentException(
                $"检测区域超出虚拟桌面：X={virtualScreen.X}, Y={virtualScreen.Y}, 宽={virtualScreen.Width}, 高={virtualScreen.Height}。");

        Color color = TargetColorPicker.Color;
        ColorDetectionOptions colorOptions = new()
        {
            TargetRed = color.R,
            TargetGreen = color.G,
            TargetBlue = color.B,
            HueTolerance = ParseDouble(HueToleranceBox, "色相容差"),
            SaturationTolerance = ParseDouble(SaturationToleranceBox, "饱和度容差"),
            ValueTolerance = ParseDouble(ValueToleranceBox, "亮度容差"),
            MinimumTargetPixels = ParseInt(MinimumPixelsBox, "最少目标色像素"),
            MinimumConnectedArea = ParseInt(MinimumAreaBox, "最小连通面积")
        };
        colorOptions.Validate(region.PixelCount);

        int interval = ParseInt(ScanIntervalBox, "扫描间隔");
        int presentFrames = ParseInt(PresentFramesBox, "出现确认帧数");
        int absentFrames = ParseInt(AbsentFramesBox, "消失确认帧数");
        if (interval is < 20 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(interval), "扫描间隔必须为 20～5000 ms。");
        if (presentFrames is < 1 or > 100 || absentFrames is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(presentFrames), "确认帧数必须为 1～100。");
        if (string.IsNullOrWhiteSpace(_settings.AudioFilePath) || !File.Exists(_settings.AudioFilePath))
            throw new ArgumentException("请先选择有效的 MP3 声音文件。");
        if (!string.Equals(Path.GetExtension(_settings.AudioFilePath), ".mp3", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("声音文件必须是 MP3。");

        return new ScreenDetectionSettings
        {
            ScreenRegion = region,
            ColorDetectionOptions = colorOptions,
            ScanIntervalMs = interval,
            PresentConfirmationFrames = presentFrames,
            AbsentConfirmationFrames = absentFrames,
            AudioFilePath = _settings.AudioFilePath
        };
    }

    private ScreenRegion GetRegionFromControlsOrSettings()
    {
        return int.TryParse(XBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
               int.TryParse(YBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) &&
               int.TryParse(WidthBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) &&
               int.TryParse(HeightBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height)
            ? new ScreenRegion(x, y, width, height)
            : _settings.ScreenRegion;
    }

    private async Task StopPreviewAsync()
    {
        CancellationTokenSource? cancellation = _previewCancellation;
        Task? task = _previewTask;
        _previewCancellation = null;
        _previewTask = null;
        cancellation?.Cancel();
        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }
        cancellation?.Dispose();
    }

    private async Task StopDetectionAsync(bool updateUi)
    {
        CancellationTokenSource? cancellation = _detectionCancellation;
        Task? task = _detectionTask;
        _detectionCancellation = null;
        _detectionTask = null;
        cancellation?.Cancel();
        SafeStopAudio();
        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }
        cancellation?.Dispose();

        if (updateUi)
        {
            SetRunning(false);
            StateText.Text = "已停止";
            StateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 116, 139));
            MetricsText.Text = "等待开始检测";
            Logger.Info("ScreenDetection", "Detection stopped");
        }
    }

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        SelectRegionButton.IsEnabled = !running;
        SelectAudioButton.IsEnabled = !running;
        TargetColorPicker.IsEnabled = !running;
        XBox.IsEnabled = YBox.IsEnabled = WidthBox.IsEnabled = HeightBox.IsEnabled = !running;
        HueToleranceBox.IsEnabled = SaturationToleranceBox.IsEnabled = ValueToleranceBox.IsEnabled = !running;
        MinimumPixelsBox.IsEnabled = MinimumAreaBox.IsEnabled = ScanIntervalBox.IsEnabled = !running;
        PresentFramesBox.IsEnabled = AbsentFramesBox.IsEnabled = !running;
    }

    private Task RunOnUiThreadAsync(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetCanceled();
        }

        return completion.Task;
    }

    private void Audio_PlaybackFailed(object? sender, AudioPlaybackFailedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() => ErrorText.Text = "声音播放失败：" + args.ErrorMessage);
    }

    private void SafeStopAudio()
    {
        try
        {
            _audio.Stop();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
