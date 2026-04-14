---
layout: default
title: 從原始碼建置
lang: zh-TW
permalink: /zh-tw/build-from-source/
description: 了解如何使用 .NET 10 從原始碼建置、執行並測試 PCL-ME。
---

[首頁]({{ "/zh-tw/" | relative_url }}) · [下載]({{ "/zh-tw/downloads/" | relative_url }}) · [從原始碼建置]({{ "/zh-tw/build-from-source/" | relative_url }}) · [社群]({{ "/zh-tw/community/" | relative_url }}) · [简体中文]({{ "/build-from-source/" | relative_url }}) · [English]({{ "/en/build-from-source/" | relative_url }})

## 環境需求

- Git
- `.NET 10 SDK`
- 可執行 Avalonia 的桌面環境

## 快速開始

```bash
git clone https://github.com/TheUnknownThing/PCL-ME.git
cd PCL-ME
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 測試

```bash
dotnet test
```

倉庫目前包含後端回歸測試與基礎層測試；提交變更前，建議至少先跑完整測試集確認沒有回歸。

## 重點目錄

- `PCL.Frontend.Avalonia/`：目前維護中的桌面前端與 UI 資源
- `PCL.Core.Backend/`：共用啟動器邏輯、後端工作流與基礎服務
- `PCL.Core.Backend.Test/`：後端回歸測試
- `PCL.Core.Backend.Foundation.Test/`：可攜性與基礎層測試

## 相關文件

- [繁體中文 README](https://github.com/TheUnknownThing/PCL-ME/blob/main/README-ZH_TW.md)
- [Avalonia 前端 README](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Frontend.Avalonia/README.md)
- [後端 README](https://github.com/TheUnknownThing/PCL-ME/blob/main/PCL.Core.Backend/README.md)
