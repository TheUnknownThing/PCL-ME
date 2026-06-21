[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Frontend.Avalonia/Assets/icon.png" alt="PCL-ME logo" width="80" height="80">

# PCL Multiplatform Edition

A cross-platform Minecraft launcher for Windows, macOS, and Linux.

[⬇ Download Latest](https://github.com/TheUnknownThing/PCL-ME/releases/latest) ·
[🐛 Report an Issue](https://github.com/TheUnknownThing/PCL-ME/issues) ·
[📖 Contributing Guide](CONTRIBUTING.md)

</div>

## About PCL-ME

PCL-ME aims to bring the familiar PCL experience to Windows, macOS, and Linux, with long-term maintenance in mind.

## Screenshots

<table>
<tr>
<td><img src="docs/screenshots/launch.png" alt="Launch page"></td>
<td><img src="docs/screenshots/download.png" alt="Download page"></td>
<td><img src="docs/screenshots/settings.png" alt="Settings page"></td>
</tr>
<tr>
<td align="center">Launching the game</td>
<td align="center">Browsing mods and modpacks</td>
<td align="center">Interface and theme settings</td>
</tr>
</table>

## Platform Support

| Platform | Status |
|---|---|
| 🐧 Linux | Primary target, actively developed and tested |
| 🍎 macOS | Primary target, actively developed and tested |
| 🪟 Windows | Supported, but receives less testing than the above |

## Installation

- **Windows / macOS**: Grab the installer for your platform from the [Releases page](https://github.com/TheUnknownThing/PCL-ME/releases/latest).
- **Linux**: In addition to the Releases artifacts, Arch Linux users can install from the AUR:

  ```bash
  yay -S pcl-me-bin
  ```

---

## For Developers

If you'd like to build from source or contribute:

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

See the [Avalonia frontend docs](PCL.Frontend.Avalonia/README.md) and [backend docs](PCL.Core.Backend/README.md) for more details, and refer to [CONTRIBUTING.md](CONTRIBUTING.md) for the contribution workflow.

> PCL-ME was forked from [PCL-CE](https://github.com/PCL-Community/PCL-CE) and is now maintained as an independent branch. Tech stack: C# + .NET 10 + Avalonia.

## License

PCL-ME is licensed per directory:

- UI-related content under `PCL.Frontend.Avalonia/` follows its [custom license](PCL.Frontend.Avalonia/LICENSE).
- The rest of the repository follows the [Apache License 2.0](LICENSE).

If you are unsure which terms apply to a particular file, refer to the license notice in its directory or parent directories.

#### Asset Credits

- The application icon is based on assets from [PCL-Community/PCL-CE-Logo](https://github.com/PCL-Community/PCL-CE-Logo) (Apache 2.0).
- The UI uses [HarmonyOS Sans](https://developer.huawei.com/consumer/en/design/resource/), copyrighted by Huawei Device Co., Ltd. and licensed under the HarmonyOS Sans Fonts License Agreement. We gratefully acknowledge their work.
