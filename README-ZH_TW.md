[簡體中文](README.md) | [English](README-EN.md) | **繁體中文**

<div align="center">

<img src="PCL.Frontend.Avalonia/Assets/icon.png" alt="PCL-ME logo" width="80" height="80">

# PCL Multiplatform Edition

一款跨平台的 Minecraft 啟動器，支援 Windows、macOS 與 Linux。

[⬇ 下載最新版本](https://github.com/TheUnknownThing/PCL-ME/releases/latest) ·
[🐛 回報問題](https://github.com/TheUnknownThing/PCL-ME/issues) ·
[📖 貢獻指南](CONTRIBUTING.md)

</div>

## 關於 PCL-ME

PCL-ME 致力於將 PCL 熟悉的使用體驗完整帶到 Windows、macOS 與 Linux 上，並長期維護下去。

## 介面預覽

<table>
<tr>
<td><img src="docs/screenshots/launch.png" alt="啟動頁"></td>
<td><img src="docs/screenshots/download.png" alt="下載頁"></td>
<td><img src="docs/screenshots/settings.png" alt="設定頁"></td>
</tr>
<tr>
<td align="center">啟動遊戲</td>
<td align="center">下載 Mod 與整合包</td>
<td align="center">介面與主題設定</td>
</tr>
</table>

## 平台支援

| 平台 | 狀態 |
|---|---|
| 🐧 Linux | 主力平台，持續開發與測試 |
| 🍎 macOS | 主力平台，持續開發與測試 |
| 🪟 Windows | 已支援，但測試覆蓋略少於上述平台 |

## 安裝

- **Windows / macOS**：前往 [Releases 頁面](https://github.com/TheUnknownThing/PCL-ME/releases/latest) 下載對應平台的安裝包。
- **Linux**：除了 Releases 中的發行包，Arch Linux 使用者也可以透過 AUR 安裝：

  ```bash
  yay -S pcl-me-bin
  ```

---

## 給開發者

如果你希望從原始碼建置或參與開發：

```bash
dotnet restore
dotnet build
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

更多細節請參見 [Avalonia 前端文件](PCL.Frontend.Avalonia/README.md) 與 [後端文件](PCL.Core.Backend/README.md)，貢獻流程請參見 [CONTRIBUTING.md](CONTRIBUTING.md)。

> PCL-ME 由 [PCL-CE](https://github.com/PCL-Community/PCL-CE) 分叉而來，目前作為獨立分支維護。技術棧：C# + .NET 10 + Avalonia。

## 授權條款

PCL-ME 的程式碼依目錄分別授權：

- `PCL.Frontend.Avalonia/` 下的介面相關內容遵循 [自訂授權條款](PCL.Frontend.Avalonia/LICENSE)。
- 倉庫其餘部分遵循 [Apache License 2.0](LICENSE)。

若不確定某個檔案適用哪套條款，請查閱其所在目錄或上層目錄中的授權說明。

#### 資源致謝

- 應用程式圖示取自 [PCL-Community/PCL-CE-Logo](https://github.com/PCL-Community/PCL-CE-Logo)（Apache 2.0）。
- 介面字體使用 [HarmonyOS Sans](https://developer.huawei.com/consumer/en/design/resource/)，著作權歸 Huawei Device Co., Ltd. 所有，依據 HarmonyOS Sans Fonts License Agreement 授權使用，特此致謝。
