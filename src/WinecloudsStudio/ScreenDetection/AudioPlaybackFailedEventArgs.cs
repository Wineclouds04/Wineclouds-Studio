namespace WinecloudsStudio.ScreenDetection;

public sealed class AudioPlaybackFailedEventArgs(string errorMessage, Exception? exception) : EventArgs
{
    public string ErrorMessage { get; } = errorMessage;

    public Exception? Exception { get; } = exception;
}
