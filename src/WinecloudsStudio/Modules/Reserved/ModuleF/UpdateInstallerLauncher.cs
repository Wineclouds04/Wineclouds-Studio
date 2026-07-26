using System.Diagnostics;
using System.Text;
using WinecloudsStudio.Shared.Logging;

namespace WinecloudsStudio.Modules.Reserved.ModuleF;

internal static class UpdateInstallerLauncher
{
    public static void LaunchAfterCurrentProcessExits(
        string installerPath)
    {
        string fullInstallerPath =
            Path.GetFullPath(installerPath);
        if (!File.Exists(fullInstallerPath)
            || !Path.GetExtension(fullInstallerPath).Equals(
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException(
                "找不到已下载的更新安装包。",
                fullInstallerPath);
        }

        string windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);
        string powerShellPath = Path.Combine(
            windowsDirectory,
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (!File.Exists(powerShellPath))
        {
            throw new FileNotFoundException(
                "找不到 Windows PowerShell，无法安排更新安装。",
                powerShellPath);
        }

        string escapedInstaller =
            fullInstallerPath.Replace("'", "''");
        string escapedWorkingDirectory =
            Path.GetDirectoryName(fullInstallerPath)!
                .Replace("'", "''");
        string command =
            $"Wait-Process -Id {Environment.ProcessId} "
            + "-ErrorAction SilentlyContinue; "
            + $"Start-Process -FilePath '{escapedInstaller}' "
            + $"-WorkingDirectory '{escapedWorkingDirectory}'";
        string encodedCommand = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(command));

        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedCommand);

        Process? helper = Process.Start(startInfo);
        if (helper == null)
        {
            throw new InvalidOperationException(
                "无法启动更新安装辅助进程。");
        }

        Logger.Info(
            "Updater",
            $"Installer scheduled after process exit: {fullInstallerPath}");
    }
}
