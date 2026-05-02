# Velune

Velune is a local-first desktop app inspired by macOS Preview, built with .NET. macOS and Linux use Avalonia; Windows uses a dedicated WinUI 3 frontend.

## Project Layout

- `Velune.App`: app bootstrap and dependency composition
- `Velune.Windows`: Windows WinUI 3 app and installer entrypoint
- `Velune.Presentation`: Avalonia views, ViewModels, localization and UI behavior
- `Velune.Application`: use cases, DTOs and application services
- `Velune.Domain`: document models, value objects and business contracts
- `Velune.Infrastructure`: PDF, image, OCR, printing and filesystem implementations
- `tests/*`: unit, integration and render tests

## Architecture

Dependency direction:

```text
Presentation -> Application -> Domain
Infrastructure -> Application + Domain
App / Windows -> composition root
```

The domain layer must stay independent from UI frameworks and infrastructure libraries.

## Development Metrics

In `Development`, Velune logs MVP metrics at `Information` level:

- `MVP metric | DocumentOpen`
- `MVP metric | FirstPageRender`
- `MVP metric | ThumbnailRender`

Memory values are approximate and are based on `GC.GetTotalMemory(false)` and `Process.WorkingSet64`.

## Tests

Run the unit suite:

```bash
dotnet test tests/Velune.Tests.Unit/Velune.Tests.Unit.csproj
```

Run integration tests:

```bash
dotnet test tests/Velune.Tests.Integration/Velune.Tests.Integration.csproj
```

Run render snapshot tests:

```bash
dotnet test tests/Velune.Tests.Render/Velune.Tests.Render.csproj
```

Render snapshots live in `tests/Velune.Tests.Render/Snapshots/Approved`.

## CI

Pull requests run restore and build on Ubuntu, Windows and macOS. Unit and integration tests run on Ubuntu. Render snapshots run on macOS to keep PDF rasterization baselines stable.

Development build artifacts are documented in [`docs/dev-builds.md`](docs/dev-builds.md).

## Quality

Additional workflows cover static analysis and security:

- `.github/workflows/codeql.yml`
- `.github/workflows/semgrep.yml`
- `.github/workflows/sonarqube-cloud.yml`
