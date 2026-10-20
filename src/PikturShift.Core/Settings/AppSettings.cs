using System.Text.Json;
using System.Text.Json.Serialization;

namespace PikturShift.Core.Settings;

/// <summary>Everything the app remembers between launches.</summary>
public sealed class AppSettings
{
    /// <summary>System | Dark | Light</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>matcha | system | red | pink | cyan | purple | beige | mono</summary>
    public string Accent { get; set; } = "matcha";

    public string Format { get; set; } = "Jpg";
    public int Quality { get; set; } = 100;
    public string FillColor { get; set; } = "#FFFFFF";
    public bool ChooseFolder { get; set; }
    public string OutputFolder { get; set; } = string.Empty;
    public bool Overwrite { get; set; } = true;
    public bool KeepExif { get; set; }
    public bool OpenFolderAfterConvert { get; set; }

    public bool NavExpanded { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 700;
    public bool Maximized { get; set; }

}

/// <summary>Tiny JSON settings store with atomic writes and forgiving loads.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath;
    }

    public string FilePath { get; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PikturShift",
        "settings.json");

    /// <summary>Older builds stored settings under the previous app name; carry them over once.</summary>
    public static void MigrateLegacyFile()
    {
        try
        {
            if (File.Exists(DefaultPath))
            {
                return;
            }

            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhotoConverter",
                "settings.json");

            if (File.Exists(legacy))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
                File.Copy(legacy, DefaultPath, overwrite: false);
            }
        }
        catch
        {
            // Migration is a convenience; never block startup.
        }
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var text = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(text, JsonOptions);
                if (loaded is not null)
                {
                    return Sanitize(loaded);
                }
            }
        }
        catch
        {
            // Corrupt settings should never stop the app from opening.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            var temp = Path.Combine(dir, $"settings.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch
        {
            // ignore – settings are a convenience, not critical state
        }
    }

    private static AppSettings Sanitize(AppSettings s)
    {
        var themes = new[] { "System", "Dark", "Light" };
        if (!themes.Contains(s.Theme, StringComparer.OrdinalIgnoreCase))
        {
            s.Theme = "Dark";
        }

        var accents = new[] { "matcha", "system", "red", "pink", "cyan", "purple", "beige", "mono" };
        if (!accents.Contains(s.Accent, StringComparer.OrdinalIgnoreCase))
        {
            s.Accent = "matcha";
        }

        s.Quality = Math.Clamp(s.Quality, 1, 100);
        if (string.IsNullOrWhiteSpace(s.FillColor) || !s.FillColor.StartsWith('#'))
        {
            s.FillColor = "#FFFFFF";
        }

        if (s.WindowWidth < 820)
        {
            s.WindowWidth = 1100;
        }

        if (s.WindowHeight < 560)
        {
            s.WindowHeight = 700;
        }

        return s;
    }
}
