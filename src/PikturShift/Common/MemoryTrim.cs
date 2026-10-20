using System.Runtime.InteropServices;

namespace PikturShift.Common;

/// <summary>
/// Hands freed pages back to Windows after a heavy operation. Converting a batch makes the
/// GC grow its heap and leaves the touched pages resident; without this the process keeps
/// looking like it is using far more RAM than it needs between batches.
/// </summary>
internal static class MemoryTrim
{
    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr process);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    /// <summary>
    /// Collects, then releases the working set. Called after a batch finishes or the queue is
    /// emptied - never on a timer, so it cannot cause paging during normal use.
    /// </summary>
    public static void AfterHeavyWork()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false, compacting: false);
            GC.WaitForPendingFinalizers();
            EmptyWorkingSet(GetCurrentProcess());
        }
        catch
        {
            // Trimming is an optimisation; never let it surface as an error.
        }
    }
}
