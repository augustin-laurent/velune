# Contributing

## Architecture

Velune uses a layered architecture:

- `Velune.Presentation -> Velune.Application`
- `Velune.Application -> Velune.Domain`
- `Velune.Infrastructure -> Velune.Application + Velune.Domain`
- `Velune.App` composes dependencies for macOS and Linux
- `Velune.Windows` composes dependencies for Windows

The domain layer must not depend on UI, filesystem, native tooling or infrastructure packages.

## Code Rules

- Keep `nullable` enabled.
- Treat warnings as errors.
- Long-running or IO-bound operations must accept a `CancellationToken`.
- Do not put business logic in views.
- Do not let external infrastructure libraries leak into `Presentation`.
- Prefer immutable types when data crosses boundaries.
- Keep names explicit and business-oriented.
- Avoid large methods and oversized ViewModels.

## Style

- 4-space indentation
- LF line endings
- mandatory braces
- `var` only when the type is obvious

## Tests

- `Velune.Tests.Unit`: domain and application behavior
- `Velune.Tests.Integration`: infrastructure behavior
- `Velune.Tests.Render`: render snapshots

Before opening a PR:

```bash
dotnet build Velune.slnx --configuration Release
dotnet test tests/Velune.Tests.Unit/Velune.Tests.Unit.csproj --configuration Release
```

Run integration or render tests when touching infrastructure, rendering, OCR, PDF/image handling or UI layout-sensitive behavior.
