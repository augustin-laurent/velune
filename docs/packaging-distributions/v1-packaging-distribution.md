# Decision: V1 Packaging and Distribution

Date: 2026-05-01

Status: proposed for V1

Dependency: APP-090

## Context

Velune needs a clear packaging and distribution strategy for each supported desktop platform. V1 must be easy to install, local-first and usable without extra manual setup.

## Decision

V1 packages must be self-contained and must bundle required native tooling. Users install Velune once and do not install `.NET`, `qpdf`, `tesseract` or OCR data separately.

V1 beta targets:

- Windows: unsigned Inno Setup `.exe` installer for Windows 11 24H2+ x64
- macOS: unsigned `.tar.gz` containing `Velune.app`
- Linux: `.tar.gz`

Stable V1 targets:

- Windows: signed MSIX/MSIXBundle
- macOS: signed and notarized `.dmg` containing `Velune.app`
- Linux: AppImage first, Flatpak/Flathub after validation

Development builds may use simpler packages until stable release packaging is ready:

- Windows Inno Setup `.exe`
- Linux `.tar.gz`
- macOS `.tar.gz` containing `Velune.app`

Beta releases use the same formats and must be published as GitHub pre-releases.

## Platform Notes

### Windows

- V1 beta channel: unsigned Inno Setup installer.
- Main stable V1 channel: signed MSIX/MSIXBundle.
- Microsoft Store is preferred when validation and timing allow it.
- `winget` can be added once a stable signed package URL exists.
- File associations and shell verbs must be handled in packaging.
- Explorer preview or context-menu extensions should be separate follow-up work if MSIX is not enough.

### macOS

- Direct distribution uses a `.dmg` with a Developer ID signed and notarized `.app`.
- Mac App Store is not the default V1 path because sandboxing may constrain local document workflows and bundled native tools.
- Builds should cover `osx-arm64`; `osx-x64` can be added when needed.

### Linux

- Start with AppImage for direct distribution.
- Move to Flatpak/Flathub once filesystem, printing, OCR and bundled native tools are validated under sandbox constraints.
- `.deb` and `.rpm` are deferred until there is enough demand.

## CI/CD Impact

Release packaging should run from tags or protected release branches.

Required steps:

1. restore, build and test in `Release`;
2. publish self-contained per RID;
3. collect bundled native tools;
4. package per OS;
5. generate SHA-256 checksums;
6. run package smoke tests.

Smoke tests should cover:

- app startup;
- PDF and image opening;
- first page render;
- bundled `qpdf` detection;
- bundled `tesseract` detection;
- OCR using bundled data;
- preferences and signature storage.

## Native Tools

Each package must contain:

```text
tools/
  qpdf/
    bin/
    lib/
  tesseract/
    bin/
    lib/
    tessdata/
      eng.traineddata
      fra.traineddata
```

The app must resolve bundled tools before falling back to system paths.

## Native Tool Acquisition (Windows)

Windows builds use `eng/Download-WindowsNativeTools.ps1` to download pinned releases directly from GitHub:

- **qpdf**: Downloaded from `github.com/qpdf/qpdf/releases` (MSVC64 zip)
- **Tesseract**: Downloaded from `github.com/UB-Mannheim/tesseract/releases` (Inno Setup installer, silent extracted)
- **tessdata**: Downloaded from `github.com/tesseract-ocr/tessdata_fast` (eng, fra, osd)

Versions are pinned in the script parameters for reproducibility. The script is idempotent — skips download if tools already present (use `-Force` to re-download).

CI no longer uses Chocolatey for native tools on Windows — direct downloads are faster and ensure consistent versions across builds.

macOS and Linux still use `eng/Collect-DevNativeTools.ps1` which finds system-installed tools (via brew/apt) and bundles them.

## Third-Party Notices

`eng/windows-installer/THIRD-PARTY-NOTICES.txt` is bundled in the installer and installed to `{app}/THIRD-PARTY-NOTICES.txt`. Covers: qpdf (Apache 2.0), Tesseract (Apache 2.0), PDFium (BSD 3-Clause), SkiaSharp (MIT), .NET Runtime (MIT).

## File Associations

The Windows installer registers:
- `.pdf` — OpenWithProgids for Velune.PDF
- `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp` — OpenWithProgids for Velune.Image

Velune does not set itself as default handler. Users can choose Velune from "Open with" in Explorer.

## Open Items

- Confirm final package identity: publisher, bundle id, app id and channel naming.
- Decide whether V1 includes Windows context menu actions beyond file associations.
- Choose the Windows signing path (code signing certificate).
- Choose the macOS packaging/signing implementation.
- Decide OCR languages beyond English and French.
- Decide whether Inno remains a beta-only channel or stays as the direct download channel after V1.
