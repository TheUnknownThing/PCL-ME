[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Frontend.Avalonia/Assets/icon.png" alt="PCL-ME logo" width="80" height="80">

# PCL Multiplatform Edition

[Releases](https://github.com/TheUnknownThing/PCL-ME/releases/latest) |
[Issues](https://github.com/TheUnknownThing/PCL-ME/issues) |
[Contributing](CONTRIBUTING.md) |
[Avalonia Frontend Docs](PCL.Frontend.Avalonia/README.md) |
[Backend Docs](PCL.Core.Backend/README.md)

</div>

PCL-ME is a hard fork of the upstream PCL-CE edition, focused on a maintained cross-platform launcher stack. The active application is built with C#, .NET 10, and Avalonia for Windows, macOS, and Linux.

## Project Layout

- `PCL.Frontend.Avalonia/`: maintained desktop frontend and UI assets
- `PCL.Core.Backend/`: shared launcher logic, backend workflows, and foundation services
- `PCL.Core.Backend.Test/`: backend regression tests
- `PCL.Core.Backend.Foundation.Test/`: portability and foundation tests

## Quick Start

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## Platform Notes

| Platform | Status | Notes |
|---|---|---|
| Windows | In progress | Supported, but still receives less validation than macOS/Linux |
| macOS | Primary target | Actively developed and tested |
| Linux | Primary target | Actively developed and tested |

## License

PCL-ME uses a split license model:

- UI contents under `PCL.Frontend.Avalonia/` follow the custom license in [PCL.Frontend.Avalonia/LICENSE](PCL.Frontend.Avalonia/LICENSE).
- All other launcher-related logic in this repository follows [Apache License 2.0](LICENSE), unless a file states otherwise.

Asset credits:

- The current application logo is based on assets from [PCL-Community/PCL-CE-Logo](https://github.com/PCL-Community/PCL-CE-Logo); that source repository is licensed under Apache License 2.0.
- To keep UI typography consistent across platforms, the project bundles and uses [HarmonyOS Sans](https://developer.huawei.com/consumer/en/design/resource/) font files in `PCL.Frontend.Avalonia/Assets/Fonts/`. HarmonyOS Sans is copyrighted by Huawei Device Co., Ltd. and provided under the HarmonyOS Sans Fonts License Agreement.

If you are unsure which terms apply to a file, start with the folder it lives in and then check the nearest license notice in that subtree.
