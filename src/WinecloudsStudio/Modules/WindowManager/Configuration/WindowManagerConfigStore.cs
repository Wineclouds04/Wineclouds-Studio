using System.Text.Json;

namespace WinecloudsStudio.Modules.WindowManager.Configuration;

/// <summary>
/// Persists <see cref="WindowManagerConfig"/> to a local JSON file
/// so thumbnail resolution and display settings survive across sessions.
/// </summary>
public class WindowManagerConfigStore
{
    private readonly string _filePath;

    public WindowManagerConfigStore()
    {
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "WinecloudsStudio");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "window_manager_config.json");
    }

    /// <summary>
    /// Loads the saved configuration, or returns a fresh default if none exists.
    /// </summary>
    public WindowManagerConfig Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<WindowManagerConfig>(json)
                       ?? new WindowManagerConfig();
            }
        }
        catch
        {
            // Corrupt or missing file — use defaults
        }

        return new WindowManagerConfig();
    }

    /// <summary>
    /// Writes the configuration to disk.
    /// </summary>
    public void Save(WindowManagerConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
