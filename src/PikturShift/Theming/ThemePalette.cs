using System.Reflection;
using System.Windows.Media;

namespace PikturShift.Theming;

/// <summary>
/// Every colour token the UI needs, for one visual theme. Colours are authored to match
/// the reference mockup in dark mode and to carry the same structure in light mode.
/// </summary>
public sealed class ThemePalette
{
    public required Color Window { get; init; }
    public required Color Rail { get; init; }
    public required Color Content { get; init; }
    public required Color Card { get; init; }
    public required Color CardBorder { get; init; }
    public required Color CardHover { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceBorder { get; init; }
    public required Color SurfaceHover { get; init; }
    public required Color Control { get; init; }
    public required Color ControlHover { get; init; }
    public required Color ControlPressed { get; init; }
    public required Color ControlBorder { get; init; }
    public required Color ControlBorderStrong { get; init; }
    public required Color Divider { get; init; }
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color TextTertiary { get; init; }
    public required Color TextDisabled { get; init; }
    public required Color SwitchOff { get; init; }
    public required Color SwitchOffHover { get; init; }
    public required Color RailHover { get; init; }
    public required Color RailSelected { get; init; }
    public required Color CaptionHover { get; init; }
    public required Color CaptionCloseHover { get; init; }
    public required Color ScrollThumb { get; init; }
    public required Color ScrollThumbHover { get; init; }
    public required Color Popup { get; init; }
    public required Color PopupBorder { get; init; }
    public required Color PopupHover { get; init; }
    public required Color WindowBorder { get; init; }
    public required Color Error { get; init; }
    public required Color Warning { get; init; }
    public required Color Shadow { get; init; }

    /// <summary>
    /// Returns a copy of the palette re-hued to <paramref name="hue"/> (the selected accent),
    /// keeping every token's lightness so contrast is unchanged. The mockup's neutrals are
    /// olive because they were built around the matcha accent - re-hueing makes the same
    /// surfaces pick up whatever accent colour is chosen, so no fixed green survives.
    /// </summary>
    public ThemePalette Tinted(double hue, double saturationScale)
    {
        var clone = (ThemePalette)MemberwiseClone();

        foreach (var property in typeof(ThemePalette).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType != typeof(Color) || property.GetValue(clone) is not Color current)
            {
                continue;
            }

            // Semantic colours keep their meaning: the close button stays red, errors stay red, etc.
            if (property.Name is nameof(CaptionCloseHover) or nameof(Error) or nameof(Warning) or nameof(Shadow))
            {
                continue;
            }

            property.SetValue(clone, current.WithHue(hue, saturationScale));
        }

        return clone;
    }

    public static readonly ThemePalette Dark = new()
    {
        Window = ColorUtil.FromHex("#1F221A"),
        Rail = ColorUtil.FromHex("#1F221A"),
        Content = ColorUtil.FromHex("#262921"),
        Card = ColorUtil.FromHex("#2C2F27"),
        CardBorder = ColorUtil.FromHex("#3A3D34"),
        CardHover = ColorUtil.FromHex("#33362D"),
        Surface = ColorUtil.FromHex("#31342C"),
        SurfaceBorder = ColorUtil.FromHex("#4A4D43"),
        SurfaceHover = ColorUtil.FromHex("#383B33"),
        Control = ColorUtil.FromHex("#3A3D35"),
        ControlHover = ColorUtil.FromHex("#454840"),
        ControlPressed = ColorUtil.FromHex("#2F322B"),
        ControlBorder = ColorUtil.FromHex("#4A4D44"),
        ControlBorderStrong = ColorUtil.FromHex("#5C6055"),
        Divider = ColorUtil.FromHex("#3C3F36"),
        TextPrimary = ColorUtil.FromHex("#F1F2ED"),
        TextSecondary = ColorUtil.FromHex("#A9ADA0"),
        TextTertiary = ColorUtil.FromHex("#82867A"),
        TextDisabled = ColorUtil.FromHex("#61655B"),
        SwitchOff = ColorUtil.FromHex("#54584C"),
        SwitchOffHover = ColorUtil.FromHex("#63675A"),
        RailHover = ColorUtil.FromHex("#2A2D24"),
        RailSelected = ColorUtil.FromHex("#2F332A"),
        CaptionHover = ColorUtil.FromHex("#33FFFFFF"),
        CaptionCloseHover = ColorUtil.FromHex("#C42B1C"),
        ScrollThumb = ColorUtil.FromHex("#5A5E52"),
        ScrollThumbHover = ColorUtil.FromHex("#71766A"),
        Popup = ColorUtil.FromHex("#2F322A"),
        PopupBorder = ColorUtil.FromHex("#4B4F45"),
        PopupHover = ColorUtil.FromHex("#3D4037"),
        WindowBorder = ColorUtil.FromHex("#00000000"),
        Error = ColorUtil.FromHex("#FF8A8A"),
        Warning = ColorUtil.FromHex("#FFD479"),
        Shadow = ColorUtil.FromHex("#CC000000")
    };

    public static readonly ThemePalette Light = new()
    {
        Window = ColorUtil.FromHex("#F3F3EF"),
        Rail = ColorUtil.FromHex("#EDEDE8"),
        Content = ColorUtil.FromHex("#F7F7F3"),
        Card = ColorUtil.FromHex("#FFFFFF"),
        CardBorder = ColorUtil.FromHex("#E2E2DA"),
        CardHover = ColorUtil.FromHex("#FAFAF6"),
        Surface = ColorUtil.FromHex("#FBFBF7"),
        SurfaceBorder = ColorUtil.FromHex("#C9C9BF"),
        SurfaceHover = ColorUtil.FromHex("#F3F3ED"),
        Control = ColorUtil.FromHex("#FFFFFF"),
        ControlHover = ColorUtil.FromHex("#F5F5F0"),
        ControlPressed = ColorUtil.FromHex("#ECECE6"),
        ControlBorder = ColorUtil.FromHex("#DADAD1"),
        ControlBorderStrong = ColorUtil.FromHex("#BEBEB3"),
        Divider = ColorUtil.FromHex("#E6E6DE"),
        TextPrimary = ColorUtil.FromHex("#1B1D18"),
        TextSecondary = ColorUtil.FromHex("#5B5F55"),
        TextTertiary = ColorUtil.FromHex("#84887D"),
        TextDisabled = ColorUtil.FromHex("#A8ACA2"),
        SwitchOff = ColorUtil.FromHex("#BFC2B8"),
        SwitchOffHover = ColorUtil.FromHex("#A9ACA1"),
        RailHover = ColorUtil.FromHex("#E6E6DF"),
        RailSelected = ColorUtil.FromHex("#E0E5D5"),
        CaptionHover = ColorUtil.FromHex("#14000000"),
        CaptionCloseHover = ColorUtil.FromHex("#C42B1C"),
        ScrollThumb = ColorUtil.FromHex("#C4C4BA"),
        ScrollThumbHover = ColorUtil.FromHex("#A0A096"),
        Popup = ColorUtil.FromHex("#FFFFFF"),
        PopupBorder = ColorUtil.FromHex("#D6D6CC"),
        PopupHover = ColorUtil.FromHex("#F1F1EA"),
        WindowBorder = ColorUtil.FromHex("#D2D2C8"),
        Error = ColorUtil.FromHex("#C42B1C"),
        Warning = ColorUtil.FromHex("#9B6100"),
        Shadow = ColorUtil.FromHex("#33000000")
    };
}
