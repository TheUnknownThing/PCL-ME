**English** · [简体中文](README-ZH_CN.md) · [繁體中文](README-ZH_TW.md)

<p align="center">
  <img src="PCL.Frontend.Avalonia/Assets/icon.png" alt="PCL-ME logo" width="112" height="112">
</p>

<h1 align="center">PCL Multiplatform Edition</h1>

<p align="center">
  The familiar PCL experience, across Windows, macOS, and Linux.
</p>

<p align="center">
  <a href="#get-started"><img src="https://img.shields.io/badge/Platforms-Windows_%C2%B7_macOS_%C2%B7_Linux-3D948B?style=flat-square" alt="Available for Windows, macOS, and Linux"></a>
  <a href="https://github.com/TheUnknownThing/PCL-ME/actions/workflows/build-test.yml"><img src="https://img.shields.io/github/actions/workflow/status/TheUnknownThing/PCL-ME/build-test.yml?branch=main&amp;style=flat-square&amp;label=CI" alt="CI status"></a>
  <a href="https://github.com/TheUnknownThing/PCL-ME/releases"><img src="https://img.shields.io/github/v/release/TheUnknownThing/PCL-ME?include_prereleases&amp;style=flat-square&amp;label=Release" alt="Latest release version, including prereleases"></a>
</p>

<p align="center">
  <a href="https://github.com/TheUnknownThing/PCL-ME/releases/latest"><strong>Download</strong></a> ·
  <a href="https://github.com/TheUnknownThing/PCL-ME/issues">Report an issue</a> ·
  <a href="CONTRIBUTING.md">Contribute</a>
</p>

PCL-ME is a Minecraft launcher that brings Plain Craft Launcher's familiar workflow to all three desktop platforms. Built with C#, .NET 10, and Avalonia, it continues [PCL-CE](https://github.com/PCL-Community/PCL-CE) as an independently maintained fork.

- **Launch** — manage game instances and launch Minecraft from one place.
- **Explore** — browse mods, modpacks, resource packs, and shaders for your next playthrough.
- **Personalize** — adjust themes, color palettes, language, and interface scale to suit your desktop.

## A look inside

<table align="center" width="760">
  <tr>
    <td colspan="2" align="center">
      <a href="docs/screenshots/launch.png"><img src="docs/screenshots/launch.png" alt="PCL-ME launch page with account controls and the selected Minecraft instance" width="720"></a>
      <p>Your account, game instances, and launch controls in one place.</p>
    </td>
  </tr>
  <tr>
    <td width="50%" align="center" valign="top">
      <a href="docs/screenshots/download.png"><img src="docs/screenshots/download.png" alt="Mod browser with Minecraft version, loader, source, and tag filters" width="340"></a>
      <p><strong>Find your next mod</strong><br>
      Browse community resources<br>
      with version and loader filters.</p>
    </td>
    <td width="50%" align="center" valign="top">
      <a href="docs/screenshots/settings.png"><img src="docs/screenshots/settings.png" alt="Interface settings showing the dark theme, color palettes, language, and UI scale" width="340"></a>
      <p><strong>Make it feel like home</strong><br>
      Choose your theme, palette,<br>
      language, and interface scale.</p>
    </td>
  </tr>
</table>

## Get started

Download the package for your operating system and architecture from the [latest release](https://github.com/TheUnknownThing/PCL-ME/releases/latest).

| Platform | Support |
| --- | --- |
| Windows | Supported; receives less testing than Linux and macOS. |
| macOS | Primary platform; actively developed and tested. |
| Linux | Primary platform; actively developed and tested. |

On Arch Linux, install the AUR package with an AUR helper such as `yay`:

```sh
yay -S pcl-me-bin
```

## Build from source

Install the **.NET 10 SDK**, then run these commands from the repository root:

```sh
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

The codebase separates the desktop interface from the shared launcher services:

| Project | Responsibility |
| --- | --- |
| [PCL.Frontend.Avalonia](PCL.Frontend.Avalonia/README.md) | Desktop UI, themes, assets, and application packaging. |
| [PCL.Core.Backend](PCL.Core.Backend/README.md) | Shared launcher services, Minecraft launch workflows, downloads, and platform utilities. |

Run `dotnet test` before submitting code changes. The project documentation above covers more specific build, test, packaging, and troubleshooting workflows.

## Contributing

Bug reports, documentation improvements, and code contributions are welcome. Read the [contribution guide](CONTRIBUTING.md) for the development workflow and submission requirements.

When reporting a bug, include your operating system, launcher and game versions, reproduction steps, and logs. For a new feature, open a [feature issue](https://github.com/TheUnknownThing/PCL-ME/issues/new?template=3-feature.yml) and agree on the direction with a maintainer before submitting an implementation.

## Licensing & credits

Licensing depends on the directory:

- UI-related content under `PCL.Frontend.Avalonia/` is covered by the frontend's [custom license](PCL.Frontend.Avalonia/LICENSE).
- The rest of the repository is covered by [Apache License 2.0](LICENSE), unless a file states otherwise.

Check the license notice in a file's directory or parent directories for the applicable terms.

PCL-ME builds on the work of [PCL-CE](https://github.com/PCL-Community/PCL-CE). The application icon is based on [PCL-Community/PCL-CE-Logo](https://github.com/PCL-Community/PCL-CE-Logo), licensed under Apache 2.0. The interface uses [HarmonyOS Sans](https://developer.huawei.com/consumer/en/design/resource/), copyright Huawei Device Co., Ltd., under the HarmonyOS Sans Fonts License Agreement.
