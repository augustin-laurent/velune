# Velune

Velune is a local-first desktop document viewer inspired by macOS Preview, built with .NET 10. macOS and Linux use Avalonia (deprecated will migrate on native UI for both system); Windows uses a dedicated WinUI 3 frontend.

## Features

- **PDF & Image viewing** with smooth page rendering and zoom
- **Annotations** — freehand, text, highlights, signatures (flattened into PDF on save)
- **OCR text extraction** via Tesseract for scanned documents
- **Text selection and search** across PDF text and OCR results
- **Page manipulation** — merge, split, reorder, rotate, delete pages (via qpdf)
- **Printing** with page range and orientation options
- **Multi-document tabs** with recent files history
- **Dark/Light theme** support
- **Localization** — multi-language UI
- **Fully offline** — no network activity, no telemetry

## Screenshots

_Coming soon_

## Project Layout

```
src/
├── Velune.Domain/            Business logic, entities, value objects
├── Velune.Application/       Use cases, DTOs, orchestrators, service contracts
├── Velune.Infrastructure/    PDF (Pdfium), images (SkiaSharp), OCR (Tesseract), qpdf
├── Velune.Presentation/      Avalonia UI — views, ViewModels, localization
├── Velune.Windows/           WinUI 3 UI — XAML, Windows-specific services
└── Velune.App/               Avalonia entry point (macOS/Linux) [Depecrated]

tests/
├── Velune.Tests.Unit/        Domain + Application tests
├── Velune.Tests.Integration/ Infrastructure tests
├── Velune.Tests.Render/      Render snapshot tests
└── Velune.Tests.Windows.Unit/ Windows-specific tests
```

## Architecture

Clean Architecture with strict dependency direction:

```
Presentation ──→ Application ──→ Domain
Infrastructure ──→ Application + Domain
App / Windows = Composition Root
```

The domain layer has zero dependencies on UI frameworks or infrastructure libraries.

Key design patterns:
- **Strategy** — `DispatchingDocumentOpener` / `DispatchingRenderService` route to PDF or Image implementations
- **Result pattern** — `Result<T>` for error handling without exceptions
- **Orchestrator** — background job queues with priority (viewer renders before thumbnails)
- **MVVM** — ViewModels with `ObservableObject` from MVVM Toolkit

## Getting Started

### Prerequisites

- .NET 10 SDK
- Windows 10 (build 26100+) for WinUI 3 app
- Bundled native tools (Pdfium, qpdf, Tesseract) — included in build output

### Build

```bash
dotnet build Velune.slnx
```

### Run

```bash
# Windows (WinUI 3)
dotnet run --project src/Velune.Windows/Velune.Windows.csproj

# macOS/Linux (Avalonia)
dotnet run --project src/Velune.App/Velune.App.csproj
```

## Keyboard Shortcuts

### File & Navigation

| Action | Windows | macOS |
|--------|---------|-------|
| Open file | `Ctrl+O` | `Cmd+O` |
| Save | `Ctrl+S` | `Cmd+S` |
| Print | `Ctrl+P` | `Cmd+P` |
| Next page | `→` / `PageDown` | `→` / `PageDown` |
| Previous page | `←` / `PageUp` | `←` / `PageUp` |
| Search | `Ctrl+F` | `Cmd+F` |

### Zoom & View

| Action | Windows | macOS |
|--------|---------|-------|
| Zoom in | `Ctrl++` | `Cmd++` |
| Zoom out | `Ctrl+-` | `Cmd+-` |
| Fit to page | `Ctrl+0` | `Cmd+0` |
| Fit to width / Actual size | `Ctrl+1` | `Cmd+1` |

### Rotation & Transform

| Action | Windows | macOS |
|--------|---------|-------|
| Rotate clockwise | `Ctrl+R` | `Cmd+R` |
| Rotate counter-clockwise | `Ctrl+Shift+R` | `Cmd+Shift+R` |
| Flip horizontal (annotation) | `Ctrl+Shift+H` | — |

### Editing

| Action | Windows | macOS |
|--------|---------|-------|
| Undo | `Ctrl+Z` | `Cmd+Z` |
| Redo | `Ctrl+Y` | `Cmd+Y` |
| Delete annotation | `Delete` | `Delete` |
| Copy selected text | `Ctrl+C` | `Cmd+C` |

### Panels

| Action | Windows | macOS |
|--------|---------|-------|
| Toggle annotations panel | `Ctrl+Shift+A` | `Cmd+Shift+A` |
| Move page earlier | `Ctrl+Shift+↑` | `Cmd+Shift+↑` |
| Move page later | `Ctrl+Shift+↓` | `Cmd+Shift+↓` |

## Tests

```bash
# Unit tests
dotnet test tests/Velune.Tests.Unit/Velune.Tests.Unit.csproj

# Integration tests
dotnet test tests/Velune.Tests.Integration/Velune.Tests.Integration.csproj

# Render snapshot tests
dotnet test tests/Velune.Tests.Render/Velune.Tests.Render.csproj
```

Render snapshots live in `tests/Velune.Tests.Render/Snapshots/Approved`.

## Development Metrics

In `Development` configuration, Velune logs performance metrics at `Information` level:

- `MVP metric | DocumentOpen`
- `MVP metric | FirstPageRender`
- `MVP metric | ThumbnailRender`

Memory values are approximate (`GC.GetTotalMemory(false)` and `Process.WorkingSet64`).

## CI

Pull requests run restore and build on Ubuntu, Windows, and macOS. Unit and integration tests run on Ubuntu. Render snapshots run on macOS for stable PDF rasterization baselines.

Additional quality workflows:
- `.github/workflows/codeql.yml` — code scanning
- `.github/workflows/semgrep.yml` — static analysis
- `.github/workflows/sonarqube-cloud.yml` — code quality

## Documentation

- [`docs/architecture/class-diagram.md`](docs/architecture/class-diagram.md) — UML class diagram (Mermaid)
- [`docs/architecture/sequence-diagrams.md`](docs/architecture/sequence-diagrams.md) — key flow sequence diagrams
- [`docs/dev-builds.md`](docs/dev-builds.md) — development build artifacts
- [`docs/beta-releases.md`](docs/beta-releases.md) — beta release notes
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — contributor guidelines
- [`AGENT.md`](AGENT.md) — AI agent instructions

## License

_Private repository_
