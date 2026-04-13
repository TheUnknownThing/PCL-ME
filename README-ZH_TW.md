[簡體中文](README.md) | [English](README-EN.md) | **繁體中文**

<div align="center">

<img src="PCL.Frontend.Avalonia/Assets/icon.png" alt="PCL-ME logo" width="80" height="80">

# PCL-ME

Plain Craft Launcher Multiplatform Edition

[下載發行版](https://github.com/TheUnknownThing/PCL-CE/releases/latest) |
[提交問題](https://github.com/TheUnknownThing/PCL-CE/issues) |
[貢獻指南](CONTRIBUTING.md) |
[Avalonia 前端文件](PCL.Frontend.Avalonia/README.md)

</div>

PCL-ME 是從 PCL-CE 衍生出來的多平台版本。這個倉庫已經完全放棄舊的 VB.NET 前端路線，現階段以 C# 為主語言，並使用 Avalonia 建構面向 Windows、macOS 與 Linux 的桌面 UI。

## ✨ 專案特點

- 目前活躍程式碼全部為 C#，不再以 VB.NET / WPF 前端作為維護方向。
- 專案執行環境與工具鏈基於 `.NET 10`。
- 前端 UI 使用 Avalonia，為跨平台提供一致的使用體驗與原生效能。

## 平台狀態

| 平台 | 狀態 | 說明 |
|---|---|---|
| Windows | ⚠️ 仍未經充分測試 | 測試仍在持續推進中 |
| macOS | ✅ 主要目標平台 | 進行充分開發與測試 |
| Linux | ✅ 主要目標平台 | 進行充分開發與測試 |

由於這個專案的首要目標並非在 Windows 上提供完整支援，因此 Windows 平台的測試與最佳化仍在進行中，尚未達到穩定狀態，也可能仍存在未發現的相容性問題。若你想在 Windows 上使用 PCL，目前仍較建議使用 PCL 或 PCL-CE。

## 快速開始

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

建議先從這些目錄了解專案：

- `PCL.Frontend.Avalonia/`：目前正在維護的桌面前端
- `PCL.Core/` 與 `PCL.Core.Backend/`：共用啟動器與後端邏輯

## 授權條款

- `PCL 啟動器邏輯` 使用與原始 PCL / PCL-CE 一致的 [自訂授權指引](https://github.com/PCL-Community/PCL-CE/blob/dev/Plain%20Craft%20Launcher%202/LICENCE)。
- `其他獨立邏輯` 使用 [Apache License 2.0](LICENSE)。
