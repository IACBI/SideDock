using System.IO;
using System.Text.Json;
using SideDock.Models;

namespace SideDock.Services;

/// <summary>
/// Loads and saves the dock's JSON config file under
/// <c>%APPDATA%\SideDock\config.json</c>. If the file is missing (first run)
/// a sensible default is created so the user has something to see and to edit.
/// </summary>
public static class ConfigService
{
    // %APPDATA% = C:\Users\<you>\AppData\Roaming  (per-user, roams with profile).
    private static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SideDock");

    private static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    // Shared serializer settings: pretty-printed and forgiving about casing so
    // hand-edited files (e.g. "name" vs "Name") still load.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Full path to the config file (handy for tooltips/logging).</summary>
    public static string FilePath => ConfigFilePath;

    /// <summary>
    /// Reads the config file. On first run (or if the file is unreadable) it
    /// writes and returns a default config instead of throwing.
    /// </summary>
    public static DockConfig LoadOrCreateDefault()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<DockConfig>(json, JsonOptions);
                if (config is not null)
                    return config;
            }
        }
        catch
        {
            // Missing/corrupt file: fall through to defaults rather than crash
            // the dock. (A later phase can surface a friendlier warning.)
        }

        var defaults = CreateDefaultConfig();
        try
        {
            Save(defaults);
        }
        catch
        {
            // Best-effort: if we can't write the file (locked/unwritable), still
            // return usable defaults so the dock runs.
        }
        return defaults;
    }

    /// <summary>Writes the config file, creating the folder if needed.</summary>
    public static void Save(DockConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, JsonOptions));
    }

    // A small starter set of built-in Windows apps, using full paths so both
    // icon extraction and launching are reliable.
    private static DockConfig CreateDefaultConfig()
    {
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);   // ...\System32
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows); // ...\Windows

        return new DockConfig
        {
            Apps =
            {
                new AppEntry { Name = "File Explorer", Path = Path.Combine(windows, "explorer.exe") },
                new AppEntry { Name = "Notepad",       Path = Path.Combine(system,  "notepad.exe") },
                new AppEntry { Name = "Calculator",    Path = Path.Combine(system,  "calc.exe") },
            }
        };
    }
}
