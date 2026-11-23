using System.Text.Json;

namespace MagicPanel.Core;

/// <summary>Saves/loads settings as JSON in %AppData%\MagicPanel\settings.json.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string FilePath { get; }

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MagicPanel", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (loaded != null) return loaded;
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not read settings, using defaults: " + ex.Message);
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}
