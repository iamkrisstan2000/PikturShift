# What's new

## V1.1.0 · 1 November 2026

A little polish, plus a lighter memory footprint:

- Dragging files highlights only the drop zone in your chosen accent; the queue and settings stay clear, and dropping onto the rest of the app still works.
- The main content lets 5% of the backdrop through in Dark and 3% in Light. The rail and titlebar are unchanged.
- Large batches use up to four memory-aware conversion workers. Thumbnail previews use less memory, preview loading stays at two workers, and pending files no longer each create a waiting task.
- Cleared unused XAML names and release completed output-path bookkeeping as conversions finish.

Thanks for using PikturShift — and for helping make it better.
