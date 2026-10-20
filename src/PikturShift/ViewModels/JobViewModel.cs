using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PikturShift.Common;
using PikturShift.Core.Imaging;

namespace PikturShift.ViewModels;

/// <summary>One row in the file queue.</summary>
public sealed class JobViewModel : ObservableObject
{
    private BitmapImage? _thumbnail;
    private ImageBrush? _thumbnailBrush;

    public JobViewModel(ConversionJob model)
    {
        Model = model;
    }

    public ConversionJob Model { get; }

    public string FileName => Model.FileName;
    public string SourcePath => Model.SourcePath;
    public string ExtensionLabel => Model.Extension;

    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        private set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>Brush form lets the Border clip the bitmap to its rounded corners.</summary>
    public ImageBrush? ThumbnailBrush
    {
        get => _thumbnailBrush;
        private set => SetProperty(ref _thumbnailBrush, value);
    }

    public bool HasThumbnail => _thumbnail is not null;

    public string SizeText => FormatBytes(Model.SizeBytes);

    public string MetaText => Model.Width > 0
        ? $"{ExtensionLabel}  \u00b7  {Model.Width} \u00d7 {Model.Height}  \u00b7  {SizeText}"
        : $"{ExtensionLabel}  \u00b7  {SizeText}";

    public string Status
    {
        get
        {
            if (Model.Error == "Cancelled")
            {
                return "cancelled";
            }

            return Model.Status;
        }
    }

    public bool IsConverting => Status == "converting";
    public bool IsDone => Status == "done";
    public bool IsFailed => Status == "failed";
    public bool IsCancelled => Status == "cancelled";
    public bool IsQueued => Status is "queued" or "reading";

    public double Progress => Model.Progress;

    public string StatusText => Status switch
    {
        "reading" => "Reading\u2026",
        "converting" => $"{Model.Progress} %",
        "done" => $"\u2192 {TargetLabel}  \u00b7  {FormatBytes(Model.OutputBytes)}",
        "failed" => "Failed",
        "cancelled" => "Cancelled",
        _ => TargetLabel
    };

    public string StatusGlyph => Status switch
    {
        "done" => "\uE73E",
        "failed" => "\uEA39",
        "cancelled" => "\uE711",
        _ => string.Empty
    };

    public bool HasStatusGlyph => !string.IsNullOrEmpty(StatusGlyph);

    public string ErrorText => Model.Error ?? string.Empty;
    public bool HasError => !string.IsNullOrEmpty(Model.Error) && Status is "failed";

    public string TargetLabel => Model.OutputPath is not null
        ? Path.GetExtension(Model.OutputPath).TrimStart('.').ToUpperInvariant()
        : CurrentTargetLabel;

    /// <summary>Set by the view model before a run so queued rows can show their destination format.</summary>
    public string CurrentTargetLabel { get; set; } = string.Empty;

    public string OutputPath => Model.OutputPath ?? string.Empty;

    public void Refresh()
    {
        OnPropertiesChanged(
            nameof(Status), nameof(IsConverting), nameof(IsDone), nameof(IsFailed), nameof(IsCancelled),
            nameof(IsQueued), nameof(Progress), nameof(StatusText), nameof(StatusGlyph), nameof(HasStatusGlyph),
            nameof(ErrorText), nameof(HasError), nameof(MetaText), nameof(SizeText), nameof(TargetLabel),
            nameof(OutputPath));
    }

    public void ApplyProbe(ImageProbe probe)
    {
        if (probe.Success)
        {
            Model.Width = probe.Width;
            Model.Height = probe.Height;
            Model.SizeBytes = probe.Bytes;
        }

        OnPropertiesChanged(nameof(MetaText), nameof(SizeText));
    }

    /// <summary>Attaches a thumbnail that was decoded (and frozen) off the UI thread.</summary>
    public void AttachThumbnail(BitmapImage? image)
    {
        if (image is null)
        {
            return;
        }

        try
        {
            Thumbnail = image;
            var brush = new ImageBrush(image)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            brush.Freeze();
            ThumbnailBrush = brush;
            OnPropertiesChanged(nameof(HasThumbnail), nameof(ThumbnailBrush));
        }
        catch
        {
            // A broken thumbnail must never break the queue.
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "\u2014";
        }

        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} B" : $"{value:0.#} {units[unit]}";
    }
}
