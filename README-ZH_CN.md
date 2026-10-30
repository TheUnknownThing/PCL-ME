[English](README-EN.md) · **简体中文** · [繁體中文](README-ZH_TW.md)

<p align="center">
  <img src="PCL.Frontend.Avalonia/Assets/icon.png" alt="PCL-ME 标志" width="112" height="112">
</p>

<h1 align="center">PCL Multiplatform Edition</h1>

<p align="center">
  熟悉的 PCL 体验，现已来到 Windows、macOS 与 Linux。
</p>

<p align="center">
  <a href="#开始使用"><img src="https://img.shields.io/badge/Platforms-Windows_%C2%B7_macOS_%C2%B7_Linux-3D948B?style=flat-square" alt="支持 Windows、macOS 与 Linux"></a>
  <a href="https://github.com/TheUnknownThing/PCL-ME/actions/workflows/build-test.yml"><img src="https://img.shields.io/github/actions/workflow/status/TheUnknownThing/PCL-ME/build-test.yml?branch=main&amp;style=flat-square&amp;label=CI" alt="CI 状态"></a>
  <a href="https://github.com/TheUnknownThing/PCL-ME/releases"><img src="https://img.shields.io/github/v/release/TheUnknownThing/PCL-ME?include_prereleases&amp;style=flat-square&amp;label=Release" alt="最新发布版本，包含预发布版本"></a>
</p>

<p align="center">
  <a href="https://github.com/TheUnknownThing/PCL-ME/releases/latest"><strong>下载</strong></a> ·
  <a href="https://github.com/TheUnknownThing/PCL-ME/issues">反馈问题</a> ·
  <a href="CONTRIBUTING.md">参与贡献</a>
</p>

PCL-ME 是一款 Minecraft 启动器，将 Plain Craft Launcher 熟悉的使用体验带到三大桌面平台。项目基于 C#、.NET 10 与 Avalonia 开发，由 [PCL-CE](https://github.com/PCL-Community/PCL-CE) 分叉而来，目前作为独立分支持续维护。

- **启动游戏** — 在同一界面管理游戏实例，启动 Minecraft。
- **发现资源** — 浏览模组、整合包、资源包与光影，为下一次冒险做好准备。
- **随心定制** — 调整主题、配色、语言与界面缩放，让启动器融入你的桌面。

## 界面预览

<table align="center" width="760">
  <tr>
    <td colspan="2" align="center">
      <a href="docs/screenshots/launch.png"><img src="docs/screenshots/launch.png" alt="PCL-ME 启动页，展示账户操作与当前选择的 Minecraft 实例" width="720"></a>
      <p>账户、游戏实例与启动操作，尽在一处。</p>
    </td>
  </tr>
  <tr>
    <td width="50%" align="center" valign="top">
      <a href="docs/screenshots/download.png"><img src="docs/screenshots/download.png" alt="模组浏览界面，可按 Minecraft 版本、加载器、来源与标签筛选" width="340"></a>
      <p><strong>发现下一款心仪模组</strong><br>
      按版本与加载器筛选，探索社区资源。</p>
    </td>
    <td width="50%" align="center" valign="top">
      <a href="docs/screenshots/settings.png"><img src="docs/screenshots/settings.png" alt="界面设置，展示深色主题、配色、语言与界面缩放选项" width="340"></a>
      <p><strong>打造熟悉的使用体验</strong><br>
      自由选择主题、配色、语言与界面缩放。</p>
    </td>
  </tr>
</table>

## 开始使用

前往[最新发布页面](https://github.com/TheUnknownThing/PCL-ME/releases/latest)，下载适合你的操作系统与处理器架构的安装包。

| 平台 | 支持情况 |
| --- | --- |
| Windows | 已支持；测试覆盖略少于 Linux 与 macOS。 |
| macOS | 主力平台，持续开发与测试。 |
| Linux | 主力平台，持续开发与测试。 |

Arch Linux 用户还可以使用 `yay` 等 AUR 助手安装：

```sh
yay -S pcl-me-bin
```

## 从源码构建

安装 **.NET 10 SDK**，然后在仓库根目录运行：

```sh
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

代码按桌面界面与共享启动器服务分层组织：

| 项目 | 职责 |
| --- | --- |
| [PCL.Frontend.Avalonia](PCL.Frontend.Avalonia/README.md) | 桌面界面、主题、资源与应用打包。 |
| [PCL.Core.Backend](PCL.Core.Backend/README.md) | 共享启动器服务、Minecraft 启动流程、下载与跨平台工具。 |

提交代码修改前，请运行 `dotnet test`。更详细的构建、测试、打包与故障排查流程，请参阅上方项目文档。

## 参与贡献

欢迎反馈问题、完善文档或贡献代码。开发流程与提交要求，请参阅[贡献指南](CONTRIBUTING.md)。

反馈问题时，请附上操作系统、启动器与游戏版本、复现步骤及日志。如果你希望实现新功能，请先提交[功能提案](https://github.com/TheUnknownThing/PCL-ME/issues/new?template=3-feature.yml)，与维护者确认方向后再提交实现。

## 许可证与致谢

本项目按目录分别授权：

- `PCL.Frontend.Avalonia/` 下的界面相关内容遵循前端的[自定义许可证](PCL.Frontend.Avalonia/LICENSE)。
- 仓库其余部分遵循 [Apache License 2.0](LICENSE)，除非文件另有说明。

具体适用条款，请查阅文件所在目录或上级目录中的许可证说明。

PCL-ME 基于 [PCL-CE](https://github.com/PCL-Community/PCL-CE) 的工作继续开发。应用图标基于 [PCL-Community/PCL-CE-Logo](https://github.com/PCL-Community/PCL-CE-Logo) 的素材，遵循 Apache 2.0 许可证。界面使用 [HarmonyOS Sans](https://developer.huawei.com/consumer/en/design/resource/) 字体，版权归 Huawei Device Co., Ltd. 所有，依据 HarmonyOS Sans Fonts License Agreement 授权使用，特此致谢。
