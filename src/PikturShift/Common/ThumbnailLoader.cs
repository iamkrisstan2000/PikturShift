using System.Windows.Media.Imaging;

namespace PikturShift.Common;

/// <summary>
/// Builds row thumbnails with WIC, which can subsample while decoding. That keeps a 24 MP
/// photo from turning into a 96 MB pixel buffer just to paint a 38 px icon - the main reason
/// adding a few hundred files could spike memory.
/// </summary>
internal static class ThumbnailLoader
{
    /// <summary>Decodes a frozen thumbnail; safe to call from a pool thread.</summary>
    public static BitmapImage? Load(string path, int decodeWidth = 96)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
            image.DecodePixelWidth = decodeWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
