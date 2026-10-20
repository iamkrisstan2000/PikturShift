using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;
using PikturShift.Core.Formats;

namespace PikturShift.Core.Imaging;

/// <summary>Options for a single conversion.</summary>
public sealed class ConversionOptions
{
    public OutputFormat Format { get; init; } = OutputFormat.Jpg;

    /// <summary>1-100, used by JPG and WebP.</summary>
    public int Quality { get; init; } = 100;

    /// <summary>#RRGGBB colour used to flatten transparency for formats without alpha.</summary>
    public string FillColor { get; init; } = "#FFFFFF";

    public bool KeepExif { get; init; }

    /// <summary>Strip EXIF orientation but bake the rotation into the pixels (always on).</summary>
    public bool AutoOrient { get; init; } = true;
}

public sealed class ConversionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? OutputPath { get; init; }
    public long InputBytes { get; init; }
    public long OutputBytes { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class ImageProbe
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string FormatName { get; init; } = string.Empty;
    public long Bytes { get; init; }
}

/// <summary>
/// Pure, UI-free conversion engine. Uses ImageSharp (managed, SIMD accelerated) so it ships
/// with the app, needs no external codecs and handles JPG / PNG / WebP / BMP / TIFF / GIF.
/// </summary>
public static class ImageConverter
{
    private const int MaxSuffix = 9999;

    static ImageConverter()
    {
        // One image is processed sequentially; the batch layer supplies the parallelism.
        // ImageSharp splits tiles across cores when this is raised, which multiplies the
        // peak buffer size per image - the main reason a "few files at once" batch could
        // balloon to hundreds of MB. Batch workers give the same throughput at a
        // predictable, per-file memory cost.
        Configuration.Default.MaxDegreeOfParallelism = 1;

        // Reuse decode/encode buffers instead of handing every allocation to the GC.
        // Capping the pool keeps the process lean while still avoiding Large Object Heap
        // churn on repeated conversions.
        Configuration.Default.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            MaximumPoolSizeMegabytes = 64
        });
    }

    // ----------------------------------------------------------------- probes

    public static ImageProbe Probe(string path)
    {
        try
        {
            var info = Image.Identify(path);
            if (info is null)
            {
                return new ImageProbe { Success = false, Error = "Not a readable image" };
            }

            var decoded = Image.DetectFormat(path);
            string format = decoded?.Name ?? FormatInfo.FromExtension(Path.GetExtension(path));
            if (string.Equals(format, "JPEG", StringComparison.OrdinalIgnoreCase))
            {
                format = "JPG";
            }

            return new ImageProbe
            {
                Success = true,
                Width = info.Width,
                Height = info.Height,
                FormatName = format.ToUpperInvariant(),
                Bytes = new FileInfo(path).Length
            };
        }
        catch (Exception ex)
        {
            return new ImageProbe { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Creates a small PNG thumbnail. Kept for the engine tests and for callers without a UI
    /// toolkit; ImageSharp 2.1 has no decoder-side downscaling, so the UI uses WIC instead
    /// (see <c>JobViewModel.SetThumbnail</c>), which scales JPEGs while decoding.
    /// </summary>
    public static byte[]? CreateThumbnail(string path, int maxEdge = 240)
    {
        try
        {
            using var image = Image.Load(path);
            image.Mutate(ctx => ctx.AutoOrient().Resize(new ResizeOptions
            {
                Size = new Size(maxEdge, maxEdge),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));

            using var ms = new MemoryStream();
            image.SaveAsPng(ms, new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestSpeed
            });
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------- conversion

    /// <summary>
    /// Converts <paramref name="inputPath"/> into <paramref name="requestedOutputPath"/>.
    /// The file is written to a temp file first and then moved, so a crash never leaves a
    /// half-written image behind. If the destination exists and <paramref name="overwrite"/>
    /// is false a unique "(1)", "(2)" … suffix is chosen.
    /// </summary>
    public static ConversionResult Convert(
        string inputPath,
        string requestedOutputPath,
        ConversionOptions options,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        long inputBytes = 0;

        try
        {
            if (!File.Exists(inputPath))
            {
                return new ConversionResult { Success = false, Message = "File not found" };
            }

            inputBytes = new FileInfo(inputPath).Length;

            using var image = Image.Load(inputPath);
            cancellationToken.ThrowIfCancellationRequested();

            if (options.AutoOrient)
            {
                // Rotates the pixels and clears the orientation tag so viewers agree with us.
                image.Mutate(ctx => ctx.AutoOrient());
            }

            // Metadata is written by the encoder straight from image.Metadata, so there is
            // never a second re-encode (which would degrade JPEG/WebP quality).
            if (!options.KeepExif || !FormatInfo.SupportsExif(options.Format))
            {
                image.Metadata.ExifProfile = null;
                image.Metadata.XmpProfile = null;
                if (options.Format == OutputFormat.Png)
                {
                    // PNG stores text chunks; drop them too so "keep EXIF off" stays honest.
                    image.Metadata.XmpProfile = null;
                }
            }
            else if (image.Metadata.ExifProfile is not null)
            {
                foreach (var value in image.Metadata.ExifProfile.Values)
                {
                    if (value.Tag == ExifTag.Orientation)
                    {
                        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)1);
                        break;
                    }
                }
            }

            if (!FormatInfo.SupportsAlpha(options.Format))
            {
                var fill = ParseColor(options.FillColor);
                image.Mutate(ctx => ctx.BackgroundColor(fill));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var outputPath = EnsureUniquePath(inputPath, requestedOutputPath, overwrite);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = Path.Combine(
                string.IsNullOrEmpty(directory) ? "." : directory,
                $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
                {
                    var encoder = CreateEncoder(options);
                    image.Save(stream, encoder);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(tempPath, outputPath, overwrite: true);
            }
            finally
            {
                TryDelete(tempPath);
            }

            started.Stop();
            var outInfo = new FileInfo(outputPath);
            return new ConversionResult
            {
                Success = true,
                Message = "Converted",
                OutputPath = outputPath,
                InputBytes = inputBytes,
                OutputBytes = outInfo.Exists ? outInfo.Length : 0,
                Width = image.Width,
                Height = image.Height,
                Duration = started.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return new ConversionResult { Success = false, Message = "Cancelled", InputBytes = inputBytes };
        }
        catch (UnknownImageFormatException)
        {
            return new ConversionResult { Success = false, Message = "Unsupported or damaged image", InputBytes = inputBytes };
        }
        catch (InvalidImageContentException)
        {
            return new ConversionResult { Success = false, Message = "Damaged image data", InputBytes = inputBytes };
        }
        catch (Exception ex)
        {
            return new ConversionResult { Success = false, Message = Shorten(ex.Message), InputBytes = inputBytes };
        }
    }

    private static IImageEncoder CreateEncoder(ConversionOptions options)
    {
        int quality = Math.Clamp(options.Quality, 1, 100);
        return options.Format switch
        {
            OutputFormat.Jpg => new JpegEncoder
            {
                Quality = quality
            },
            OutputFormat.Png => new PngEncoder
            {
                CompressionLevel = quality >= 95 ? PngCompressionLevel.Level4 : PngCompressionLevel.Level6,
                FilterMethod = PngFilterMethod.Adaptive
            },
            OutputFormat.WebP => new WebpEncoder
            {
                FileFormat = WebpFileFormatType.Lossy,
                Quality = quality,
                Method = WebpEncodingMethod.BestQuality,
                UseAlphaCompression = true
            },
            OutputFormat.Bmp => new BmpEncoder
            {
                BitsPerPixel = BmpBitsPerPixel.Pixel24
            },
            OutputFormat.Tiff => new TiffEncoder
            {
                Compression = TiffCompression.Deflate
            },
            _ => new JpegEncoder { Quality = quality }
        };
    }

    // ----------------------------------------------------------------- paths

    /// <summary>Returns a path that will not clobber an existing file unless overwrite is allowed.</summary>
    public static string EnsureUniquePath(string inputPath, string requestedOutputPath, bool overwrite)
    {
        var full = Path.GetFullPath(requestedOutputPath);

        if (string.Equals(full, Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(full)!;
            var stem = Path.GetFileNameWithoutExtension(full);
            var ext = Path.GetExtension(full);
            full = Path.Combine(dir, stem + " (converted)" + ext);
        }

        if (overwrite || !File.Exists(full))
        {
            return full;
        }

        var directory = Path.GetDirectoryName(full)!;
        var baseName = Path.GetFileNameWithoutExtension(full);
        var extension = Path.GetExtension(full);
        for (int i = 1; i <= MaxSuffix; i++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{baseName} ({Guid.NewGuid():N}){extension}");
    }

    // --------------------------------------------------------------- helpers

    public static Color ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Color.White;
        }

        var text = hex.Trim();
        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        return Color.TryParseHex(text, out var color) ? color : Color.White;
    }

    public static string ToHex(Color color) => color.ToHex();

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static string Shorten(string message) =>
        message.Length <= 160 ? message : message[..157] + "\u2026";
}
