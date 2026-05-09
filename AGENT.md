# Agent Instructions

This file provides context for AI coding assistants working on the Velune codebase.

## Project Overview

Velune is a local-first, offline document viewer (PDF + images) for desktop. It supports annotation, OCR text extraction, page manipulation, printing, and search. No network activity — fully offline.

- **Windows**: WinUI 3 (Windows App SDK) frontend in `Velune.Windows`
- **macOS/Linux**: Avalonia frontend in `Velune.Presentation` + `Velune.App`
- **Framework**: .NET 10, C# 13
- **Architecture**: Clean Architecture (Domain → Application → Infrastructure / Presentation)

## Solution Structure

```
src/
├── Velune.Domain/            Core business logic, entities, value objects
├── Velune.Application/       Use cases, DTOs, orchestrators, abstractions
├── Velune.Infrastructure/    Pdfium, SkiaSharp, Tesseract, qpdf, file I/O
├── Velune.Presentation/      Avalonia UI (ViewModels, views, localization)
├── Velune.Windows/           WinUI 3 UI (XAML, Windows-specific services)
└── Velune.App/               Avalonia entry point (macOS/Linux)

tests/
├── Velune.Tests.Unit/        Domain + Application layer tests
├── Velune.Tests.Integration/ Infrastructure tests
├── Velune.Tests.Render/      Render snapshot tests
├── Velune.Tests.Render.Host/ Test host for render snapshots
└── Velune.Tests.Windows.Unit/ Windows-specific unit tests
```

## Dependency Direction

```
Presentation ──→ Application ──→ Domain
Infrastructure ──→ Application + Domain
App / Windows = Composition Root (wires DI)
```

Domain has zero dependencies on external packages or other layers.

## Key Design Patterns

| Pattern | Location | Purpose |
|---------|----------|---------|
| Strategy | `DispatchingDocumentOpener`, `DispatchingRenderService` | Route to PDF or Image implementation |
| Result | `Result<T>`, `AppError` | Error handling without exceptions |
| Orchestrator | `RenderOrchestrator`, `DocumentTextAnalysisOrchestrator` | Background job queues with priority |
| Value Object | `DocumentId`, `PageIndex`, `ViewportState`, `Rotation` | Immutable domain primitives |
| MVVM | All ViewModels | UI data binding |
| DI | All layers | Constructor injection everywhere |

## Native Dependencies

- **Pdfium** (PDF rendering) — P/Invoke via `PdfiumNative.cs`, serialized by `PdfiumExecutionGate` (single-permit semaphore)
- **qpdf** (PDF structure: merge, split, reorder, rotate) — spawned as process, arguments via `ArgumentList.Add()` (no shell)
- **Tesseract** (OCR) — spawned as process, same safe pattern
- **SkiaSharp** (image rendering, annotation compositing) — managed wrapper

## Coding Conventions

- Nullable enabled, warnings as errors
- 4-space indent, LF endings, mandatory braces
- `var` only when type is obvious from RHS
- Long-running ops accept `CancellationToken`
- Business logic never in views
- Infrastructure libs never leak into Presentation
- Prefer immutable types at boundaries
- Use `ArgumentNullException.ThrowIfNull` for null checks
- XML doc comments on all public types and members (concise, one-sentence)

## Build & Test

```bash
# Build
dotnet build Velune.slnx

# Unit tests
dotnet test tests/Velune.Tests.Unit/Velune.Tests.Unit.csproj

# Integration tests
dotnet test tests/Velune.Tests.Integration/Velune.Tests.Integration.csproj

# Render snapshots
dotnet test tests/Velune.Tests.Render/Velune.Tests.Render.csproj
```

## Important Files

- `src/Velune.Application/Rendering/RenderOrchestrator.cs` — background render job scheduler (priority queues, cancellation)
- `src/Velune.Infrastructure/PDF/PdfiumExecutionGate.cs` — serializes all Pdfium native calls (not thread-safe)
- `src/Velune.Infrastructure/PDF/PdfiumNative.cs` — all P/Invoke declarations
- `src/Velune.Presentation/ViewModels/MainWindowViewModel.cs` — main Avalonia ViewModel (large, partial class)
- `src/Velune.Windows/ViewModels/WindowsMainViewModel.cs` — main WinUI ViewModel
- `src/Velune.Application/DependencyInjection/ServiceCollectionExtensions.cs` — Application DI registration
- `src/Velune.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` — Infrastructure DI registration

## Known Technical Debt

- `MainWindowViewModel` is a god class (29 dependencies, 100+ methods) — needs splitting
- `RenderOrchestrator` and `DocumentTextAnalysisOrchestrator` share ~95% queue logic — needs generic extraction
- `InMemoryPageViewportStore` lacks thread synchronization
- Inconsistent error handling across use cases (some catch, some don't)

## Security Notes

- No network activity, fully offline
- All process spawning uses `ArgumentList.Add()` — no command injection possible
- Native Pdfium handles wrapped in IDisposable with finalizer safety net
- File paths from user input — validate extensions for known formats
- Memory cache bounded (256MB cap, 64MB per entry)

## When Making Changes

1. Verify build compiles: `dotnet build Velune.slnx`
2. Run unit tests for Application/Domain changes
3. Run integration tests for Infrastructure changes
4. Don't add dependencies from Domain to any other layer
5. Don't let infrastructure types leak into Presentation
6. Add XML doc comments to any new public members
7. Use `Result<T>` pattern for operations that can fail — don't throw from use cases
