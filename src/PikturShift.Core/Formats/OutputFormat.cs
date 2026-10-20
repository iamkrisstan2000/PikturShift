namespace PikturShift.Core.Formats;

/// <summary>Output formats supported by the converter.</summary>
public enum OutputFormat
{
    Jpg,
    Png,
    WebP,
    Bmp,
    Tiff
}

/// <summary>Static capability + presentation metadata for each output format.</summary>
public static class FormatInfo
{
    public const string AllInputExtensions =
        ".jpg,.jpeg,.jpe,.jfif,.png,.webp,.bmp,.dib,.gif,.tif,.tiff";

    public static string Extension(OutputFormat format) => format switch
    {
        OutputFormat.Jpg => ".jpg",
        OutputFormat.Png => ".png",
        OutputFormat.WebP => ".webp",
        OutputFormat.Bmp => ".bmp",
        OutputFormat.Tiff => ".tif",
        _ => ".img"
    };

    public static string DisplayName(OutputFormat format) => format switch
    {
        OutputFormat.Jpg => "JPG",
        OutputFormat.Png => "PNG",
        OutputFormat.WebP => "WebP",
        OutputFormat.Bmp => "BMP",
        OutputFormat.Tiff => "TIFF",
        _ => format.ToString()
    };

    /// <summary>Short, honest hint shown under the format picker.</summary>
    public static string Hint(OutputFormat format) => format switch
    {
        OutputFormat.Jpg => "Best for photos \u00b7 fills transparency",
        OutputFormat.Png => "Lossless \u00b7 keeps transparency",
        OutputFormat.WebP => "Small web files \u00b7 keeps transparency",
        OutputFormat.Bmp => "Uncompressed \u00b7 fills transparency",
        OutputFormat.Tiff => "Archival \u00b7 keeps transparency",
        _ => string.Empty
    };

    public static bool SupportsAlpha(OutputFormat format) => format is OutputFormat.Png or OutputFormat.WebP or OutputFormat.Tiff;

    public static bool SupportsQuality(OutputFormat format) => format is OutputFormat.Jpg or OutputFormat.WebP;

    public static bool SupportsExif(OutputFormat format) => format is OutputFormat.Jpg or OutputFormat.Tiff;

    public static string FromExtension(string? extension)
    {
        switch ((extension ?? string.Empty).Trim().ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
            case ".jpe":
            case ".jfif":
                return "JPG";
            case ".png":
                return "PNG";
            case ".webp":
                return "WebP";
            case ".bmp":
            case ".dib":
                return "BMP";
            case ".gif":
                return "GIF";
            case ".tif":
            case ".tiff":
                return "TIFF";
            default:
                return string.IsNullOrEmpty(extension) ? "IMG" : extension.TrimStart('.').ToUpperInvariant();
        }
    }

    public static bool IsSupportedInput(string path) =>
        AllInputExtensions.Contains(Path.GetExtension(path).ToLowerInvariant(), StringComparison.Ordinal);

}
