using System.Text.Json;

namespace WinecloudsStudio.Modules.WindowManager.Configuration;

/// <summary>
/// Persists thumbnail window positions to a local JSON file,
/// keyed by "processName::windowTitle" so positions survive across sessions.
/// </summary>
public class ThumbnailWindowPositionStore
{
    private readonly string _filePath;

    public ThumbnailWindowPositionStore()
    {
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "WinecloudsStudio");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "thumbnail_positions.json");
    }

    /// <summary>
    /// Returns the last saved (X, Y) for a window key, or null if never saved.
    /// </summary>
    public (int X, int Y)? GetPosition(string key)
    {
        var positions = Load();
        if (positions.TryGetValue(key.ToLowerInvariant(), out var pos))
            return (pos.X, pos.Y);
        return null;
    }

    /// <summary>
    /// Persists a batch of key → (X, Y) mappings to disk.
    /// </summary>
    public void SavePositions(Dictionary<string, (int X, int Y)> positions)
    {
        var data = positions.ToDictionary(
            kvp => kvp.Key.ToLowerInvariant(),
            kvp => new WindowPosition { X = kvp.Value.X, Y = kvp.Value.Y });

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    private Dictionary<string, WindowPosition> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<Dictionary<string, WindowPosition>>(json)
                       ?? new(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Corrupt or missing file — start fresh
        }

        return new(StringComparer.OrdinalIgnoreCase);
    }

    private class WindowPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
