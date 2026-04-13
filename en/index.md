---
layout: default
title: PCL-ME
lang: en
permalink: /en/
description: PCL-ME is a multiplatform edition derived from PCL-CE, now centered on C#, .NET 10, and Avalonia.
---

[Home]({{ "/en/" | relative_url }}) · [Downloads]({{ "/en/downloads/" | relative_url }}) · [Build From Source]({{ "/en/build-from-source/" | relative_url }}) · [Community]({{ "/en/community/" | relative_url }}) · [简体中文]({{ "/" | relative_url }}) · [繁體中文]({{ "/zh-tw/" | relative_url }})

PCL-ME is a multiplatform edition derived from PCL-CE. This repository fully drops the old VB.NET frontend path and continues the launcher in C# with an Avalonia desktop UI that targets Windows, macOS, and Linux.

## Highlights

- The active codebase is C# only; the legacy VB.NET / WPF frontend is no longer the maintained direction here.
- The project targets `.NET 10`.
- The frontend UI uses Avalonia to provide a consistent cross-platform experience with native performance.

## Platform Status

| Platform | Status | Notes |
| --- | --- | --- |
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

## Useful Links

- [Releases](https://github.com/TheUnknownThing/PCL-CE/releases/latest)
- [Issues](https://github.com/TheUnknownThing/PCL-CE/issues)
- [Contributing](https://github.com/TheUnknownThing/PCL-CE/blob/dev/CONTRIBUTING.md)
- [Avalonia Frontend Docs](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
- [GitHub repository](https://github.com/TheUnknownThing/PCL-CE)

## License

- `PCL launcher logic` follows the original custom [license guide](https://github.com/PCL-Community/PCL-CE/blob/dev/Plain%20Craft%20Launcher%202/LICENCE) inherited from PCL / PCL-CE.
- `Other independent logic` uses [Apache License 2.0](https://github.com/TheUnknownThing/PCL-CE/blob/dev/LICENSE).

## Documentation

- [English README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
- [Simplified Chinese README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README.md)
- [Traditional Chinese README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_TW.md)
- [Avalonia frontend notes](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
