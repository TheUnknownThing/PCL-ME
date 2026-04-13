---
layout: default
title: PCL-ME
lang: zh-CN
permalink: /
description: PCL-ME 是从 PCL-CE 衍生出来的多平台版本，当前以 C#、.NET 10 与 Avalonia 为主线。
---

[首页]({{ "/" | relative_url }}) · [下载]({{ "/downloads/" | relative_url }}) · [从源码构建]({{ "/build-from-source/" | relative_url }}) · [社区]({{ "/community/" | relative_url }}) · [English]({{ "/en/" | relative_url }}) · [繁體中文]({{ "/zh-tw/" | relative_url }})

PCL-ME 是从 PCL-CE 衍生出来的多平台版本。这个仓库已经完全放弃旧的 VB.NET 前端路径，当前以 C# 为主语言，并使用 Avalonia 构建面向 Windows、macOS 和 Linux 的桌面 UI。

## 项目特点

- 当前活跃代码全部为 C#，不再以 VB.NET / WPF 前端为维护方向。
- 项目运行时与工具链基于 `.NET 10`。
- 前端 UI 使用 Avalonia，为跨平台提供一致的用户体验与原生性能。

## 平台状态

| 平台 | 状态 | 说明 |
| --- | --- | --- |
| Windows | ⚠️ 仍未经过充分测试 | 测试仍在继续推进中 |
| macOS | ✅ 主要目标平台 | 进行充分开发与测试 |
| Linux | ✅ 主要目标平台 | 进行充分开发与测试 |

由于该项目的首要目标并非在 Windows 上提供完整支持，因此 Windows 平台的测试和优化仍在进行中，尚未达到稳定状态，并有可能存在未发现的兼容性问题。若你想要在 Windows 上使用 PCL，仍建议使用 PCL 或者 PCL-CE。

## 快速开始

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

建议先从这些目录了解项目：

- `PCL.Frontend.Avalonia/`：当前正在维护的桌面前端
- `PCL.Core.Backend/`：当前仍在使用的共享启动器与后端逻辑

## 常用链接

- [下载发行版](https://github.com/TheUnknownThing/PCL-CE/releases/latest)
- [提交问题](https://github.com/TheUnknownThing/PCL-CE/issues)
- [贡献指南](https://github.com/TheUnknownThing/PCL-CE/blob/dev/CONTRIBUTING.md)
- [Avalonia 前端文档](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
- [GitHub 仓库](https://github.com/TheUnknownThing/PCL-CE)

## 许可证

- `PCL 启动器逻辑` 使用与原始 PCL / PCL-CE 一致的 [自定义许可证指南](https://github.com/PCL-Community/PCL-CE/blob/dev/Plain%20Craft%20Launcher%202/LICENCE)。
- `其他独立逻辑` 使用 [Apache License 2.0](https://github.com/TheUnknownThing/PCL-CE/blob/dev/LICENSE)。

## 相关文档

- [简体中文 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README.md)
- [English README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
- [繁體中文 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_TW.md)
- [Avalonia 前端说明](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
