using PikturShift.Core.Formats;

namespace PikturShift.Core.Imaging;

/// <summary>
/// Decides where each converted file goes. Keeps a reservation set so that a whole batch
/// dropped at once never writes two images onto the same path, even when converting in parallel.
/// </summary>
public sealed class OutputPathPlanner
{
    private readonly object _gate = new();
    private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);

    public string Plan(string inputPath, string? folder, OutputFormat format, bool overwrite)
    {
        var directory = string.IsNullOrWhiteSpace(folder)
            ? Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory()
            : folder!;

        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var extension = FormatInfo.Extension(format);
        var candidate = Path.GetFullPath(Path.Combine(directory, stem + extension));

        lock (_gate)
        {
            if (string.Equals(candidate, Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.Combine(directory, stem + " (converted)" + extension);
            }

            // Reservations take priority over File.Exists: in a parallel batch the file
            // may not be on disk yet even though another worker already claimed the name.
            if (!overwrite || _reserved.Contains(candidate) || File.Exists(candidate))
            {
                if (!overwrite)
                {
                    candidate = NextFree(candidate);
                }
                else if (_reserved.Contains(candidate))
                {
                    // Overwrite mode still avoids two queued files fighting for one name.
                    candidate = NextFree(candidate);
                }
            }

            _reserved.Add(candidate);
            return candidate;
        }
    }

    public void Release(string path)
    {
        lock (_gate)
        {
            _reserved.Remove(path);
        }
    }

    private string NextFree(string path)
    {
        if (!File.Exists(path) && !_reserved.Contains(path))
        {
            return path;
        }

        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate) && !_reserved.Contains(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{stem} ({Guid.NewGuid():N}){ext}");
    }
}

/// <summary>A single queued file with live status, used by both the UI and headless runs.</summary>
public sealed class ConversionJob
{
    public ConversionJob(string sourcePath)
    {
        SourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        Extension = Path.GetExtension(sourcePath).TrimStart('.').ToUpperInvariant();
        SizeBytes = SafeLength(sourcePath);
        Progress = 0;
    }

    public string SourcePath { get; }
    public string FileName { get; }
    public string Extension { get; }
    public long SizeBytes { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
    public string? OutputPath { get; set; }
    public long OutputBytes { get; set; }
    public int Progress { get; set; }
    public string Status { get; set; } = "queued";
    public string? Error { get; set; }

    public bool IsDone => Status is "done" or "failed" or "skipped";

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }
}
