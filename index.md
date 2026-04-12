---
layout: page
title: PCL-ME
permalink: /
---

PCL-ME is the multiplatform continuation of PCL-CE. It keeps the launcher on a modern `C# + .NET 10 + Avalonia` stack and targets Windows, macOS, and Linux with a shared desktop UI.

## Highlights

- Cross-platform desktop frontend built with Avalonia.
- Active codebase is C# only.
- Legacy WPF and VB.NET code is not the maintained path in this repository.
- Shared launcher logic lives in reusable core projects.

## Platform Status

| Platform | Status | Notes |
| --- | --- | --- |
| Windows | In progress | Validation is still ongoing |
| macOS | Primary target | Receives full development and testing focus |
| Linux | Primary target | Receives full development and testing focus |

If you need the safest Windows-first experience today, the original PCL or PCL-CE line may still be the better choice while this port continues to mature.

## Quick Links

- [Download the latest release](https://github.com/TheUnknownThing/PCL-CE/releases/latest)
- [Build from source]({{ "/build-from-source/" | relative_url }})
- [Browse the repository](https://github.com/TheUnknownThing/PCL-CE)
- [Open issues](https://github.com/TheUnknownThing/PCL-CE/issues)

## Project Structure

- `PCL.Frontend.Avalonia/` is the active desktop frontend.
- `PCL.Core/` and `PCL.Core.Backend/` contain shared launcher and backend logic.
- The repository also includes tests and supporting libraries used by the Avalonia-based launcher.

## Documentation

- [English README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
- [Simplified Chinese README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_CN.md)
- [Traditional Chinese README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_TW.md)
- [Avalonia frontend notes](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
