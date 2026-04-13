---
layout: default
title: 从源码构建
lang: zh-CN
permalink: /build-from-source/
description: 了解如何使用 .NET 10 从源码构建和运行 PCL-ME。
---

[首页]({{ "/" | relative_url }}) · [下载]({{ "/downloads/" | relative_url }}) · [从源码构建]({{ "/build-from-source/" | relative_url }}) · [社区]({{ "/community/" | relative_url }}) · [English]({{ "/en/build-from-source/" | relative_url }}) · [繁體中文]({{ "/zh-tw/build-from-source/" | relative_url }})

## 环境要求

- `.NET 10 SDK`
- 可运行 Avalonia 的桌面环境
- Git

## 快速开始

```bash
git clone https://github.com/TheUnknownThing/PCL-CE.git
cd PCL-CE
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 说明

- 当前正在维护的前端目录为 `PCL.Frontend.Avalonia/`。
- 旧版 WPF 前端不再是本仓库的开发目标。
- 当前主线代码以 C# 和 `.NET 10` 为基础。
- 测试可通过 `dotnet test` 运行。

## 常用路径

- `PCL.Frontend.Avalonia/`
- `PCL.Core.Backend/`

## 更多文档

- [Avalonia 前端 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
- [仓库 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README.md)
