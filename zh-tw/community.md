---
layout: default
title: 社群
lang: zh-TW
permalink: /zh-tw/community/
description: 參與 PCL-ME 社群，提交 Issue、閱讀貢獻指南，並跟進目前的開發方向。
---

[首頁]({{ "/zh-tw/" | relative_url }}) · [下載]({{ "/zh-tw/downloads/" | relative_url }}) · [從原始碼建置]({{ "/zh-tw/build-from-source/" | relative_url }}) · [社群]({{ "/zh-tw/community/" | relative_url }}) · [简体中文]({{ "/community/" | relative_url }}) · [English]({{ "/en/community/" | relative_url }})

## 參與方式

- [提交 Issue](https://github.com/TheUnknownThing/PCL-ME/issues)
- [貢獻指南](https://github.com/TheUnknownThing/PCL-ME/blob/main/CONTRIBUTING.md)
- [倉庫首頁](https://github.com/TheUnknownThing/PCL-ME)

## 貢獻說明

- 專案目前維護重點是 `C# + .NET 10 + Avalonia` 的跨平台啟動器程式碼棧。
- 前端相關改動通常集中在 `PCL.Frontend.Avalonia/`，共用邏輯集中在 `PCL.Core.Backend/`。
- 倉庫包含後端與基礎層測試，提交前建議先執行 `dotnet test`。
- Pull Request 若附帶平台資訊、驗證步驟與測試結果會很有幫助。

## 回報問題時建議附帶

1. 清楚的重現步驟。
2. 實際結果與預期結果。
3. 你的作業系統、架構與 .NET 環境資訊。
4. 相關日誌、截圖或診斷檔案。
