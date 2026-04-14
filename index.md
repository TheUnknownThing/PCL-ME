---
layout: default
title: PCL-ME
lang: zh-CN
permalink: /
description: PCL-ME 是一套面向 Windows、macOS 和 Linux 的跨平台启动器，当前主线基于 C#、.NET 10 与 Avalonia。
---

[首页]({{ "/" | relative_url }}) · [下载]({{ "/downloads/" | relative_url }}) · [从源码构建]({{ "/build-from-source/" | relative_url }}) · [社区]({{ "/community/" | relative_url }}) · [English]({{ "/en/" | relative_url }}) · [繁體中文]({{ "/zh-tw/" | relative_url }})

PCL-ME 是上游 PCL-CE 的硬分叉版本，目标是维护一套真正可持续的跨平台启动器。当前主线实现基于 C#、.NET 10 与 Avalonia，面向 Windows、macOS 和 Linux。

## 项目结构

- `PCL.Frontend.Avalonia/`：当前维护中的桌面前端与 UI 资源
- `PCL.Core.Backend/`：共享启动器逻辑、后端工作流与基础服务
- `PCL.Core.Backend.Test/`：后端回归测试
- `PCL.Core.Backend.Foundation.Test/`：可移植性与基础层测试

## 平台状态

| 平台 | 状态 | 说明 |
| --- | --- | --- |
| Windows | 持续完善中 | 已支持，但验证强度仍低于 macOS / Linux |
| macOS | 主要目标平台 | 持续开发与测试 |
| Linux | 主要目标平台 | 持续开发与测试 |

## 快速开始

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 常用链接

- [下载发行版](https://github.com/TheUnknownThing/PCL-ME/releases/latest)
- [提交问题](https://github.com/TheUnknownThing/PCL-ME/issues)
- [贡献指南](https://github.com/TheUnknownThing/PCL-ME/blob/main/CONTRIBUTING.md)
- [Avalonia 前端文档](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Frontend.Avalonia/README.md)
- [后端文档](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Core.Backend/README.md)
- [GitHub 仓库](https://github.com/TheUnknownThing/PCL-ME)

## 许可证

PCL-ME 使用分层授权模型：

- `PCL.Frontend.Avalonia/` 下的 UI 内容遵循仓库根目录中的 [LICENCE](https://github.com/TheUnknownThing/PCL-ME/blob/main/LICENCE) 自定义许可证。
- 本仓库中其余启动器相关逻辑遵循 [Apache License 2.0](https://github.com/TheUnknownThing/PCL-ME/blob/main/LICENSE)，除非某个文件或子目录另有说明。
