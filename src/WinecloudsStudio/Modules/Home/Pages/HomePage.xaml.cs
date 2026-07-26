using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinecloudsStudio.Shared;

namespace WinecloudsStudio.Modules.Home.Pages;

public sealed partial class HomePage : Page
{
    private const string Logo =
        """
        ██╗    ██╗██╗███╗   ██╗███████╗ ██████╗██╗      ██████╗ ██╗   ██╗██████╗ ███████╗
        ██║    ██║██║████╗  ██║██╔════╝██╔════╝██║     ██╔═══██╗██║   ██║██╔══██╗██╔════╝
        ██║ █╗ ██║██║██╔██╗ ██║█████╗  ██║     ██║     ██║   ██║██║   ██║██║  ██║███████╗
        ██║███╗██║██║██║╚██╗██║██╔══╝  ██║     ██║     ██║   ██║██║   ██║██║  ██║╚════██║
        ╚███╔███╔╝██║██║ ╚████║███████╗╚██████╗███████╗╚██████╔╝╚██████╔╝██████╔╝███████║
         ╚══╝╚══╝ ╚═╝╚═╝  ╚═══╝╚══════╝ ╚═════╝╚══════╝ ╚═════╝  ╚═════╝ ╚═════╝ ╚══════╝
        """;

    private const string Tagline =
        "The Windows workspace for multi-window workflows";
    private const string Cursor = "▋";

    private readonly DispatcherQueueTimer _cursorTimer;
    private CancellationTokenSource? _animationCancellation;
    private bool _cursorVisible = true;

    public HomePage()
    {
        InitializeComponent();
        VersionText.Text = $"VERSION {BuildInfo.DisplayVersion}";

        _cursorTimer = DispatcherQueue.CreateTimer();
        _cursorTimer.Interval = TimeSpan.FromMilliseconds(520);
        _cursorTimer.Tick += CursorTimer_Tick;

        Loaded += HomePage_Loaded;
        Unloaded += HomePage_Unloaded;
    }

    private async void HomePage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _animationCancellation?.Cancel();
        _animationCancellation?.Dispose();
        _animationCancellation = new CancellationTokenSource();

        try
        {
            await PlayTypewriterAsync(
                _animationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void HomePage_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _animationCancellation?.Cancel();
        _cursorTimer.Stop();
    }

    private async Task PlayTypewriterAsync(
        CancellationToken cancellationToken)
    {
        _cursorTimer.Stop();
        SetLogoText(string.Empty, showCursor: false);
        SetTaglineText(string.Empty, showCursor: false);

        await Task.Delay(
            TimeSpan.FromMilliseconds(220),
            cancellationToken);

        var logoBuilder = new StringBuilder(Logo.Length);
        foreach (char character in Logo)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logoBuilder.Append(character);
            SetLogoText(
                logoBuilder.ToString(),
                showCursor: true);

            await Task.Delay(
                character is '\r' or '\n'
                    ? TimeSpan.FromMilliseconds(18)
                    : TimeSpan.FromMilliseconds(7),
                cancellationToken);
        }

        SetLogoText(Logo, showCursor: false);
        await Task.Delay(
            TimeSpan.FromMilliseconds(120),
            cancellationToken);

        var taglineBuilder =
            new StringBuilder(Tagline.Length);
        foreach (char character in Tagline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            taglineBuilder.Append(character);
            SetTaglineText(
                taglineBuilder.ToString(),
                showCursor: true);

            await Task.Delay(
                TimeSpan.FromMilliseconds(34),
                cancellationToken);
        }

        _cursorVisible = true;
        SetTaglineText(Tagline, showCursor: true);
        _cursorTimer.Start();
    }

    private void CursorTimer_Tick(
        DispatcherQueueTimer sender,
        object args)
    {
        _cursorVisible = !_cursorVisible;
        SetTaglineText(
            Tagline,
            showCursor: _cursorVisible);
    }

    private void SetLogoText(
        string text,
        bool showCursor)
    {
        LogoAccentCyanText.Text = text;
        LogoAccentAmberText.Text = text;
        LogoText.Text = showCursor
            ? text + Cursor
            : text;
    }

    private void SetTaglineText(
        string text,
        bool showCursor)
    {
        TaglineAccentCyanText.Text = text;
        TaglineAccentAmberText.Text = text;
        TaglineText.Text = showCursor
            ? text + Cursor
            : text;
    }
}
