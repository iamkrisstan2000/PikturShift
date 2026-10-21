# PikturShift

**A fast, private batch image converter for Windows 11.** Drop photos, choose a format, and convert—offline, with no account or telemetry.

- **Official release: V1.0.1** · Windows 10 1809+ / Windows 11 · x64
- **Download:** [`release/PikturShift.exe`](release/PikturShift.exe) — single-file, self-contained, no .NET installation required
- **Source:** [`release/PikturShift-source.zip`](release/PikturShift-source.zip)

> The app is unsigned, so SmartScreen may show a warning on first launch. Choose **More info → Run anyway** if you trust the build.

## Features

- **Input:** JPG, JPEG, PNG, WebP, BMP, GIF, TIFF (first frame of animated files)
- **Output:** JPG, PNG, WebP, BMP, TIFF
- **Batch conversion:** bounded, memory-aware workers; safe naming when files share a name
- **Quality:** 1–100% for JPG and WebP
- **Transparency:** preserved where supported; choose a fill colour for JPG/BMP
- **EXIF:** optional metadata copy; orientation is corrected in the output
- **Save:** beside the original or in a selected folder; overwrite option
- **Privacy:** offline; no telemetry or network requests

## Quick start

1. Run `PikturShift.exe`.
2. Drop files or a folder into the window, or press **Ctrl+O**.
3. Choose an output format and destination, then select **Convert**.

`Ctrl+Enter` converts, `Esc` cancels, `Delete` removes the selected row, and `Ctrl+,` opens Settings.

## Appearance

System / Dark / Light themes, live Windows accent matching, seven named accents plus mono, and native Mica translucency where Windows 11 supports it. The UI is built with WPF and custom Fluent-style controls.

## Build and test

Requires the .NET 8 SDK. Build on Windows with:

```sh
dotnet build PikturShift.sln -c Release
dotnet run --project tests/PikturShift.Tests
```

The WPF app is Windows-only; the conversion engine and tests can run cross-platform. The single-file Windows release can be reproduced with:

```sh
dotnet publish src/PikturShift/PikturShift.csproj -c Release -r win-x64 \
  -p:SelfContained=true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

See [CHANGELOG.md](CHANGELOG.md) for release notes and [LICENSE](LICENSE) for the GNU GPL v3 terms. ImageSharp is a third-party dependency under the Six Labors Split License.

## Support

PikturShift is free and open source. If it helps, a repo star, a share, a bug report, or a feature suggestion is a lovely way to support it. You can also [sponsor the project](https://github.com/sponsors/iamkrisstan2000)—no pressure; using the app is already appreciated.

*Made by Mr KNIGHT · Made possible with AI · October 2026*
