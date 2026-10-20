using System.Windows.Media;

namespace PikturShift.Theming;

public enum AppTheme
{
    System,
    Dark,
    Light
}

public enum AccentId
{
    Matcha,
    System,
    Red,
    Pink,
    Cyan,
    Purple,
    Beige,
    Mono
}

public sealed record AccentOption(
    AccentId Id,
    string Key,
    string Name,
    string? DarkHex,
    string? LightHex,
    bool IsSystem = false,
    bool IsMono = false);

public static class AccentCatalog
{
    /// <summary>Order shown in Settings; System sync first, then the solid colours.</summary>
    public static readonly IReadOnlyList<AccentOption> Options = new List<AccentOption>
    {
        new(AccentId.System, "system", "System", null, null, IsSystem: true),
        new(AccentId.Matcha, "matcha", "Matcha", "#AAFF29", "#7CC900"),
        new(AccentId.Red, "red", "Red", "#FF6067", "#D93A42"),
        new(AccentId.Pink, "pink", "Pink", "#FF74B8", "#DE3B8D"),
        new(AccentId.Cyan, "cyan", "Cyan", "#52DBEA", "#0090A3"),
        new(AccentId.Purple, "purple", "Purple", "#B893FF", "#7B57D7"),
        new(AccentId.Beige, "beige", "Beige", "#E9D5B3", "#B08C52"),
        new(AccentId.Mono, "mono", "Black", "#FFFFFF", "#000000", IsMono: true)
    };

    public static AccentOption Find(AccentId id) => Options.First(o => o.Id == id);

    public static AccentId? ParseId(string? key) =>
        Options.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase))?.Id;

    /// <summary>Resolves the colours for the current theme. Mono flips black↔white with the theme, as requested.</summary>
    public static Color Resolve(AccentId id, bool dark, Color systemAccent)
    {
        var option = Find(id);
        Color raw;
        if (option.IsSystem)
        {
            raw = systemAccent;
        }
        else
        {
            raw = ColorUtil.FromHex(dark ? option.DarkHex! : option.LightHex!);
        }

        return dark
            ? ColorUtil.EnsureReadableOnDark(raw)
            : ColorUtil.EnsureReadableOnLight(raw);
    }

    public static string DisplayName(AccentId id, bool dark)
    {
        var option = Find(id);
        if (option.IsMono)
        {
            return dark ? "White" : "Black";
        }

        return option.Name;
    }
}
