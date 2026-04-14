---
layout: default
title: 下载
lang: zh-CN
permalink: /downloads/
description: 下载 PCL-ME 的最新版本，并查看当前 README 对平台状态与项目结构的说明。
---

[首页]({{ "/" | relative_url }}) · [下载]({{ "/downloads/" | relative_url }}) · [从源码构建]({{ "/build-from-source/" | relative_url }}) · [社区]({{ "/community/" | relative_url }}) · [English]({{ "/en/downloads/" | relative_url }}) · [繁體中文]({{ "/zh-tw/downloads/" | relative_url }})

## 推荐下载方式

- [最新版本](https://github.com/TheUnknownThing/PCL-ME/releases/latest)
- [全部 Releases](https://github.com/TheUnknownThing/PCL-ME/releases)
- [源码仓库](https://github.com/TheUnknownThing/PCL-ME)

## 当前平台状态

当前 README 对平台现状的说明如下：

| 平台 | 状态 | 说明 |
| --- | --- | --- |
| Windows | 持续完善中 | 已支持，但验证强度仍低于 macOS / Linux |
| macOS | 主要目标平台 | 持续开发与测试 |
| Linux | 主要目标平台 | 持续开发与测试 |

## 下载前说明

- 当前主线实现基于 C#、.NET 10 与 Avalonia。
- 活跃桌面前端位于 `PCL.Frontend.Avalonia/`，共享逻辑位于 `PCL.Core.Backend/`。
- 若你更关注源码、测试或平台细节，可以直接对照仓库 README 与对应子模块文档。

## 继续了解项目

- [简体中文 README](https://github.com/TheUnknownThing/PCL-ME/blob/main/README-ZH_CN.md)
- [Issue 列表](https://github.com/TheUnknownThing/PCL-ME/issues)
- [从源码构建]({{ "/build-from-source/" | relative_url }})
- [社区与贡献]({{ "/community/" | relative_url }})
