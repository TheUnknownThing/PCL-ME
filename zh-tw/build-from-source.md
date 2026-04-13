---
layout: default
title: 從原始碼建置
lang: zh-TW
permalink: /zh-tw/build-from-source/
description: 了解如何使用 .NET 10 從原始碼建置並執行 PCL-ME。
---

[首頁]({{ "/zh-tw/" | relative_url }}) · [下載]({{ "/zh-tw/downloads/" | relative_url }}) · [從原始碼建置]({{ "/zh-tw/build-from-source/" | relative_url }}) · [社群]({{ "/zh-tw/community/" | relative_url }}) · [简体中文]({{ "/build-from-source/" | relative_url }}) · [English]({{ "/en/build-from-source/" | relative_url }})

## 環境需求

- `.NET 10 SDK`
- 可執行 Avalonia 的桌面環境
- Git

## 快速開始

```bash
git clone https://github.com/TheUnknownThing/PCL-CE.git
cd PCL-CE
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

## 說明

- 目前正在維護的前端目錄為 `PCL.Frontend.Avalonia/`。
- 舊版 WPF 前端不再是本倉庫的開發目標。
- 目前主線程式碼以 C# 與 `.NET 10` 為基礎。
- 測試可透過 `dotnet test` 執行。

## 常用路徑

- `PCL.Frontend.Avalonia/`
- `PCL.Core.Backend/`

## 更多文件

- [Avalonia 前端 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/PCL.Frontend.Avalonia/README.md)
- [倉庫 README](https://github.com/TheUnknownThing/PCL-CE/blob/dev/README-ZH_TW.md)
