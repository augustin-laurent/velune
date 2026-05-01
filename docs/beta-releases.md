# Beta Releases

Beta releases are public validation builds, not final V1 installers.

## Tag Format

Use SemVer prerelease tags:

```text
v1.0.0-beta.1
v1.0.0-beta.2
```

Pushing a matching tag starts `.github/workflows/beta-release.yml`.

## Output

The workflow creates a GitHub pre-release and uploads:

- Windows x64 `.zip`
- Linux x64 `.tar.gz`
- macOS arm64 `.tar.gz` containing `Velune.app`
- SHA-256 checksum files for each package

Packages are self-contained and include `.NET`, `qpdf`, `tesseract`, English OCR data and French OCR data.

## Limits

- Windows beta packages are unsigned archives.
- macOS beta packages are unsigned and not notarized.
- Linux beta packages are archives, not AppImages.
- Stable V1 requires signing, notarization and license notice review.

## Release Command

```bash
git tag -a v1.0.0-beta.1 -m "Velune v1.0.0 beta 1"
git push origin v1.0.0-beta.1
```
