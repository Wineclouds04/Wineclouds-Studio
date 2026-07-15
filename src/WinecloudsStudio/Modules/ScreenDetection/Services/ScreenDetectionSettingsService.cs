using System.Text.Json;

namespace WinecloudsStudio.Modules.ScreenDetection.Services;

public sealed class ScreenDetectionSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ScreenDetectionSettingsService(string? filePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? GetDefaultFilePath());
    }

    public string FilePath { get; }

    public bool IsReadOnly { get; private set; }

    public string? ReadOnlyReason { get; private set; }

    public ScreenDetectionSettings Load()
    {
        IsReadOnly = false;
        ReadOnlyReason = null;

        if (!File.Exists(FilePath))
            return new ScreenDetectionSettings();

        try
        {
            ScreenDetectionSettings? settings = JsonSerializer.Deserialize<ScreenDetectionSettings>(
                File.ReadAllText(FilePath),
                JsonOptions);

            if (settings is null)
                return new ScreenDetectionSettings();

            if (settings.SchemaVersion != ScreenDetectionSettings.CurrentSchemaVersion)
            {
                IsReadOnly = true;
                ReadOnlyReason = $"配置版本 {settings.SchemaVersion} 不受当前程序支持，已禁止覆盖保存。";
            }

            return settings;
        }
        catch (JsonException)
        {
            return new ScreenDetectionSettings();
        }
        catch (IOException)
        {
            return new ScreenDetectionSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new ScreenDetectionSettings();
        }
    }

    public void Save(ScreenDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (IsReadOnly)
            throw new InvalidOperationException(ReadOnlyReason ?? "当前配置为只读，不能保存。");
        if (settings.SchemaVersion != ScreenDetectionSettings.CurrentSchemaVersion)
            throw new InvalidOperationException($"不能保存配置版本 {settings.SchemaVersion}。");

        string directoryPath = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directoryPath);
        string temporaryPath = FilePath + ".tmp";

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string GetDefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinecloudsStudio",
        "screen-region-detector.json");
}
