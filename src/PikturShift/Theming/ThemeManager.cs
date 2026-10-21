using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using PikturShift.Core.Imaging;

namespace PikturShift.Theming;

/// <summary>
/// Owns the live theme: resolves System/Dark/Light and the accent colour, writes every
/// brush token into Application.Resources (so DynamicResource picks changes up instantly)
/// and keeps listening for Windows theme / accent changes while "System" is selected.
/// </summary>
public static class ThemeManager
{
    private static bool _watching;

    public static AppTheme Theme { get; private set; } = AppTheme.Dark;
    public static AccentId Accent { get; private set; } = AccentId.Matcha;
    public static bool IsDark { get; private set; } = true;
    public static Color AccentColor { get; private set; } = ColorUtil.FromHex("#AAFF29");
    public static Color AccentForeground { get; private set; } = Colors.Black;
    public static Color SystemAccentColor { get; private set; } = ColorUtil.FromHex("#0078D4");

    /// <summary>
    /// True once the Windows 11 Mica backdrop is live. Only then do the shell surfaces become
    /// translucent - without a backdrop behind them alpha would just blend to black.
    /// </summary>
    public static bool UseTranslucency { get; private set; }

    /// <summary>
    /// How much of the backdrop each shell surface lets through. The rail runs up into the
    /// caption bar, so the caption shares its value; the content surface is deliberately
    /// lighter so the sidebar still reads as a distinct layer.
    /// </summary>
    private static double RailTranslucency => IsDark ? 0.060 : 0.025;
    private static double ContentTranslucency => IsDark ? 0.080 : 0.015;
    public static ThemePalette Palette { get; private set; } = ThemePalette.Dark;
    public static string AccentDisplayName => AccentCatalog.DisplayName(Accent, IsDark);
    public static string SystemAccentHex => ColorUtil.ToHex(SystemAccentColor);

    /// <summary>Raised after the brushes change; native window chrome re-applies its colours.</summary>
    public static event EventHandler? ThemeChanged;

    public static void Initialize(string? theme, string? accent)
    {
        Theme = ParseTheme(theme);
        Accent = AccentCatalog.ParseId(accent) ?? AccentId.Matcha;
        SystemAccentColor = ReadSystemAccent();
        Apply(silent: true);
        StartWatching();
    }

    /// <summary>Called once the window knows whether a system backdrop is available.</summary>
    public static void EnableTranslucency(bool enabled)
    {
        if (UseTranslucency == enabled)
        {
            return;
        }

        UseTranslucency = enabled;
        WriteResources();
    }

    /// <summary>Alpha (0-255) for a surface, or fully opaque when translucency is off.</summary>
    private static byte SurfaceAlpha(double translucency) =>
        UseTranslucency ? (byte)Math.Round(255 * (1 - translucency)) : (byte)255;

    public static void Apply(string? theme = null, string? accent = null, bool silent = false)
    {
        if (theme is not null)
        {
            Theme = ParseTheme(theme);
        }

        if (accent is not null)
        {
            Accent = AccentCatalog.ParseId(accent) ?? Accent;
        }

        SystemAccentColor = ReadSystemAccent();
        IsDark = Theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDark()
        };

        AccentColor = AccentCatalog.Resolve(Accent, IsDark, SystemAccentColor);
        AccentForeground = ColorUtil.ContrastForeground(AccentColor);
        // Surfaces follow the accent hue; a mono accent (black/white) leaves them neutral.
        var basePalette = IsDark ? ThemePalette.Dark : ThemePalette.Light;
        var accentHsl = ColorUtil.From(AccentColor);
        Palette = accentHsl.S > 0.12
            ? basePalette.Tinted(accentHsl.H, 1.0)
            : basePalette.Tinted(0, 0.0);

        WriteResources();

        if (!silent)
        {
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    private static void WriteResources()
    {
        var p = Palette;
        var app = Application.Current?.Resources;
        if (app is null)
        {
            return;
        }

        var accent = AccentColor;
        var accentText = IsDark ? accent : ColorUtil.EnsureReadableOnLight(accent);

        // The rail and the caption bar above it share one translucent tint; the content
        // surface gets a lighter one; the window itself is fully transparent so the Mica
        // backdrop (wallpaper) shows through the two of them.
        byte railAlpha = SurfaceAlpha(RailTranslucency);
        byte contentAlpha = SurfaceAlpha(ContentTranslucency);

        Set("WindowBgBrush", UseTranslucency ? Colors.Transparent : p.Window);

        // The caption bar and the rail are one continuous strip: the rail runs up into the
        // titlebar, so both surfaces use the rail colour. They used to differ (the caption
        // took the window colour), which in Dark is the same value but in Light showed up as
        // a visible seam where the strip meets the pane.
        Set("CaptionBgBrush", ColorUtil.WithAlpha(p.Rail, railAlpha));
        Set("RailBgBrush", ColorUtil.WithAlpha(p.Rail, railAlpha));

        Set("ContentBgBrush", ColorUtil.WithAlpha(p.Content, contentAlpha));
        Set("CardBgBrush", p.Card);
        Set("CardBorderBrush", p.CardBorder);
        Set("CardHoverBrush", p.CardHover);
        Set("SurfaceBrush", p.Surface);
        Set("SurfaceBorderBrush", p.SurfaceBorder);
        Set("SurfaceHoverBrush", p.SurfaceHover);
        Set("ControlBgBrush", p.Control);
        Set("ControlHoverBrush", p.ControlHover);
        Set("ControlPressedBrush", p.ControlPressed);
        Set("ControlBorderBrush", p.ControlBorder);
        Set("ControlBorderStrongBrush", p.ControlBorderStrong);
        Set("DividerBrush", p.Divider);
        Set("TextPrimaryBrush", p.TextPrimary);
        Set("TextSecondaryBrush", p.TextSecondary);
        Set("TextTertiaryBrush", p.TextTertiary);
        Set("TextDisabledBrush", p.TextDisabled);
        Set("SwitchOffBrush", p.SwitchOff);
        Set("SwitchOffHoverBrush", p.SwitchOffHover);
        Set("RailHoverBrush", p.RailHover);
        Set("RailSelectedBrush", ColorUtil.Mix(p.Rail, accent, IsDark ? 0.13 : 0.16));
        Set("CaptionHoverBrush", p.CaptionHover);
        Set("CaptionCloseHoverBrush", p.CaptionCloseHover);
        Set("ScrollThumbBrush", p.ScrollThumb);
        Set("ScrollThumbHoverBrush", p.ScrollThumbHover);
        Set("PopupBgBrush", p.Popup);
        Set("PopupBorderBrush", p.PopupBorder);
        Set("PopupHoverBrush", p.PopupHover);
        Set("WindowBorderBrush", p.WindowBorder);
        Set("ErrorBrush", p.Error);
        Set("WarningBrush", p.Warning);
        Set("ShadowBrush", p.Shadow);

        Set("AccentBrush", accent);
        Set("AccentTextBrush", accentText);
        Set("AccentHoverBrush", IsDark ? ColorUtil.Lighten(accent, 0.14) : ColorUtil.Darken(accent, 0.08));
        Set("AccentPressedBrush", IsDark ? ColorUtil.Lighten(accent, 0.24) : ColorUtil.Darken(accent, 0.18));
        Set("AccentFgBrush", AccentForeground);
        Set("AccentSubtleBrush", ColorUtil.WithAlpha(accent, IsDark ? (byte)0x2E : (byte)0x24));
        Set("AccentSubtleHoverBrush", ColorUtil.WithAlpha(accent, IsDark ? (byte)0x40 : (byte)0x33));
        Set("AccentBorderBrush", ColorUtil.WithAlpha(accent, 0xB0));
        Set("AccentDisabledBrush", ColorUtil.Mix(p.Control, accent, 0.25));
        Set("DropHoverBrush", ColorUtil.WithAlpha(accent, IsDark ? (byte)0x14 : (byte)0x0F));
        Set("DropBorderHoverBrush", accent);
        Set("SelectionPillBrush", accent);
        Set("SuccessBrush", IsDark ? accent : ColorUtil.EnsureReadableOnLight(accent));
        Set("ThumbBorderBrush", IsDark ? ColorUtil.FromHex("#40FFFFFF") : ColorUtil.FromHex("#22000000"));
    }

    /// <summary>
    /// Resource dictionaries freeze Freezables on insert, so brushes are never mutated in
    /// place: each theme change swaps in a freshly created brush and DynamicResource
    /// references pick it up everywhere.
    /// </summary>
    private static void Set(string key, Color color)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        app.Resources[key] = brush;
    }

    // ------------------------------------------------------------- system

    public static AppTheme ParseTheme(string? value) =>
        Enum.TryParse<AppTheme>(value, ignoreCase: true, out var t) ? t : AppTheme.Dark;

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int light)
            {
                return light == 0;
            }
        }
        catch
        {
            // fall through
        }

        return true;
    }

    /// <summary>
    /// Reads the user's Windows accent colour, in the same order of authority Windows
    /// itself uses: AccentColorMenu (the exact swatch Settings shows), then
    /// AccentPalette[3], then the DWM accent, then the glass colour as a last resort.
    ///
    /// Byte orders matter: AccentPalette is stored as 8 x R,G,B,X groups (so bytes
    /// 0,1,2 are red, green, blue in that order), while the DWORDs are 0xAABBGGRR.
    /// Getting those two wrong swaps red and blue - a blue accent silently becomes
    /// orange - so every candidate is sanity-checked against the others.
    /// </summary>
    private static Color ReadSystemAccent()
    {
        Color? menu = null, palette = null, dwm = null, glass = null;

        try
        {
            using var accentKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");

            if (accentKey is not null)
            {
                // The colour the Settings app paints for "Accent color" (0xAABBGGRR).
                if (accentKey.GetValue("AccentColorMenu") is int menuRaw)
                {
                    var candidate = AccentDecoder.FromAbgr(unchecked((uint)menuRaw));
                    if (AccentDecoder.IsUsable(candidate))
                    {
                        menu = ToColor(candidate);
                    }
                }

                // Palette entry 3 is the base accent; each entry is R,G,B,unused.
                if (AccentDecoder.FromPalette(accentKey.GetValue("AccentPalette") as byte[]) is { } entry
                    && AccentDecoder.IsUsable(entry))
                {
                    palette = ToColor(entry);
                }
            }

            using var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (dwmKey is not null)
            {
                if (dwmKey.GetValue("AccentColor") is int accentRaw)
                {
                    var candidate = AccentDecoder.FromAbgr(unchecked((uint)accentRaw));
                    if (AccentDecoder.IsUsable(candidate))
                    {
                        dwm = ToColor(candidate);
                    }
                }

                // ColorizationColor is ARGB, not ABGR, and is a tint rather than the accent.
                if (dwmKey.GetValue("ColorizationColor") is int glassRaw)
                {
                    var candidate = AccentDecoder.FromArgb(unchecked((uint)glassRaw));
                    if (AccentDecoder.IsUsable(candidate))
                    {
                        glass = ToColor(candidate);
                    }
                }
            }
        }
        catch
        {
            // Registry access is best-effort; fall through to the defaults below.
        }

        // Trust the explicit accent, but if it disagrees wildly in hue with the glass
        // colour we probably misread a byte order somewhere - follow the glass instead.
        foreach (var candidate in new[] { menu, palette, dwm })
        {
            if (candidate is { } color && !DisagreesWith(glass, color))
            {
                return color;
            }
        }

        return menu ?? palette ?? dwm ?? glass ?? Color.FromRgb(0x00, 0x78, 0xD4);
    }

    private static Color ToColor((byte R, byte G, byte B) c) => Color.FromRgb(c.R, c.G, c.B);

    private static bool DisagreesWith(Color? reference, Color candidate) =>
        AccentDecoder.Disagrees(reference is { } r ? (r.R, r.G, r.B) : null, (candidate.R, candidate.G, candidate.B));

    private static void StartWatching()
    {
        if (_watching)
        {
            return;
        }

        _watching = true;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>
    /// Cheap re-check used when the window is activated: if Windows changed its theme or
    /// accent while we were in the background, follow it immediately.
    /// </summary>
    public static void RefreshSystemSync()
    {
        if (Theme != AppTheme.System && Accent != AccentId.System)
        {
            return;
        }

        var accent = ReadSystemAccent();
        var systemDark = IsSystemDark();
        bool accentChanged = accent != SystemAccentColor;
        bool themeChanged = Theme == AppTheme.System && systemDark != IsDark;
        if (accentChanged || themeChanged)
        {
            Apply();
        }
    }

    public static void Shutdown()
    {
        if (!_watching)
        {
            return;
        }

        _watching = false;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private static void OnDisplaySettingsChanged(object? sender, EventArgs e) => ReapplyIfSystem();

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            ReapplyIfSystem();
        }
    }

    private static void ReapplyIfSystem()
    {
        if (Theme != AppTheme.System && Accent != AccentId.System)
        {
            return;
        }

        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.Dispatcher.BeginInvoke(new Action(() => Apply()));
    }
}
