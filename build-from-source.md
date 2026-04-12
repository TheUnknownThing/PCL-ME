---
layout: page
title: Build From Source
permalink: /build-from-source/
---

## Requirements

- `.NET 10 SDK`
- A supported desktop environment for Avalonia
- Git

## Quick Start

```bash
git clone https://github.com/TheUnknownThing/PCL-CE.git
cd PCL-CE
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## Notes

- The active frontend is `PCL.Frontend.Avalonia/`.
- The old WPF frontend is not the maintained target in this repository.
- Tests can be run with `dotnet test`.

## Useful Paths

- `PCL.Frontend.Avalonia/`
- `PCL.Core/`
- `PCL.Core.Backend/`

## More Documentation

- [Avalonia frontend README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
- [Repository README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
