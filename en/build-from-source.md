---
layout: default
title: Build From Source
lang: en
permalink: /en/build-from-source/
description: Learn how to build, run, and test PCL-ME from source with .NET 10.
---

[Home]({{ "/en/" | relative_url }}) · [Downloads]({{ "/en/downloads/" | relative_url }}) · [Build From Source]({{ "/en/build-from-source/" | relative_url }}) · [Community]({{ "/en/community/" | relative_url }}) · [简体中文]({{ "/build-from-source/" | relative_url }}) · [繁體中文]({{ "/zh-tw/build-from-source/" | relative_url }})

## Requirements

- Git
- `.NET 10 SDK`
- A desktop environment supported by Avalonia

## Quick Start

```bash
git clone https://github.com/TheUnknownThing/PCL-ME.git
cd PCL-ME
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## Tests

```bash
dotnet test
```

The repository includes backend regression tests and foundation-level tests. Running the full test suite before you push changes is the safest baseline.

## Important Directories

- `PCL.Frontend.Avalonia/`: maintained desktop frontend and UI assets
- `PCL.Core.Backend/`: shared launcher logic, backend workflows, and foundation services
- `PCL.Core.Backend.Test/`: backend regression tests
- `PCL.Core.Backend.Foundation.Test/`: portability and foundation tests

## Documentation

- [English README](https://github.com/TheUnknownThing/PCL-ME/blob/main/README-EN.md)
- [Avalonia frontend README](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Frontend.Avalonia/README.md)
- [Backend README](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Core.Backend/README.md)
