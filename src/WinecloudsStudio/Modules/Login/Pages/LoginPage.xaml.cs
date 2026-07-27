using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinecloudsStudio.Modules.Login.Pages;

public sealed partial class LoginPage : Page
{
    private const string AccountServiceUnavailableMessage =
        "账号服务尚未接入，当前页面用于界面与交互预览。";

    public LoginPage()
    {
        InitializeComponent();
    }

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string mode })
            SetMode(mode);
    }

    private void ForgotPasswordLink_Click(object sender, RoutedEventArgs e)
    {
        SetMode("recover");
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateEmail(LoginEmailBox.Text))
        {
            ShowStatus(InfoBarSeverity.Error, "请输入有效的邮箱地址。");
            return;
        }

        if (LoginPasswordBox.Password.Length < 8)
        {
            ShowStatus(InfoBarSeverity.Error, "密码至少需要 8 位。");
            return;
        }

        ShowStatus(InfoBarSeverity.Informational, AccountServiceUnavailableMessage);
    }

    private void SendRegisterCodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateEmail(RegisterEmailBox.Text))
        {
            ShowStatus(InfoBarSeverity.Error, "请输入有效的注册邮箱。");
            return;
        }

        ShowStatus(InfoBarSeverity.Informational, AccountServiceUnavailableMessage);
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateEmail(RegisterEmailBox.Text))
        {
            ShowStatus(InfoBarSeverity.Error, "请输入有效的注册邮箱。");
            return;
        }

        if (RegisterPasswordBox.Password.Length < 8)
        {
            ShowStatus(InfoBarSeverity.Error, "密码至少需要 8 位。");
            return;
        }

        string verificationCode = RegisterCodeBox.Text.Trim();
        if (verificationCode.Length != 6 || !verificationCode.All(char.IsDigit))
        {
            ShowStatus(InfoBarSeverity.Error, "请输入 6 位数字验证码。");
            return;
        }

        ShowStatus(InfoBarSeverity.Informational, AccountServiceUnavailableMessage);
    }

    private void SendRecoverCodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateEmail(RecoverEmailBox.Text))
        {
            ShowStatus(InfoBarSeverity.Error, "请输入有效的注册邮箱。");
            return;
        }

        ShowStatus(InfoBarSeverity.Informational, AccountServiceUnavailableMessage);
    }

    private void WeChatLoginButton_Click(object sender, RoutedEventArgs e)
    {
        ShowStatus(
            InfoBarSeverity.Informational,
            "微信登录服务尚未接入，当前入口仅用于界面预览。");
    }

    private void SetMode(string mode)
    {
        LoginPanel.Visibility = mode == "login" ? Visibility.Visible : Visibility.Collapsed;
        RegisterPanel.Visibility = mode == "register" ? Visibility.Visible : Visibility.Collapsed;
        RecoverPanel.Visibility = mode == "recover" ? Visibility.Visible : Visibility.Collapsed;

        Style normalStyle = (Style)Resources["AccountModeButtonStyle"];
        Style selectedStyle = (Style)Resources["SelectedAccountModeButtonStyle"];
        LoginModeButton.Style = mode == "login" ? selectedStyle : normalStyle;
        RegisterModeButton.Style = mode == "register" ? selectedStyle : normalStyle;
        RecoverModeButton.Style = mode == "recover" ? selectedStyle : normalStyle;
        AccountStatusInfoBar.IsOpen = false;
    }

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        AccountStatusInfoBar.Severity = severity;
        AccountStatusInfoBar.Message = message;
        AccountStatusInfoBar.IsOpen = true;
    }

    private static bool ValidateEmail(string value)
    {
        string email = value.Trim();
        int atIndex = email.IndexOf('@');
        return atIndex > 0 &&
               atIndex < email.Length - 3 &&
               email.IndexOf('.', atIndex + 2) > atIndex + 1;
    }
}
