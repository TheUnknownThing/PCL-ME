---
layout: default
title: PCL-ME
lang: en
permalink: /en/
description: PCL-ME is the multiplatform continuation of PCL-CE, built with C#, .NET 10, and Avalonia.
---

[Home]({{ "/en/" | relative_url }}) · [Downloads]({{ "/en/downloads/" | relative_url }}) · [Build From Source]({{ "/en/build-from-source/" | relative_url }}) · [Community]({{ "/en/community/" | relative_url }}) · [中文]({{ "/" | relative_url }})

PCL-ME is the multiplatform continuation of PCL-CE, built on `C# + .NET 10 + Avalonia` for a shared desktop experience across Windows, macOS, and Linux.

## Highlights

- The active direction is C#, not the legacy WPF and VB.NET frontend.
- The maintained desktop frontend lives in `PCL.Frontend.Avalonia/`.
- Shared launcher logic mainly lives in `PCL.Core/` and `PCL.Core.Backend/`.
- The project is still actively migrating and stabilizing.

## Platform Status

| Platform | Status | Notes |
| --- | --- | --- |
| Windows | Improving | Compatibility and validation are still in progress |
| macOS | Primary target | Receives focused development and testing |
| Linux | Primary target | Receives focused development and testing |

If you need the safest Windows-first experience today, the original PCL or PCL-CE line may still be the better fit while this port continues to mature.

## Quick Start

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## Useful Links

- [Downloads]({{ "/en/downloads/" | relative_url }})
- [Build from source]({{ "/en/build-from-source/" | relative_url }})
- [GitHub repository](https://github.com/TheUnknownThing/PCL-CE)
- [Issue tracker](https://github.com/TheUnknownThing/PCL-CE/issues)

## Documentation

- [English README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
- [Simplified Chinese README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_CN.md)
- [Traditional Chinese README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_TW.md)
- [Avalonia frontend notes](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
