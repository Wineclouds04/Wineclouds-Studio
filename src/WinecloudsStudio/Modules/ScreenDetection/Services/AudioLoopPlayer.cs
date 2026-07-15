using Windows.Media.Core;
using Windows.Media.Playback;

namespace WinecloudsStudio.Modules.ScreenDetection.Services;

public sealed class AudioLoopPlayer : IDisposable
{
    private readonly MediaPlayer _player = new();
    private MediaSource? _source;
    private bool _disposed;

    public AudioLoopPlayer()
    {
        _player.MediaFailed += OnMediaFailed;
    }

    public event EventHandler<AudioPlaybackFailedEventArgs>? PlaybackFailed;

    public string? FilePath { get; private set; }

    public string? LastError { get; private set; }

    public void SetFile(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("请选择 MP3 声音文件。", nameof(filePath));

        string fullPath = Path.GetFullPath(filePath);
        if (!string.Equals(Path.GetExtension(fullPath), ".mp3", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("声音文件必须是本地 MP3 文件。", nameof(filePath));
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("声音文件不存在。", fullPath);

        Stop();
        _player.Source = null;
        _source?.Dispose();
        _source = MediaSource.CreateFromUri(new Uri(fullPath, UriKind.Absolute));
        _player.Source = _source;
        FilePath = fullPath;
        LastError = null;
    }

    public void StartLoop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_source is null || string.IsNullOrWhiteSpace(FilePath))
            throw new InvalidOperationException("请先选择 MP3 声音文件。");

        LastError = null;
        _player.IsLoopingEnabled = true;
        _player.PlaybackSession.Position = TimeSpan.Zero;
        _player.Play();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _player.IsLoopingEnabled = false;
        _player.Pause();
        _player.PlaybackSession.Position = TimeSpan.Zero;
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        _player.IsLoopingEnabled = false;
        LastError = string.IsNullOrWhiteSpace(args.ErrorMessage)
            ? "MP3 播放失败。"
            : args.ErrorMessage;
        PlaybackFailed?.Invoke(this, new AudioPlaybackFailedEventArgs(LastError, args.ExtendedErrorCode));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _player.IsLoopingEnabled = false;
        _player.Pause();
        _player.Source = null;
        _player.MediaFailed -= OnMediaFailed;
        _source?.Dispose();
        _source = null;
        _player.Dispose();
        _disposed = true;
    }
}
