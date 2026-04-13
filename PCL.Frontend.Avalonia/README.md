# PCL.Frontend.Avalonia

`PCL.Frontend.Avalonia` 是当前仓库正在维护的桌面前端。它是 PCL-ME 的 C# + Avalonia UI。

## 当前状态

- 该项目是当前仓库中的正式前端实现
- 它可以作为 `.NET 10` 桌面应用正常构建和运行
- 提供 macOS、Linux、Windows 的打包脚本

## 前置要求

- 来自 [`../global.json`](../global.json) 的 .NET SDK `10.0.100`
- 已完成还原的仓库工作区
- 若要打包：
  - 需要 `zip`、`tar`、`sed`、`dotnet`
  - 在 macOS 上还需要 `iconutil` 和 `sips`

## 快速开始

在仓库根目录运行：

```bash
dotnet restore
dotnet build PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj
```

不带参数运行时，会直接启动桌面应用。也可以显式使用 `app` 启动：

```bash
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

如果只想查看当前保留的命令入口：

```bash
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- help
```

## 支持的命令

- `app`
- `help`

`app` 支持的参数：

- `--scenario modern-fabric|legacy-forge`
- `--force-cjk-font-warning true|false`

默认行为：

- 不带参数：启动桌面应用
- `app`：启动桌面应用
- 默认场景：`modern-fabric`

## 打包

在仓库根目录使用打包脚本：

```bash
./PCL.Frontend.Avalonia/scripts/package-frontend.sh
```

默认值：

- 目标 RID：`osx-arm64`、`linux-x64`、`win-x64`
- 输出目录：`artifacts/frontend-packages/`
- 发布模式：self-contained

如果需要覆盖版本号或目标列表：

```bash
APP_VERSION=2026.04.13 ./PCL.Frontend.Avalonia/scripts/package-frontend.sh osx-arm64
```

各平台打包行为：

- macOS：生成 `PCL-ME.app` 和 zip 压缩包
- Linux：生成包含已发布前端、启动脚本、图标和 `.desktop` 文件的 tarball
- Windows：生成包含已发布前端和 `Launch PCL-ME.vbs` 的 zip 压缩包

所有打包出来的启动器现在都会通过受支持的 `app` 入口启动 Avalonia 应用。

## 故障排查

### Wayland 或 `niri` 下字体过小

在部分 Linux 环境里，Avalonia 可能会把缩放因子识别成 `1.0`，从而导致当前 UI 的固定字号显示过小。你可以先查看输出名称：

```bash
niri msg outputs
```

然后显式指定缩放启动应用：

```bash
AVALONIA_SCREEN_SCALE_FACTORS='eDP-1=1.5' dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

如果你有配置有多块显示器：

```bash
AVALONIA_SCREEN_SCALE_FACTORS='eDP-1=1.5;HDMI-A-1=1.0' dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```
