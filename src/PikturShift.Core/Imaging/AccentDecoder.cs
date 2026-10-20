namespace PikturShift.Core.Imaging;

/// <summary>
/// Decodes the colours Windows keeps in the registry. The value formats differ per key and
/// getting them wrong silently swaps red and blue, so the decoding lives here, away from the
/// UI, where it can be unit tested:
///
///   Explorer\Accent\AccentPalette   REG_BINARY, 8 groups of 4 bytes - <b>R, G, B, unused</b>
///   Explorer\Accent\AccentColorMenu DWORD, 0xAABBGGRR
///   DWM\AccentColor                 DWORD, 0xAABBGGRR
///   DWM\ColorizationColor           DWORD, 0xAARRGGBB  (a tint, not the accent)
/// </summary>
public static class AccentDecoder
{
    /// <summary>The base accent from AccentPalette: entry 3, bytes in R,G,B order.</summary>
    public static (byte R, byte G, byte B)? FromPalette(byte[]? palette)
    {
        if (palette is null || palette.Length < 16)
        {
            return null;
        }

        int i = 3 * 4;
        return (palette[i], palette[i + 1], palette[i + 2]);
    }

    /// <summary>DWORDs stored as 0xAABBGGRR (red in the low byte).</summary>
    public static (byte R, byte G, byte B) FromAbgr(uint value) =>
        ((byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF));

    /// <summary>DWORDs stored as 0xAARRGGBB (blue in the low byte).</summary>
    public static (byte R, byte G, byte B) FromArgb(uint value) =>
        ((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));

    /// <summary>Rejects near-black / near-white entries, which are usually unset ghosts.</summary>
    public static bool IsUsable((byte R, byte G, byte B) c) => c.R + c.G + c.B is > 24 and < 726;

    /// <summary>Hue in degrees (0-360).</summary>
    public static double Hue((byte R, byte G, byte B) c) => Hsl.From(c).H;

    /// <summary>
    /// True when the two colours are both vivid enough to be compared but sit on opposite
    /// sides of the colour wheel - the signature of a byte-order mistake.
    /// </summary>
    public static bool Disagrees((byte R, byte G, byte B)? reference, (byte R, byte G, byte B) candidate)
    {
        if (reference is not { } other)
        {
            return false;
        }

        var a = Hsl.From(candidate);
        var b = Hsl.From(other);
        if (a.S < 0.18 || b.S < 0.18)
        {
            return false;
        }

        double delta = Math.Abs(a.H - b.H);
        delta = Math.Min(delta, 360 - delta);
        return delta > 100;
    }
}

/// <summary>Lightweight HSL triple used for accent validation and surface tinting.</summary>
public readonly record struct Hsl(double H, double S, double L)
{
    public static Hsl From((byte R, byte G, byte B) c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;
        double d = max - min;

        if (d < 1e-9)
        {
            return new Hsl(0, 0, l);
        }

        double sat = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0)
                 : max == g ? (b - r) / d + 2
                 : (r - g) / d + 4;
        return new Hsl(h * 60.0, sat, l);
    }

    public Hsl WithHue(double hue, double saturationScale) =>
        new(hue, Math.Clamp(S * saturationScale, 0, 1), L);

    public (byte R, byte G, byte B) ToRgb()
    {
        if (S < 1e-9)
        {
            byte v = (byte)Math.Round(Math.Clamp(L, 0, 1) * 255);
            return (v, v, v);
        }

        double h = ((H % 360) + 360) % 360 / 360.0;
        double q = L < 0.5 ? L * (1 + S) : L + S - L * S;
        double p = 2 * L - q;

        static double Channel(double p, double q, double t)
        {
            t = t < 0 ? t + 1 : t > 1 ? t - 1 : t;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        return ((byte)Math.Round(Channel(p, q, h + 1.0 / 3) * 255),
                (byte)Math.Round(Channel(p, q, h) * 255),
                (byte)Math.Round(Channel(p, q, h - 1.0 / 3) * 255));
    }
}
