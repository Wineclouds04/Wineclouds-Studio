using System.Text.Json;
using System.Text.Json.Serialization;
using WinecloudsStudio.Modules.Settings.Models;

namespace WinecloudsStudio.Modules.Settings.Services;

public sealed class ApplicationSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ApplicationSettingsService(string? filePath = null)
    {
        FilePath = Path.GetFullPath(filePath ?? GetDefaultFilePath());
    }

    public string FilePath { get; }

    public ApplicationSettings Load()
    {
        if (!File.Exists(FilePath))
            return new ApplicationSettings();

        try
        {
            ApplicationSettings? settings = JsonSerializer.Deserialize<ApplicationSettings>(
                File.ReadAllText(FilePath),
                JsonOptions);

            return settings?.SchemaVersion == ApplicationSettings.CurrentSchemaVersion
                ? settings
                : new ApplicationSettings();
        }
        catch (JsonException)
        {
            return new ApplicationSettings();
        }
        catch (IOException)
        {
            return new ApplicationSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new ApplicationSettings();
        }
    }

    public void Save(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

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
        "application-settings.json");
}
