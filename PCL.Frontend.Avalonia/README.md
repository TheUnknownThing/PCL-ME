# PCL.Frontend.Avalonia

`PCL.Frontend.Avalonia` 是 PCL-ME 当前正在维护的桌面前端。这里包含现行 Avalonia 应用、UI 资源、前端组合逻辑，以及用于发布桌面应用的前端入口。

## 范围

这个项目主要包含：

- Avalonia 视图与控件
- 样式、图标、字体与其他前端资源
- 前端 ViewModel 与 UI 组合逻辑
- 前端应用的桌面打包脚本与启动入口

## 构建

在仓库根目录运行：

```bash
dotnet restore
dotnet build PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

常用命令：

- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app`
- `dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- help`
- `./PCL.Frontend.Avalonia/scripts/package-frontend.sh`

## 打包

打包脚本会为受支持的桌面目标发布前端构建，并将产物写入 `artifacts/frontend-packages/`。

例如：

```bash
APP_VERSION=2026.04.14 ./PCL.Frontend.Avalonia/scripts/package-frontend.sh osx-arm64
```

## 故障排查

### Wayland 或 `niri` 下字体过小

在部分 Linux 环境里，Avalonia 可能会把缩放因子识别成 `1.0`，从而导致当前 UI 的固定字号显示过小。你可以先查看输出名称：

通常可以先在启动器的「设置 - 个性化 - 基本 - 界面大小」里调整全局 UI 缩放；该设置会在下次启动时生效。

```bash
niri msg outputs
```

如果需要按显示器分别覆盖缩放，再显式指定缩放启动应用：

```bash
AVALONIA_SCREEN_SCALE_FACTORS='eDP-1=1.5' dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

如果你配置了多块显示器：

```bash
AVALONIA_SCREEN_SCALE_FACTORS='eDP-1=1.5;HDMI-A-1=1.0' dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 许可证

本目录下的 UI 内容遵循本目录中的 [LICENSE](LICENSE) 自定义许可证。

仓库中位于此前端 UI 层之外的启动器相关逻辑遵循 [../LICENSE](../LICENSE) 中的 Apache License 2.0，除非某个文件另有说明。

资源致谢：

- 当前应用 Logo 基于 [PCL-Community/PCL-CE-Logo](https://github.com/PCL-Community/PCL-CE-Logo) 仓库中的素材；该源仓库使用 Apache License 2.0。
- 为了保持多平台环境下的界面字体一致性，本目录在 `Assets/Fonts/` 中随附并使用了 [HarmonyOS Sans](https://developer.huawei.com/consumer/en/design/resource/) 字体文件。HarmonyOS Sans 版权归 Huawei Device Co., Ltd. 所有，按 HarmonyOS Sans Fonts License Agreement 提供，特此致谢。
