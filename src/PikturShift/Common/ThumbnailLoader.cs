using System.Windows.Media.Imaging;

namespace PikturShift.Common;

/// <summary>
/// Builds row thumbnails with WIC, which can subsample while decoding. The longest edge is
/// capped to 64 px for 40 px queue tiles, limiting retained thumbnail memory on large batches.
/// </summary>
internal static class ThumbnailLoader
{
    /// <summary>Decodes a frozen thumbnail; safe to call from a pool thread.</summary>
    public static BitmapImage? Load(string path, int sourceWidth, int sourceHeight, int maxEdge = 64)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
            // Set only the longer source dimension so WIC preserves aspect ratio while
            // bounding the decoded bitmap's longest edge.
            if (sourceHeight > sourceWidth)
            {
                image.DecodePixelHeight = maxEdge;
            }
            else
            {
                image.DecodePixelWidth = maxEdge;
            }

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
