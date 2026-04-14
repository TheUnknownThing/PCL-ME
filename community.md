---
layout: default
title: 社区
lang: zh-CN
permalink: /community/
description: 参与 PCL-ME 社区，提交 Issue、阅读贡献指南并跟进当前开发方向。
---

[首页]({{ "/" | relative_url }}) · [下载]({{ "/downloads/" | relative_url }}) · [从源码构建]({{ "/build-from-source/" | relative_url }}) · [社区]({{ "/community/" | relative_url }}) · [English]({{ "/en/community/" | relative_url }}) · [繁體中文]({{ "/zh-tw/community/" | relative_url }})

## 参与方式

- [提交 Issue](https://github.com/TheUnknownThing/PCL-ME/issues)
- [贡献指南](https://github.com/TheUnknownThing/PCL-ME/blob/main/CONTRIBUTING.md)
- [仓库主页](https://github.com/TheUnknownThing/PCL-ME)

## 贡献说明

- 项目当前维护重点是 `C# + .NET 10 + Avalonia` 的跨平台启动器栈。
- 前端相关改动通常集中在 `PCL.Frontend.Avalonia/`，共享逻辑集中在 `PCL.Core.Backend/`。
- 仓库包含后端与基础层测试，提交前建议先运行 `dotnet test`。
- Pull Request 中附带平台信息、验证步骤和测试结果会很有帮助。

## 反馈问题时建议附带

1. 清晰的复现步骤。
2. 实际结果与预期结果。
3. 你的操作系统、架构与 .NET 环境信息。
4. 相关日志、截图或诊断文件。
