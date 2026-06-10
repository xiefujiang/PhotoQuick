# PhotoQuick

PhotoQuick is a lightweight Windows image viewer and file management tool designed for fast local photo browsing, culling, and organization. It supports folder scanning, smooth zooming, panning, rotation, sorting, renaming, moving images to preset folders, and deleting files to the recycle bin, all within a clean dark interface inspired by the Windows Photos app.

## Project Info

- Name: PhotoQuick
- Author: w1ldptr
- Version: 1.0.0
- License: MIT
- Repository: https://github.com/xiefujiang/PhotoQuick

## Requirements

- Windows 10/11
- .NET 10 Desktop Runtime for framework-dependent builds

## Run

```powershell
dotnet run
```

## Features

- WPF desktop UI for Windows 10/11.
- Async folder scanning with optional recursive traversal.
- Supported extensions: JPG, JPEG, PNG, BMP, TIFF, WebP, NEF, CR2, CR3, ARW, RW2, ORF.
- Single-image browsing with previous/next buttons and keyboard shortcuts.
- Mouse wheel zoom, drag panning, rotation, and double-click reset.
- Lazy image decoding plus nearby image preloading.
- Memory cache capped to the current image neighborhood.
- Sort by file name or file modified time, ascending or descending.
- JSON settings in `%AppData%\PhotoQuick\settings.json`.
- Preset move-folder management.
- Rename while preserving the original extension.
- Move current file to a preset directory.
- Delete current file to the Windows recycle bin.

## Notes

- RAW files are indexed and passed through the decoder bridge. If Windows has a matching WIC/camera codec, a preview may render. Otherwise PhotoQuick keeps navigation responsive.
- A native LibRaw bridge can replace `Infrastructure/WicImageDecoder.cs` later without changing the ViewModel or UI contracts.
