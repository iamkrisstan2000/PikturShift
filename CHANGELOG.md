# Changelog

## V1.0.1 — 21 October 2026

- Matched the titlebar and navigation rail colours; corrected the rounded-corner backing so it blends with the translucent rail and Mica backdrop.
- Prevented dark-mode corner flashes by avoiding runtime DWM frame rebuilds.
- Reduced queued-image thumbnail decode size from 96 px to 64 px on the long edge (about 56% fewer decoded thumbnail pixels).
- Replaced one-task-per-file batch scheduling with a fixed worker pool; pending queues use bounded task overhead, worker count adapts to available memory, and completed output-path reservations are released as the batch runs.
- Limited preview loading to two workers and discard stale previews after queue clears/removals.
- Added tests for worker limits, cancellation of pending jobs, and bounded concurrency.
