---
layout: default
title: Downloads
lang: en
permalink: /en/downloads/
description: Download the latest PCL-ME builds and review the platform status and project layout described in the README.
---

[Home]({{ "/en/" | relative_url }}) · [Downloads]({{ "/en/downloads/" | relative_url }}) · [Build From Source]({{ "/en/build-from-source/" | relative_url }}) · [Community]({{ "/en/community/" | relative_url }}) · [简体中文]({{ "/downloads/" | relative_url }}) · [繁體中文]({{ "/zh-tw/downloads/" | relative_url }})

## Recommended Download Path

- [Latest release](https://github.com/TheUnknownThing/PCL-ME/releases/latest)
- [All releases](https://github.com/TheUnknownThing/PCL-ME/releases)
- [Source repository](https://github.com/TheUnknownThing/PCL-ME)

## Current Platform Status

The current README summarizes platform readiness like this:

| Platform | Status | Notes |
| --- | --- | --- |
| Windows | In progress | Supported, but still receives less validation than macOS/Linux |
| macOS | Primary target | Actively developed and tested |
| Linux | Primary target | Actively developed and tested |

## Before You Download

- The active application is built with C#, .NET 10, and Avalonia.
- The maintained desktop frontend lives in `PCL.Frontend.Avalonia/`, and shared launcher logic lives in `PCL.Core.Backend/`.
- If you need implementation details, tests, or platform notes, the repository README and module documentation are the best source of truth.

## Learn More

- [English README](https://github.com/TheUnknownThing/PCL-ME/blob/main/README-EN.md)
- [Issue tracker](https://github.com/TheUnknownThing/PCL-ME/issues)
- [Build from source]({{ "/en/build-from-source/" | relative_url }})
- [Community and contributing]({{ "/en/community/" | relative_url }})
