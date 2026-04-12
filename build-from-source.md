---
layout: default
title: 从源码构建
lang: zh-CN
nav_key: build
permalink: /build-from-source/
alt_url: /en/build-from-source/
alt_label: English
description: 了解如何使用 .NET 10 从源码构建和运行 PCL-ME。
hero_eyebrow: 开发与调试
hero_title: 从源码构建
hero_lead: 如果你想参与开发或自行运行最新代码，可以直接使用 .NET 10 构建并启动 Avalonia 前端。
primary_action_label: 查看仓库
primary_action_url: https://github.com/TheUnknownThing/PCL-CE
secondary_action_label: Avalonia 前端说明
secondary_action_url: https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md
---

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
- 测试可通过 `dotnet test` 运行。

## 常用路径

- `PCL.Frontend.Avalonia/`
- `PCL.Core/`
- `PCL.Core.Backend/`

## 更多文档

- [Avalonia 前端 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
- [仓库 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_CN.md)
