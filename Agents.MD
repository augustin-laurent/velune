# Agents Guide

This file records the project rules to follow when working on Velune.

## Product Direction

Velune is a local-first desktop app inspired by macOS Preview. V1 must let users install one app and then open, review, annotate, sign, OCR, merge, reorder and save PDF/image documents.

Rules:

- no cloud processing by default;
- no accounts for V1 workflows;
- no manual user installation of `.NET`, `qpdf`, `tesseract` or OCR data;
- Windows is the main V1 distribution target;
- macOS and Linux dev builds must remain working.

## Architecture

Layering:

```text
Presentation -> Application -> Domain
Infrastructure -> Application + Domain
App -> composition root
```

Responsibilities:

- `Velune.App`: bootstrap, settings and dependency composition
- `Velune.Presentation`: Avalonia views, ViewModels, UI behavior and localization
- `Velune.Application`: use cases, DTOs, orchestration and application services
- `Velune.Domain`: document models, annotations, value objects and business rules
- `Velune.Infrastructure`: PDF, image, OCR, printing, native tools and filesystem implementations

Do not break dependency direction. If a lower layer needs behavior, add a small abstraction at the right boundary and implement it outward.

## Code Rules

- Keep domain/application code independent from Avalonia, PDFium, Skia, Tesseract, qpdf and filesystem details.
- Put business workflow in application use cases.
- Keep native/process/platform code in infrastructure.
- Keep views thin; code-behind may coordinate UI events only.
- Use `Result<T>` and `AppError` for expected failures.
- Use `ProcessStartInfo.ArgumentList` for process calls.
- Every long operation must support `CancellationToken`.
- Prefer immutable DTOs and value objects where data crosses boundaries.
- Add dependencies only when they solve a real problem and fit the existing layering.
- Keep changes scoped to the story.

## Patterns

Use patterns only when they reduce real complexity.

Preferred existing patterns:

- MVVM for presentation
- use case classes for application actions
- small interfaces at layer boundaries
- strategy/dispatching for document open/render paths
- adapters around native tools and libraries
- options pattern for configuration

Avoid speculative factories, generic managers, service locators and broad abstractions without more than one real implementation.

## Packaging

V1 packages must bundle required native tools.

Runtime resolution must prefer:

```text
tools/qpdf/...
tools/tesseract/...
```

before system PATH.

Minimum bundled OCR data:

- `eng.traineddata`
- `fra.traineddata`

Relevant files:

- `.github/workflows/dev-builds.yml`
- `eng/Collect-DevNativeTools.ps1`
- `eng/New-DevBuildPackage.ps1`
- `src/Velune.Infrastructure/FileSystem/BundledToolResolver.cs`
- `docs/dev-builds.md`
- `docs/packaging-distributions/v1-packaging-distribution.md`

## Versioning

`VersionPrefix` lives in `Directory.Build.props`.

- `main`: bump major or minor when preparing a new product version.
- `maintenance/<tag>`: fixes only.
- maintenance releases only increase patch `ZZ` in `XX.YY.ZZ`.

Dev build versions:

```text
main/master:         X.Y.Z-dev.<run_number>+<short_sha>
maintenance/vX.Y.Z: X.Y.Z-maintenance.<run_number>+<short_sha>
```

## Verification

Default checks:

```bash
dotnet build Velune.slnx --configuration Release
dotnet test tests/Velune.Tests.Unit/Velune.Tests.Unit.csproj --configuration Release
```

Run integration tests for infrastructure, OCR, PDF/image operations or filesystem changes.

Run render tests for rendering, thumbnails, annotations or layout-sensitive changes.

## UI

- Keep the app compact and direct.
- Prefer direct manipulation over explanatory screens.
- Keep toolbars predictable and panel-based workflows clear.
- Do not add visible instructional copy unless the user is blocked.
- Never freeze the UI for rendering, OCR, printing or PDF operations.

## Localization

User-facing strings belong in localization files under:

```text
src/Velune.Presentation/Resources/Localization/
```

Add English, French and Spanish entries when adding user-facing text.

## Privacy and Security

- Keep document processing local.
- Do not upload documents, OCR output, signatures, thumbnails or metadata.
- Do not log document text or selected text.
- Keep user data outside the install directory.
- Treat bundled tools as read-only runtime assets.
- Pin and verify native binaries before public release.

## Documentation

Keep docs short and current.

- `README.md`: project overview
- `CONTRIBUTING.md`: contribution rules
- `docs/dev-builds.md`: development artifacts
- `docs/packaging-distributions/`: packaging and distribution notes

Remove stale references instead of keeping placeholder docs.
