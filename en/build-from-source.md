---
layout: default
title: Build From Source
lang: en
permalink: /en/build-from-source/
description: Learn how to build and run PCL-ME from source with .NET 10.
---

[Home]({{ "/en/" | relative_url }}) · [Downloads]({{ "/en/downloads/" | relative_url }}) · [Build From Source]({{ "/en/build-from-source/" | relative_url }}) · [Community]({{ "/en/community/" | relative_url }}) · [简体中文]({{ "/build-from-source/" | relative_url }}) · [繁體中文]({{ "/zh-tw/build-from-source/" | relative_url }})

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
- The current mainline stack is based on C# and `.NET 10`.
- You can run tests with `dotnet test`.

## Useful Paths

- `PCL.Frontend.Avalonia/`
- `PCL.Core.Backend/`

## More Documentation

- [Avalonia frontend README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
- [Repository README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
