**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Frontend.Avalonia/Assets/icon.png" alt="PCL-ME logo" width="80" height="80">

# PCL-ME

Plain Craft Launcher Multiplatform Edition

[下载发行版](https://github.com/TheUnknownThing/PCL-ME/releases/latest) |
[提交问题](https://github.com/TheUnknownThing/PCL-ME/issues) |
[贡献指南](CONTRIBUTING.md) |
[Avalonia 前端文档](PCL.Frontend.Avalonia/README.md) |
[后端文档](PCL.Core.Backend/README.md)

</div>

PCL-ME 是上游 PCL-CE 的硬分叉版本，当前目标是维护一套真正可持续的跨平台启动器。现行主线实现基于 C#、.NET 10 与 Avalonia，面向 Windows、macOS 和 Linux。

## 项目结构

- `PCL.Frontend.Avalonia/`：当前维护中的桌面前端与 UI 资源
- `PCL.Core.Backend/`：共享启动器逻辑、后端工作流与基础服务
- `PCL.Core.Backend.Test/`：后端回归测试
- `PCL.Core.Backend.Foundation.Test/`：可移植性与基础层测试

## 快速开始

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 平台说明

| 平台 | 状态 | 说明 |
|---|---|---|
| Windows | 持续完善中 | 已支持，但验证强度仍低于 macOS / Linux |
| macOS | 主要目标平台 | 持续开发与测试 |
| Linux | 主要目标平台 | 持续开发与测试 |

## 许可证

PCL-ME 使用分层授权模型：

- `PCL.Frontend.Avalonia/` 下的 UI 内容遵循 [PCL.Frontend.Avalonia/LICENSE](PCL.Frontend.Avalonia/LICENSE) 自定义许可证。
- 本仓库中其余启动器相关逻辑遵循 [Apache License 2.0](LICENSE)，除非某个文件或子目录另有说明。

如果你不确定某个文件适用哪套条款，请先看它所在目录，再检查该目录附近的许可证说明。
