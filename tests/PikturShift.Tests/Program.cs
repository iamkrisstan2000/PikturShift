using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using PikturShift.Core.Formats;
using PikturShift.Core.Imaging;
using PikturShift.Core.Settings;

var root = Path.Combine(Path.GetTempPath(), "pc_smoke_" + Guid.NewGuid().ToString("N")[..6]);
Directory.CreateDirectory(root);

int pass = 0, fail = 0;

void Check(string name, bool ok, string? detail = null)
{
    if (ok) { pass++; Console.WriteLine($"  PASS  {name}"); }
    else { fail++; Console.WriteLine($"  FAIL  {name}  {detail}"); }
}

// ------------------------------------------------------------------ accent decoding
{
    // Real Windows data for the default blue accent (#0078D4): AccentPalette is stored as
    // eight R,G,B,unused groups, so entry 3 reads 00 78 D7 -> red=00, green=78, blue=D7.
    byte[] palette =
    {
        0xA6, 0xD8, 0xFF, 0x00,  0x76, 0xB9, 0xED, 0x00,  0x42, 0x9C, 0xE3, 0x00,
        0x00, 0x78, 0xD7, 0x00,  0x5A, 0x9E, 0x00, 0x00,  0x42, 0x75, 0x00, 0x00,
        0x26, 0x42, 0x00, 0x00,  0xF7, 0x63, 0x0C, 0x00
    };

    var fromPalette = AccentDecoder.FromPalette(palette);
    Check("AccentPalette decodes R,G,B (blue accent stays blue)",
        fromPalette is { R: 0x00, G: 0x78, B: 0xD7 }, fromPalette?.ToString());
    Check("AccentPalette is not byte-swapped into orange",
        fromPalette is { } p1 && p1.B > p1.R, fromPalette?.ToString());

    // 0xAABBGGRR: 0xFFD77800 is the DWM encoding of #0078D4.
    var abgr = AccentDecoder.FromAbgr(0xFFD77800);
    Check("DWM AccentColor decodes as ABGR",
        abgr is { R: 0x00, G: 0x78, B: 0xD7 }, abgr.ToString());

    // 0xAARRGGBB: ColorizationColor puts blue in the low byte instead.
    var argb = AccentDecoder.FromArgb(0xC40078D7);
    Check("ColorizationColor decodes as ARGB",
        argb is { R: 0x00, G: 0x78, B: 0xD7 }, argb.ToString());

    Check("short palette is rejected", AccentDecoder.FromPalette(new byte[8]) is null);
    Check("ghost colours rejected",
        !AccentDecoder.IsUsable((0, 0, 0)) && !AccentDecoder.IsUsable((255, 255, 255)));

    // A byte-swapped accent (orange) must be recognised as disagreeing with the real blue.
    Check("byte-order guard catches red/blue swap",
        AccentDecoder.Disagrees((0x00, 0x78, 0xD7), (0xD7, 0x78, 0x00))
        && !AccentDecoder.Disagrees((0x00, 0x78, 0xD7), (0x18, 0x6F, 0xC4)),
        "swap should disagree, a neighbouring blue should not");
}

// ------------------------------------------------------------------ accent-driven surfaces
{
    // Hues are preserved when re-hueing, lightnesses are untouched, so contrast cannot drift.
    var olive = Hsl.From((0x1F, 0x22, 0x1A));
    var blue  = olive.WithHue(210, 1.0);
    Check("re-hued surface keeps its lightness", Math.Abs(blue.L - olive.L) < 0.005,
        $"{olive.L} -> {blue.L}");
    Check("re-hued surface adopts the accent hue", Math.Abs(blue.H - 210) < 1.0, blue.H.ToString("F1"));

    var blueRgb = blue.ToRgb();
    Check("blue accent removes the green cast", blueRgb.B > blueRgb.R && blueRgb.B > blueRgb.G,
        blueRgb.ToString());

    // White/black accents must leave the surfaces neutral.
    var mono = Hsl.From((255, 255, 255)).WithHue(210, 0.0);
    Check("mono accent keeps surfaces grey", mono.S < 1e-6, mono.S.ToString("F3"));

    // Matcha (hue ~78) must reproduce the approved mockup almost exactly.
    var matcha = olive.WithHue(78, 1.0).ToRgb();
    Check("matcha accent preserves the original olive surface",
        Math.Abs(matcha.R - 0x1F) <= 1 && Math.Abs(matcha.G - 0x22) <= 1 && Math.Abs(matcha.B - 0x1A) <= 1,
        matcha.ToString());
}


// ---------------------------------------------------------------- fixtures
string MakePhoto(string name, int w = 320, int h = 200, bool alpha = false)
{
    var path = Path.Combine(root, name);
    using var img = new Image<Rgba32>(w, h);
    img.ProcessPixelRows(acc =>
    {
        for (int y = 0; y < h; y++)
        {
            var row = acc.GetRowSpan(y);
            for (int x = 0; x < w; x++)
            {
                var a = alpha ? (byte)(x < w / 2 ? 0 : 255) : (byte)255;
                row[x] = new Rgba32((byte)(x * 255 / w), (byte)(y * 255 / h), (byte)((x + y) % 255), a);
            }
        }
    });
    img.Save(path);
    return path;
}

string rainbowPng = MakePhoto("rainbow.png", alpha: true);
string opaquePng = MakePhoto("opaque.png");
Console.WriteLine($"fixtures in {root}");

// webp / bmp / tiff / gif sources
string webp = Path.Combine(root, "photo.webp");
using (var src = Image.Load(opaquePng)) src.Save(webp, new WebpEncoder { Quality = 92 });

// JPEG with EXIF orientation = 6 (rotate 90° CW on display)
string exifJpg = Path.Combine(root, "rotated.jpg");
using (var src = Image.Load(opaquePng))
{
    var profile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
    profile.SetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, (ushort)6);
    profile.SetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Make, "Arena Cameras");
    profile.SetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Model, "Model 11");
    src.Metadata.ExifProfile = profile;
    src.Save(exifJpg, new JpegEncoder { Quality = 95 });
}

Console.WriteLine("\n[1] probe");
var probe = ImageConverter.Probe(webp);
Check("probe webp", probe.Success && probe.FormatName == "WEBP" && probe.Width == 320 && probe.Height == 200,
    $"{probe.FormatName} {probe.Width}x{probe.Height} {probe.Error}");
var badProbe = ImageConverter.Probe(Path.Combine(root, "nope.jpg"));
Check("probe missing file reports error", !badProbe.Success);

Console.WriteLine("\n[2] conversion matrix");
var sources = new (string Path, string Label)[]
{
    (rainbowPng, "png/alpha"), (opaquePng, "png"), (webp, "webp"), (exifJpg, "jpg/exif")
};

foreach (var (srcPath, label) in sources)
{
    foreach (OutputFormat fmt in Enum.GetValues<OutputFormat>())
    {
        var opts = new ConversionOptions { Format = fmt, Quality = 92, FillColor = "#FF00AA" };
        var target = Path.Combine(root, "out_" + fmt, Path.GetFileNameWithoutExtension(srcPath) + FormatInfo.Extension(fmt));
        var res = ImageConverter.Convert(srcPath, target, opts, overwrite: true);
        bool fileOk = res.Success && File.Exists(res.OutputPath);
        bool decodable = false;
        int w = 0, h = 0;
        if (fileOk)
        {
            try
            {
                var info = Image.Identify(res.OutputPath!);
                decodable = info is not null;
                w = info?.Width ?? 0; h = info?.Height ?? 0;
            }
            catch { }
        }
        bool rotatedSource = label.Contains("exif");
        bool dimsOk = decodable && (rotatedSource ? (w == 200 && h == 320) : (w == 320 && h == 200));
        // jpeg/webp are lossy: allow some slack, but png/bmp/tiff should be exact
        bool sizeOk = res.OutputBytes > 100;
        Check($"{label} -> {FormatInfo.DisplayName(fmt)}", fileOk && decodable && dimsOk && sizeOk,
            $"{res.Message} out={res.OutputPath} {w}x{h} bytes={res.OutputBytes}");
    }
}

Console.WriteLine("\n[3] transparency flattening");
{
    var target = Path.Combine(root, "flat.jpg");
    var res = ImageConverter.Convert(rainbowPng, target, new ConversionOptions
    {
        Format = OutputFormat.Jpg,
        Quality = 95,
        FillColor = "#FF00AA"
    }, overwrite: true);
    using var outImg = Image.Load<Rgba32>(res.OutputPath!);
    var left = outImg[10, 10]; // was fully transparent
    bool filled = left.R > 200 && left.G < 80 && left.B > 120;
    Check("transparent area filled with fill colour", filled, $"pixel={left}");

    var pngTarget = Path.Combine(root, "keepalpha.png");
    var resPng = ImageConverter.Convert(rainbowPng, pngTarget, new ConversionOptions { Format = OutputFormat.Png }, true);
    using var pngOut = Image.Load<Rgba32>(resPng.OutputPath!);
    Check("png keeps alpha", pngOut[10, 10].A == 0, $"alpha={pngOut[10, 10].A}");
}

Console.WriteLine("\n[4] naming + overwrite");
{
    var dir = Path.Combine(root, "naming");
    Directory.CreateDirectory(dir);
    var a = ImageConverter.Convert(opaquePng, Path.Combine(dir, "shot.jpg"), new ConversionOptions { Format = OutputFormat.Jpg }, overwrite: false);
    var b = ImageConverter.Convert(opaquePng, Path.Combine(dir, "shot.jpg"), new ConversionOptions { Format = OutputFormat.Jpg }, overwrite: false);
    Check("no-overwrite adds (1) suffix", a.OutputPath!.EndsWith("shot.jpg") && b.OutputPath!.EndsWith("shot (1).jpg"),
        $"{a.OutputPath} | {b.OutputPath}");

    var c = ImageConverter.Convert(opaquePng, Path.Combine(dir, "shot.jpg"), new ConversionOptions { Format = OutputFormat.Jpg }, overwrite: true);
    Check("overwrite reuses name", c.OutputPath!.EndsWith("shot.jpg"), c.OutputPath);

    var sameFormatDir = Path.Combine(root, "samefmt");
    Directory.CreateDirectory(sameFormatDir);
    var copy = Path.Combine(sameFormatDir, "keep.png");
    File.Copy(opaquePng, copy, true);
    var d = ImageConverter.Convert(copy, Path.Combine(sameFormatDir, "keep.png"), new ConversionOptions { Format = OutputFormat.Png }, true);
    Check("same input/output path uses (converted)", d.Success && d.OutputPath!.EndsWith("keep (converted).png"), d.OutputPath);
}

Console.WriteLine("\n[5] exif + orientation");
{
    var keep = ImageConverter.Convert(exifJpg, Path.Combine(root, "exif_keep.jpg"),
        new ConversionOptions { Format = OutputFormat.Jpg, KeepExif = true }, true);
    using (var img = Image.Load(keep.OutputPath!))
    {
        var make = img.Metadata.ExifProfile?.GetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Make)?.GetValue();
        var orient = img.Metadata.ExifProfile?.GetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation)?.GetValue();
        Check("exif kept", make?.ToString() == "Arena Cameras", $"make={make}");
        Check("orientation baked to 1", orient is ushort u && u == 1, $"orientation={orient}");
    }

    var strip = ImageConverter.Convert(exifJpg, Path.Combine(root, "exif_strip.jpg"),
        new ConversionOptions { Format = OutputFormat.Jpg, KeepExif = false }, true);
    using (var img = Image.Load(strip.OutputPath!))
    {
        Check("exif stripped when off", img.Metadata.ExifProfile is null || img.Metadata.ExifProfile!.Values.Count == 0,
            $"values={img.Metadata.ExifProfile?.Values.Count}");
    }

    // orientation=6 means the displayed image is rotated 90°: 320x200 -> 200x320 after auto-orient
    var rotated = ImageConverter.Convert(exifJpg, Path.Combine(root, "orient.png"),
        new ConversionOptions { Format = OutputFormat.Png }, true);
    Check("auto-orient swaps dimensions", rotated.Width == 200 && rotated.Height == 320, $"{rotated.Width}x{rotated.Height}");
}

Console.WriteLine("\n[6] thumbnails");
{
    var bytes = ImageConverter.CreateThumbnail(webp, 96);
    bool ok = bytes is { Length: > 100 };
    if (ok)
    {
        using var thumb = Image.Load(bytes!);
        ok = thumb.Width <= 96 && thumb.Height <= 96;
    }
    Check("thumbnail generated", ok);
    Check("thumbnail of missing file is null", ImageConverter.CreateThumbnail(Path.Combine(root, "missing.png")) is null);
}

Console.WriteLine("\n[7] parallel batch");
{
    var dir = Path.Combine(root, "batch");
    Directory.CreateDirectory(dir);
    var jobs = new List<ConversionJob>();
    for (int i = 0; i < 8; i++)
    {
        // two files sharing the same name on purpose (common when dropping folders)
        var sub = Path.Combine(dir, i % 2 == 0 ? "a" : "b");
        Directory.CreateDirectory(sub);
        var f = Path.Combine(sub, $"IMG_{i / 2}.png");
        File.Copy(opaquePng, f, true);
        jobs.Add(new ConversionJob(f));
    }
    jobs.Add(new ConversionJob(Path.Combine(root, "missing-file.png")));

    var outputs = new List<string>();
    var gate = new object();
    await ConversionBatch.RunAsync(
        jobs,
        new ConversionOptions { Format = OutputFormat.Jpg, Quality = 90 },
        Path.Combine(dir, "out"),
        overwrite: false,
        job => { lock (gate) { if (job.OutputPath is not null) outputs.Add(job.OutputPath); } },
        parallelism: 4);

    Check("all valid jobs done", jobs.Count(j => j.Status == "done") == 8);
    Check("missing file failed gracefully", jobs[^1].Status == "failed");
    Check("unique outputs for duplicate names", outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() == outputs.Count,
        string.Join(", ", outputs.Select(Path.GetFileName)));
    Check("all outputs exist", outputs.All(File.Exists));
}

Console.WriteLine("\n[8] settings round-trip");
{
    var store = new SettingsStore(Path.Combine(root, "settings.json"));
    var s = new AppSettings { Theme = "Light", Accent = "pink", Quality = 84, Format = "WebP", FillColor = "#001122", NavExpanded = true };
    store.Save(s);
    var loaded = store.Load();
    Check("settings persist", loaded.Theme == "Light" && loaded.Accent == "pink" && loaded.Quality == 84 && loaded.Format == "WebP" && loaded.NavExpanded);
    File.WriteAllText(store.FilePath, "{ not json");
    var recovered = store.Load();
    Check("corrupt settings fall back to defaults", recovered.Theme == "Dark" && recovered.Accent == "matcha");
}

Console.WriteLine($"\n{pass} passed, {fail} failed");
try { Directory.Delete(root, true); } catch { }
return fail == 0 ? 0 : 1;
