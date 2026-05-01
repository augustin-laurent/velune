# Development Builds

Development builds provide shareable executable artifacts for the target platforms. They are not final signed installers.

Public beta releases use `.github/workflows/beta-release.yml`; see `docs/beta-releases.md`.

## Artifacts

- Windows: self-contained `win-x64` `.zip`
- Linux: self-contained `linux-x64` `.tar.gz`
- macOS: self-contained `osx-arm64` `.tar.gz` containing `Velune.app`

Each artifact must include the native tools required by Velune. Users must not install `.NET`, `qpdf`, `tesseract` or OCR data separately.

Expected structure:

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

Velune resolves bundled tools before falling back to system paths.

## Workflow

`.github/workflows/dev-builds.yml` runs on:

- manual dispatch
- pull requests targeting `main` or `master`
- pushes to `main`, `master` or `maintenance/**`

The workflow:

1. restores the solution;
2. installs native tools on the runner;
3. collects them into `artifacts/native-tools/<rid>`;
4. publishes the app self-contained;
5. copies native tools into the package;
6. uploads the package and `.sha256` checksum.

Packaging fails if `qpdf`, `tesseract`, `eng.traineddata` or `fra.traineddata` are missing.

## Versioning

`VersionPrefix` is defined in `Directory.Build.props`.

Build version format:

```text
main/master:           X.Y.Z-dev.<run_number>+<short_sha>
maintenance/vX.Y.Z:   X.Y.Z-maintenance.<run_number>+<short_sha>
```

Rules:

- changes merged to `main` bump major or minor when preparing a new product version;
- maintenance branches use `maintenance/<tag>`;
- maintenance branches only receive fixes;
- stable releases from maintenance branches only increase patch `ZZ` in `XX.YY.ZZ`.

## Local Commands

Requires PowerShell 7.

Resolve a build version:

```powershell
pwsh -NoProfile -File ./eng/Get-DevBuildVersion.ps1
```

Create a package:

```powershell
pwsh -NoProfile -File ./eng/New-DevBuildPackage.ps1 `
  -RuntimeIdentifier osx-arm64 `
  -Version 0.1.0-dev.local `
  -InformationalVersion 0.1.0-dev.local+local
```

Packages are written to:

```text
artifacts/dev-builds/packages/
```

## Notes

- Dev artifacts are not signed.
- macOS dev artifacts are not notarized.
- Windows dev artifacts are not MSIX yet.
- Linux dev artifacts are not AppImage yet.
- Initial bundled OCR languages are English and French.
- Native binary licenses and notices must be audited before public release.
