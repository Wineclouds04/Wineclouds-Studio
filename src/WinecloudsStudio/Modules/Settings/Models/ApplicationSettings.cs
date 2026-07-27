namespace WinecloudsStudio.Modules.Settings.Models;

public sealed record ApplicationSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public AppCloseBehavior CloseBehavior { get; init; } = AppCloseBehavior.ExitApplication;

    public AppThemeMode ThemeMode { get; init; } = AppThemeMode.System;
}
