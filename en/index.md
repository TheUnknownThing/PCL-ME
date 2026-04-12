---
layout: default
title: PCL-ME
lang: en
nav_key: home
permalink: /en/
alt_url: /
alt_label: 中文
description: PCL-ME is the multiplatform continuation of PCL-CE, built with C#, .NET 10, and Avalonia.
hero_eyebrow: Multiplatform Minecraft Launcher
hero_title: PCL-ME
hero_lead: The multiplatform continuation of PCL-CE, built on C#, .NET 10, and Avalonia for a shared desktop experience across Windows, macOS, and Linux.
primary_action_label: Download Latest Release
primary_action_url: https://github.com/TheUnknownThing/PCL-CE/releases/latest
secondary_action_label: View Repository
secondary_action_url: https://github.com/TheUnknownThing/PCL-CE
---

## Overview

<div class="card-grid">
  <section class="card">
    <h3>Unified stack</h3>
    <p>The active direction is <code>C# + .NET 10 + Avalonia</code>, rather than the legacy WPF and VB.NET frontend.</p>
  </section>
  <section class="card">
    <h3>Desktop on every platform</h3>
    <p>The same frontend targets Windows, macOS, and Linux, with macOS and Linux currently receiving the most validation focus.</p>
  </section>
  <section class="card">
    <h3>Migration in progress</h3>
    <p>The repository is steadily moving launcher capabilities into the new desktop frontend and shared core modules.</p>
  </section>
</div>

## Platform Status

| Platform | Status | Notes |
| --- | --- | --- |
| Windows | Improving | Compatibility and validation are still in progress |
| macOS | Primary target | Receives focused development and testing |
| Linux | Primary target | Receives focused development and testing |

If you need the safest Windows-first experience today, the original PCL or PCL-CE line may still be the better fit while this port continues to mature.

## Quick Links

- [Downloads]({{ "/en/downloads/" | relative_url }})
- [Build from source]({{ "/en/build-from-source/" | relative_url }})
- [GitHub repository](https://github.com/TheUnknownThing/PCL-CE)
- [Issue tracker](https://github.com/TheUnknownThing/PCL-CE/issues)

## Repository Structure

- `PCL.Frontend.Avalonia/`: actively maintained desktop frontend
- `PCL.Core/` and `PCL.Core.Backend/`: shared launcher and backend logic
- Additional projects mainly support infrastructure, tests, and reusable components

## Documentation

- [English README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
- [Simplified Chinese README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_CN.md)
- [Traditional Chinese README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_TW.md)
- [Avalonia frontend notes](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
