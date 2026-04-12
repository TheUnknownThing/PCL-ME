---
layout: default
title: Build From Source
lang: en
nav_key: build
permalink: /en/build-from-source/
alt_url: /build-from-source/
alt_label: 中文
description: Learn how to build and run PCL-ME from source with .NET 10.
hero_eyebrow: Development and debugging
hero_title: Build From Source
hero_lead: If you want to develop PCL-ME or run the latest code locally, you can build the project directly with .NET 10 and start the Avalonia frontend.
primary_action_label: View Repository
primary_action_url: https://github.com/TheUnknownThing/PCL-CE
secondary_action_label: Avalonia Frontend Notes
secondary_action_url: https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md
---

## Requirements

- `.NET 10 SDK`
- A desktop environment supported by Avalonia
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

- The active frontend lives in `PCL.Frontend.Avalonia/`.
- The old WPF frontend is no longer the maintained target in this repository.
- You can run tests with `dotnet test`.

## Useful Paths

- `PCL.Frontend.Avalonia/`
- `PCL.Core/`
- `PCL.Core.Backend/`

## More Documentation

- [Avalonia frontend README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
- [Repository README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
