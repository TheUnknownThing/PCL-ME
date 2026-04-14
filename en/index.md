---
layout: default
title: PCL-ME
lang: en
permalink: /en/
description: PCL-ME is a cross-platform launcher for Windows, macOS, and Linux, built with C#, .NET 10, and Avalonia.
---

[Home]({{ "/en/" | relative_url }}) · [Downloads]({{ "/en/downloads/" | relative_url }}) · [Build From Source]({{ "/en/build-from-source/" | relative_url }}) · [Community]({{ "/en/community/" | relative_url }}) · [简体中文]({{ "/" | relative_url }}) · [繁體中文]({{ "/zh-tw/" | relative_url }})

PCL-ME is a hard fork of upstream PCL-CE, focused on a maintained and sustainable cross-platform launcher stack. The active application is built with C#, .NET 10, and Avalonia for Windows, macOS, and Linux.

## Project Layout

- `PCL.Frontend.Avalonia/`: maintained desktop frontend and UI assets
- `PCL.Core.Backend/`: shared launcher logic, backend workflows, and foundation services
- `PCL.Core.Backend.Test/`: backend regression tests
- `PCL.Core.Backend.Foundation.Test/`: portability and foundation tests

## Platform Status

| Platform | Status | Notes |
| --- | --- | --- |
| Windows | In progress | Supported, but still receives less validation than macOS/Linux |
| macOS | Primary target | Actively developed and tested |
| Linux | Primary target | Actively developed and tested |

## Quick Start

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## Useful Links

- [Releases](https://github.com/TheUnknownThing/PCL-ME/releases/latest)
- [Issues](https://github.com/TheUnknownThing/PCL-ME/issues)
- [Contributing](https://github.com/TheUnknownThing/PCL-ME/blob/main/CONTRIBUTING.md)
- [Avalonia Frontend Docs](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Frontend.Avalonia/README.md)
- [Backend Docs](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Core.Backend/README.md)
- [GitHub repository](https://github.com/TheUnknownThing/PCL-ME)

## License

PCL-ME uses a split license model:

- UI contents under `PCL.Frontend.Avalonia/` follow the custom license in [LICENCE](https://github.com/TheUnknownThing/PCL-ME/blob/main/LICENCE).
- All other launcher-related logic in this repository follows [Apache License 2.0](https://github.com/TheUnknownThing/PCL-ME/blob/main/LICENSE), unless a file states otherwise.
