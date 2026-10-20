# PikturShift

**A fast, lightweight batch image converter for Windows 11 — native Fluent UI, Mica backdrop, no bloat.**

<img src="src/PikturShift/Assets/app-256.png" width="96" alt="PikturShift icon">

Drop a folder of photos, pick a format, hit **Convert**. One file or several hundred — PikturShift does the lot in parallel and keeps your originals untouched.

- **Version 1.0** · Windows 10 1809+ / Windows 11 · x64
- **Download:** [`release/PikturShift.exe`](release/PikturShift.exe) — one file, ~64 MB, self-contained (no .NET install, no installer, no admin rights)

> **SmartScreen:** the exe is not code-signed (that needs a paid certificate), so Windows may say *"Windows protected your PC"* the first time. Choose **More info → Run anyway**. You can also build it yourself in about a minute — see below.

---

## Features

| | |
|---|---|
| **Input** | JPG, JPEG, PNG, WebP, BMP, GIF, TIFF (first frame for animated files) |
| **Output** | JPG, PNG, WebP, BMP, TIFF — any source to any target |
| **Batch** | Unlimited files, converted in parallel (RAM-aware, up to 6 workers) |
| **Quality** | 1–100 % for JPG and WebP |
| **Transparency** | Kept for PNG / WebP / TIFF · flattened onto any hex colour for JPG / BMP |
| **EXIF** | Optional copy-through; orientation is always baked into the pixels so nothing ends up sideways |
| **Saving** | Same folder as the source, or a folder you choose · overwrite toggle · automatic `name (1).jpg` collisions |
| **Safety** | Every file is written to a temporary name and then moved, so a crash never leaves a half-written image |
| **Privacy** | Fully offline. No telemetry, no accounts, no network calls |

---

## Using it

- Drop files or folders anywhere on the window — folders are expanded one level and unsupported files are skipped with a notice.
- Click the drop zone, the **+** button, or press `Ctrl+O` to browse.
- Double-click a row to reveal the file in Explorer; the **×** removes it, the **✕** in the *Files* header clears the queue.
- The format picker is the single source of truth: quality and fill controls appear only when they apply.
- **Cancel** stops cleanly — files already written stay valid.

| Keys | Action |
|---|---|
| `Ctrl+O` | Add images |
| `Ctrl+Enter` | Convert |
| `Esc` | Cancel a running conversion |
| `Delete` | Remove the selected row |
| `Ctrl+,` or `F1` | Open Settings |

Settings, window position and menu state live in `%LOCALAPPDATA%\PikturShift\settings.json`; crashes are written to `crash.log` in the same folder.

---

## Look and feel

- One navigation rail that widens in place (WinUI `NavigationView` behaviour), a 10 px rounded content corner and a caption bar that sits flush with the rail.
- **Theme:** System / Dark / Light — *System* follows Windows and updates live.
- **Accents:** System (reads your real Windows accent), Matcha, Red, Pink, Cyan, Purple, Beige, plus a mono swatch that flips **Black on Light / White on Dark**. Surfaces adopt the accent hue, so the whole window re-tints instead of keeping a fixed cast.
- **Translucency:** native Mica backdrop on Windows 11 22H2+ — the rail lets 6 % through in Dark (2.5 % in Light) and the content surface 8 % in Dark (1.5 % in Light). Without a backdrop, every surface stays 100 % opaque rather than blending against black.
- Every control is a first-party WPF template: no UI framework, no runtime skinning, nothing to download.

---

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) and Windows (or `-p:EnableWindowsTargeting=true` on Linux/macOS).

```
dotnet build PikturShift.sln -c Release
dotnet run --project tests/PikturShift.Tests      # 51 checks, no test framework needed
```

Single-file release build:

```
dotnet publish src/PikturShift/PikturShift.csproj -c Release -r win-x64 ^
  -p:SelfContained=true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

```
PikturShift.sln
├── src/PikturShift          WPF shell — XAML templates, theming, view models, Win32 interop
├── src/PikturShift.Core     UI-free engine (ImageSharp): convert, probe, paths, settings
├── tests/PikturShift.Tests  headless test harness — 51 checks
└── tools/                   icon generator, Wine screenshot harness, dev setup
```

The engine builds and tests on any OS (`dotnet run --project tests/PikturShift.Tests`); only the WPF project is Windows-only.

---

## Support PikturShift

PikturShift is free and open source. No ads, no telemetry, no "Pro" upsell, no account to create — a good image converter shouldn't cost money or nag you, so this one doesn't.

It is built in the evenings and given away as-is. If it saved you a little time, here's how you can say thanks — every one of these genuinely helps:

- **⭐ Star the repository** — the strongest signal that tells other people the project is worth a try.
- **📣 Share it** — a link in a Discord server, a Reddit thread or a group chat reaches more people than any advertising budget I could ever afford.
- **🐞 Report what breaks** — [open an issue](https://github.com/iamkrisstan2000/PikturShift/issues) with the file that failed and what you expected. Bug reports are free QA, and they make the next release better.
- **💡 Ask for what's missing** — a format, a preset, a shortcut. Requests shape the roadmap.
- **☕ Buy me a coffee** — if you'd rather chip in, you can [sponsor the project on GitHub](https://github.com/sponsors/iamkrisstan2000). It pays for the coffee behind the late-night builds.

No pressure either way: using PikturShift and telling one friend about it is already support. Thank you. 💚

---

## License

PikturShift is free software: you can redistribute it and/or modify it under the terms of the **GNU General Public License, version 3** as published by the Free Software Foundation.

It is distributed in the hope that it will be useful, but **without any warranty**; without even the implied warranty of merchantability or fitness for a particular purpose. See [LICENSE](LICENSE) for the full text.

Copyright © 2026 Mr KNIGHT.

Bundled third-party component: [SixLabors.ImageSharp 2.1](https://github.com/SixLabors/ImageSharp), under the Six Labors Split License (free for open-source projects).

---

*Made by Mr KNIGHT · Made possible with AI · October 2026*
