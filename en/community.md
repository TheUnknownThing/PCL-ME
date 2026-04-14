---
layout: default
title: Community
lang: en
permalink: /en/community/
description: Join the PCL-ME community, report issues, read the contribution guide, and track the active development areas.
---

[Home]({{ "/en/" | relative_url }}) · [Downloads]({{ "/en/downloads/" | relative_url }}) · [Build From Source]({{ "/en/build-from-source/" | relative_url }}) · [Community]({{ "/en/community/" | relative_url }}) · [简体中文]({{ "/community/" | relative_url }}) · [繁體中文]({{ "/zh-tw/community/" | relative_url }})

## Get Involved

- [Open an issue](https://github.com/TheUnknownThing/PCL-ME/issues)
- [Contributing guide](https://github.com/TheUnknownThing/PCL-ME/blob/main/CONTRIBUTING.md)
- [Repository home](https://github.com/TheUnknownThing/PCL-ME)

## Contribution Notes

- The active stack is `C# + .NET 10 + Avalonia`.
- Frontend work usually lands in `PCL.Frontend.Avalonia/`, while shared launcher logic lives in `PCL.Core.Backend/`.
- The repository includes backend and foundation tests, and `dotnet test` is the recommended pre-push baseline.
- Cross-platform validation details are especially helpful in pull requests.

## When Reporting Problems

1. Include clear reproduction steps.
2. Describe the actual result and the expected result.
3. Share your operating system, architecture, and .NET environment.
4. Attach logs, screenshots, or diagnostics when available.
