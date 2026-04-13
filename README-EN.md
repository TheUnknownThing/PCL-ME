[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Frontend.Avalonia/Assets/icon.png" alt="PCL-ME logo" width="80" height="80">

# PCL-ME

Plain Craft Launcher Multiplatform Edition

[Releases](https://github.com/TheUnknownThing/PCL-CE/releases/latest) |
[Issues](https://github.com/TheUnknownThing/PCL-CE/issues) |
[Contributing](CONTRIBUTING.md) |
[Avalonia Frontend Docs](PCL.Frontend.Avalonia/README.md)

</div>

PCL-ME is a multiplatform edition derived from PCL-CE. This repository fully drops the old VB.NET frontend path and continues the launcher in C# with an Avalonia desktop UI that targets Windows, macOS, and Linux.

## Highlights

- The active codebase is C# only; the legacy VB.NET / WPF frontend is no longer the maintained direction here.
- The project targets `.NET 10`.
- The frontend UI uses Avalonia to provide a consistent cross-platform experience with native performance.

## Platform Status

| Platform | Status | Notes |
|---|---|---|
| Windows | ⚠️ Not yet fully tested | Validation is still ongoing |
| macOS | ✅ Primary target | Receives full development and testing focus |
| Linux | ✅ Primary target | Receives full development and testing focus |

Because this project's first priority is not full Windows support, testing and optimization on Windows are still in progress and compatibility gaps may still exist. If you want to use PCL on Windows today, PCL or PCL-CE is still the safer recommendation.

## Quick Start

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

Useful entry points:

- `PCL.Frontend.Avalonia/`: active desktop frontend
- `PCL.Core.Backend/`: active shared launcher/backend logic and foundation services

## License

- `PCL launcher logic` follows the original custom [https://github.com/PCL-Community/PCL-CE/blob/dev/Plain%20Craft%20Launcher%202/LICENCE](https://github.com/PCL-Community/PCL-CE/blob/dev/Plain%20Craft%20Launcher%202/LICENCE) guide inherited from PCL / PCL-CE.
- `Other independent logic` uses [Apache License 2.0](LICENSE).
