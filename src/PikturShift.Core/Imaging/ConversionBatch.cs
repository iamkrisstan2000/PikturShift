
namespace PikturShift.Core.Imaging;

/// <summary>
/// Runs a queue of conversion jobs in parallel with a bounded degree of concurrency.
/// Used by the desktop app; also usable headless (see --cli in the app, handy for testing).
/// </summary>
public static class ConversionBatch
{
    public static int DefaultParallelism => Math.Clamp(Environment.ProcessorCount, 1, 6);

    /// <summary>
    /// Converts all jobs. <paramref name="onProgress"/> is marshalled by the caller;
    /// this method reports raw updates from worker threads.
    /// </summary>
    public static async Task RunAsync(
        IReadOnlyList<ConversionJob> jobs,
        ConversionOptions options,
        string? outputFolder,
        bool overwrite,
        Action<ConversionJob>? onProgress = null,
        int parallelism = 0,
        CancellationToken cancellationToken = default)
    {
        var planner = new OutputPathPlanner();
        int degree = parallelism <= 0 ? DefaultParallelism : parallelism;
        using var throttle = new SemaphoreSlim(degree, degree);

        var tasks = jobs.Select(async job =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                job.Status = "reading";
                job.Progress = 8;
                onProgress?.Invoke(job);

                var probe = ImageConverter.Probe(job.SourcePath);
                if (!probe.Success)
                {
                    job.Status = "failed";
                    job.Error = probe.Error ?? "Unreadable image";
                    job.Progress = 100;
                    onProgress?.Invoke(job);
                    return;
                }

                job.Width = probe.Width;
                job.Height = probe.Height;
                job.SizeBytes = probe.Bytes;
                job.Status = "converting";
                job.Progress = 35;
                onProgress?.Invoke(job);

                string target = planner.Plan(job.SourcePath, outputFolder, options.Format, overwrite);

                var result = await Task.Run(
                    () => ImageConverter.Convert(job.SourcePath, target, options, overwrite, cancellationToken),
                    cancellationToken).ConfigureAwait(false);

                if (result.Success)
                {
                    job.Status = "done";
                    job.OutputPath = result.OutputPath;
                    job.OutputBytes = result.OutputBytes;
                    job.Width = result.Width;
                    job.Height = result.Height;
                    job.Progress = 100;
                }
                else
                {
                    job.Status = "failed";
                    job.Error = result.Message;
                    job.Progress = 100;
                }

                onProgress?.Invoke(job);
            }
            catch (OperationCanceledException)
            {
                job.Status = "failed";
                job.Error = "Cancelled";
                job.Progress = 100;
                onProgress?.Invoke(job);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
