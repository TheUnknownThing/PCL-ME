---
layout: default
title: 从源码构建
lang: zh-CN
permalink: /build-from-source/
description: 了解如何使用 .NET 10 从源码构建、运行并测试 PCL-ME。
---

[首页]({{ "/" | relative_url }}) · [下载]({{ "/downloads/" | relative_url }}) · [从源码构建]({{ "/build-from-source/" | relative_url }}) · [社区]({{ "/community/" | relative_url }}) · [English]({{ "/en/build-from-source/" | relative_url }}) · [繁體中文]({{ "/zh-tw/build-from-source/" | relative_url }})

## 环境要求

- Git
- `.NET 10 SDK`
- 可运行 Avalonia 的桌面环境

## 快速开始

```bash
git clone https://github.com/TheUnknownThing/PCL-ME.git
cd PCL-ME
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 测试

```bash
dotnet test
```

仓库当前包含后端回归测试与基础层测试；提交变更前，建议至少先跑完整测试集确认没有回归。

## 重点目录

- `PCL.Frontend.Avalonia/`：当前维护中的桌面前端与 UI 资源
- `PCL.Core.Backend/`：共享启动器逻辑、后端工作流与基础服务
- `PCL.Core.Backend.Test/`：后端回归测试
- `PCL.Core.Backend.Foundation.Test/`：可移植性与基础层测试

## 相关文档

- [简体中文 README](https://github.com/TheUnknownThing/PCL-ME/blob/main/README-ZH_CN.md)
- [Avalonia 前端 README](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Frontend.Avalonia/README.md)
- [后端 README](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Core.Backend/README.md)
