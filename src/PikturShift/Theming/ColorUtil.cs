using System.Windows.Media;
using PikturShift.Core.Imaging;

namespace PikturShift.Theming;

internal static class ColorUtil
{
    /// <summary>Bridges the UI-free HSL helper in the engine to WPF colours.</summary>
    public static Hsl From(Color c) => Core.Imaging.Hsl.From((c.R, c.G, c.B));

    public static Color WithHue(this Color c, double hue, double saturationScale)
    {
        var rgb = From(c).WithHue(hue, saturationScale).ToRgb();
        return Color.FromArgb(c.A, rgb.R, rgb.G, rgb.B);
    }

    /// <summary>Hue of a colour in degrees, with saturation as a 0..1 measure of how vivid it is.</summary>
    public static double Hue(Color c) => From(c).H;

    public static Color FromHex(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Magenta;
        }
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Relative luminance (0 = black, 1 = white), used to pick readable foregrounds.</summary>
    public static double Luminance(Color c)
    {
        static double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    public static Color ContrastForeground(Color background) =>
        Luminance(background) > 0.42 ? FromHex("#111310") : Colors.White;

    public static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    public static Color Mix(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * amount),
            (byte)(a.R + (b.R - a.R) * amount),
            (byte)(a.G + (b.G - a.G) * amount),
            (byte)(a.B + (b.B - a.B) * amount));
    }

    public static Color Lighten(Color c, double amount) => Mix(c, Colors.White, amount);

    public static Color Darken(Color c, double amount) => Mix(c, Colors.Black, amount);

    public static Color EnsureReadableOnLight(Color c)
    {
        // Very light accents (yellow, pastel beige, white) vanish on a white surface.
        int guard = 0;
        while (Luminance(c) > 0.58 && guard++ < 12)
        {
            c = Darken(c, 0.09);
        }

        return c;
    }

    public static Color EnsureReadableOnDark(Color c)
    {
        int guard = 0;
        while (Luminance(c) < 0.22 && guard++ < 12)
        {
            c = Lighten(c, 0.09);
        }

        return c;
    }
}
