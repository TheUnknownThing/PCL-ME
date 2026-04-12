---
layout: default
title: PCL-ME
lang: zh-CN
permalink: /
description: PCL-ME 是 PCL-CE 的多平台延续版本，使用 Avalonia 构建跨平台桌面启动器。
---

[首页]({{ "/" | relative_url }}) · [下载]({{ "/downloads/" | relative_url }}) · [从源码构建]({{ "/build-from-source/" | relative_url }}) · [社区]({{ "/community/" | relative_url }}) · [English]({{ "/en/" | relative_url }})

PCL-ME 是 PCL-CE 的多平台延续版本，基于 `C# + .NET 10 + Avalonia` 构建，面向 Windows、macOS 和 Linux 提供统一桌面体验。

## 项目特点

- 当前主线为 C#，不再以旧版 WPF 与 VB.NET 前端作为维护方向。
- 活跃桌面前端位于 `PCL.Frontend.Avalonia/`。
- 共享启动器逻辑主要位于 `PCL.Core/` 与 `PCL.Core.Backend/`。
- 项目仍在持续迁移和完善中。

## 平台状态

| 平台 | 状态 | 说明 |
| --- | --- | --- |
| Windows | 持续完善中 | 兼容性与验证仍在推进 |
| macOS | 主要目标平台 | 当前开发与测试重点之一 |
| Linux | 主要目标平台 | 当前开发与测试重点之一 |

如果你现在需要更稳妥的 Windows 优先体验，原始 PCL 或 PCL-CE 仍然可能是更安全的选择；PCL-ME 仍在持续完善中。

## 快速开始

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 常用链接

- [下载页面]({{ "/downloads/" | relative_url }})
- [从源码构建]({{ "/build-from-source/" | relative_url }})
- [GitHub 仓库](https://github.com/TheUnknownThing/PCL-CE)
- [问题反馈](https://github.com/TheUnknownThing/PCL-CE/issues)

## 相关文档

- [简体中文 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_CN.md)
- [English README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
- [繁體中文 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_TW.md)
- [Avalonia 前端说明](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
