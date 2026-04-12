---
layout: default
title: PCL-ME
lang: zh-CN
nav_key: home
permalink: /
alt_url: /en/
alt_label: English
description: PCL-ME 是 PCL-CE 的多平台延续版本，使用 Avalonia 构建跨平台桌面启动器。
hero_eyebrow: 跨平台 Minecraft 启动器
hero_title: PCL-ME
hero_lead: PCL-CE 的多平台延续版本，基于 C#、.NET 10 与 Avalonia，为 Windows、macOS 和 Linux 提供统一桌面体验。
primary_action_label: 下载最新版本
primary_action_url: https://github.com/TheUnknownThing/PCL-CE/releases/latest
secondary_action_label: 查看源码
secondary_action_url: https://github.com/TheUnknownThing/PCL-CE
---

## 项目概览

<div class="card-grid">
  <section class="card">
    <h3>统一技术栈</h3>
    <p>当前主线为 <code>C# + .NET 10 + Avalonia</code>，不再以旧版 WPF 与 VB.NET 前端作为维护方向。</p>
  </section>
  <section class="card">
    <h3>跨平台桌面体验</h3>
    <p>同一套前端面向 Windows、macOS 与 Linux，重点验证目标为 macOS 与 Linux。</p>
  </section>
  <section class="card">
    <h3>持续迁移中</h3>
    <p>仓库正在将启动器能力逐步迁移到新的桌面前端与共享核心模块中。</p>
  </section>
</div>

## 平台状态

| 平台 | 状态 | 说明 |
| --- | --- | --- |
| Windows | 持续完善中 | 兼容性与验证仍在推进 |
| macOS | 主要目标平台 | 当前开发与测试重点之一 |
| Linux | 主要目标平台 | 当前开发与测试重点之一 |

如果你现在需要更稳妥的 Windows 优先体验，原始 PCL 或 PCL-CE 仍然可能是更安全的选择；PCL-ME 仍在持续完善中。

## 快速入口

- [下载页面]({{ "/downloads/" | relative_url }})
- [从源码构建]({{ "/build-from-source/" | relative_url }})
- [GitHub 仓库](https://github.com/TheUnknownThing/PCL-CE)
- [问题反馈](https://github.com/TheUnknownThing/PCL-CE/issues)

## 仓库结构

- `PCL.Frontend.Avalonia/`：当前正在维护的桌面前端
- `PCL.Core/` 与 `PCL.Core.Backend/`：共享启动器与后端逻辑
- 其余项目主要用于基础设施、测试与通用组件支持

## 相关文档

- [简体中文 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_CN.md)
- [English README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-EN.md)
- [繁體中文 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_TW.md)
- [Avalonia 前端说明](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
