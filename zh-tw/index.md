---
layout: default
title: PCL-ME
lang: zh-TW
permalink: /zh-tw/
description: PCL-ME 是一套面向 Windows、macOS 與 Linux 的跨平台啟動器，現行主線基於 C#、.NET 10 與 Avalonia。
---

[首頁]({{ "/zh-tw/" | relative_url }}) · [下載]({{ "/zh-tw/downloads/" | relative_url }}) · [從原始碼建置]({{ "/zh-tw/build-from-source/" | relative_url }}) · [社群]({{ "/zh-tw/community/" | relative_url }}) · [简体中文]({{ "/" | relative_url }}) · [English]({{ "/en/" | relative_url }})

PCL-ME 是上游 PCL-CE 的硬分叉版本，目標是維護一套真正可持續演進的跨平台啟動器。現行主線實作基於 C#、.NET 10 與 Avalonia，面向 Windows、macOS 與 Linux。

## 專案結構

- `PCL.Frontend.Avalonia/`：目前維護中的桌面前端與 UI 資源
- `PCL.Core.Backend/`：共用啟動器邏輯、後端工作流與基礎服務
- `PCL.Core.Backend.Test/`：後端回歸測試
- `PCL.Core.Backend.Foundation.Test/`：可攜性與基礎層測試

## 平台狀態

| 平台 | 狀態 | 說明 |
| --- | --- | --- |
| Windows | 持續完善中 | 已支援，但驗證強度仍低於 macOS / Linux |
| macOS | 主要目標平台 | 持續開發與測試 |
| Linux | 主要目標平台 | 持續開發與測試 |

## 快速開始

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 常用連結

- [下載發行版](https://github.com/TheUnknownThing/PCL-ME/releases/latest)
- [提交問題](https://github.com/TheUnknownThing/PCL-ME/issues)
- [貢獻指南](https://github.com/TheUnknownThing/PCL-ME/blob/main/CONTRIBUTING.md)
- [Avalonia 前端文件](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Frontend.Avalonia/README.md)
- [後端文件](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Core.Backend/README.md)
- [GitHub 倉庫](https://github.com/TheUnknownThing/PCL-ME)

## 授權條款

PCL-ME 採用分層授權模型：

- `PCL.Frontend.Avalonia/` 下的 UI 內容遵循倉庫根目錄中的 [LICENCE](https://github.com/TheUnknownThing/PCL-ME/blob/main/LICENCE) 自訂授權。
- 本倉庫中其餘啟動器相關邏輯遵循 [Apache License 2.0](https://github.com/TheUnknownThing/PCL-ME/blob/main/LICENSE)，除非某個檔案或子目錄另有說明。
