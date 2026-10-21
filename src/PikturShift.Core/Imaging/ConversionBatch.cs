namespace PikturShift.Core.Imaging;

/// <summary>
/// Runs a queue of conversion jobs in parallel with a bounded degree of concurrency.
/// Used by the desktop app; also usable headless (see --cli in the app, handy for testing).
/// </summary>
public static class ConversionBatch
{
    private const long BytesPerWorkerBudget = 1024L * 1024 * 1024;
    private const int MaximumWorkers = 6;

    /// <summary>
    /// Caps concurrency by both CPU count and available memory. A worker may hold several
    /// decoded image buffers, so a one-gigabyte-per-worker budget is a conservative guard
    /// against large batches exhausting low-memory machines.
    /// </summary>
    public static int DefaultParallelism
    {
        get
        {
            int cpuLimit = Math.Clamp(Environment.ProcessorCount, 1, MaximumWorkers);
            long memory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (memory <= 0)
            {
                return cpuLimit;
            }

            int memoryLimit = (int)Math.Clamp(memory / BytesPerWorkerBudget, 1, MaximumWorkers);
            return Math.Min(cpuLimit, memoryLimit);
        }
    }

    /// <summary>
    /// Converts all jobs. <paramref name="onProgress"/> is called from worker threads;
    /// marshal to the UI thread inside the callback when needed.
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
        if (jobs.Count == 0)
        {
            return;
        }

        var planner = new OutputPathPlanner();
        int degree = parallelism <= 0 ? DefaultParallelism : Math.Clamp(parallelism, 1, MaximumWorkers);
        int nextIndex = -1;

        void ProcessQueue()
        {
            while (true)
            {
                int index = Interlocked.Increment(ref nextIndex);
                if (index >= jobs.Count)
                {
                    return;
                }

                var job = jobs[index];
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    job.Status = "reading";
                    job.Progress = 8;
                    onProgress?.Invoke(job);

                    var probe = ImageConverter.Probe(job.SourcePath);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!probe.Success)
                    {
                        job.Status = "failed";
                        job.Error = probe.Error ?? "Unreadable image";
                        job.Progress = 100;
                        onProgress?.Invoke(job);
                        continue;
                    }

                    job.Width = probe.Width;
                    job.Height = probe.Height;
                    job.SizeBytes = probe.Bytes;
                    job.Status = "converting";
                    job.Progress = 35;
                    onProgress?.Invoke(job);

                    string target = planner.Plan(job.SourcePath, outputFolder, options.Format, overwrite);
                    ConversionResult result;
                    try
                    {
                        result = ImageConverter.Convert(
                            job.SourcePath, target, options, overwrite, cancellationToken);
                    }
                    finally
                    {
                        // Once conversion completes, the on-disk file protects a successful
                        // reservation; failed paths can be reused. Keep only active paths in
                        // the planner instead of retaining one string for every batch item.
                        planner.Release(target);
                    }

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
                catch (Exception ex)
                {
                    job.Status = "failed";
                    job.Error = ex.Message;
                    job.Progress = 100;
                    onProgress?.Invoke(job);
                }
            }
        }

        // Keep the scheduler's memory use constant: a large queue runs on at most six worker
        // tasks rather than allocating one waiting task per input file.
        int workerCount = Math.Min(degree, jobs.Count);
        var workers = new Task[workerCount];
        for (int i = 0; i < workers.Length; i++)
        {
            workers[i] = Task.Run(ProcessQueue, CancellationToken.None);
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
    }
}
