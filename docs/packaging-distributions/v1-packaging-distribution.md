# Decision: V1 Packaging and Distribution

Date: 2026-05-01

Status: proposed for V1

Dependency: APP-090

## Context

Velune needs a clear packaging and distribution strategy for each supported desktop platform. V1 must be easy to install, local-first and usable without extra manual setup.

## Decision

V1 packages must be self-contained and must bundle required native tooling. Users install Velune once and do not install `.NET`, `qpdf`, `tesseract` or OCR data separately.

Primary targets:

- Windows: signed MSIX/MSIXBundle
- macOS: signed and notarized `.dmg` containing `Velune.app`
- Linux: AppImage first, Flatpak/Flathub after validation

Development builds may use archives until final installers are ready:

- Windows `.zip`
- Linux `.tar.gz`
- macOS `.tar.gz` containing `Velune.app`

## Platform Notes

### Windows

- Main V1 channel: signed MSIX/MSIXBundle.
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

## Open Items

- Confirm final package identity: publisher, bundle id, app id and channel naming.
- Decide whether V1 includes only file associations or also Windows context menu actions.
- Choose the Windows signing path.
- Choose the macOS packaging/signing implementation.
- Audit bundled native binary licenses and notices.
- Decide OCR languages beyond English and French.
