# 如何为 PCL-ME 做贡献

感谢你愿意参与 PCL-ME。

PCL-ME 是从上游社区版衍生出来的多平台版本，当前主线技术栈是 `C# + .NET 10 + Avalonia`。如果仓库中仍保留旧的 WPF / VB.NET 痕迹，它们只应被视为历史参考，而不是新的开发目标。

## 开始之前

- 先阅读 [README](README.md) 与 [PCL.Frontend.Avalonia/README.md](PCL.Frontend.Avalonia/README.md)。
- 查看当前 [Issues](https://github.com/TheUnknownThing/PCL-ME/issues)，确认问题或提案是否已经存在。
- 涉及界面改动时，请优先修改 `PCL.Frontend.Avalonia/`，不要把新功能继续加回旧的 WPF 路线。

## 本地开发

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj -- app
```

建议：

- 功能变更尽量补充对应测试。
- 文档、帮助文案或品牌名发生变化时，请同步更新 README 与相关用户可见文案。
- 若改动涉及跨平台行为，请至少说明你验证过的平台或尚未验证的风险。

## 提交问题

提交 Issue 时请尽量包含：

1. 清晰的问题描述或改进目标。
2. 复现步骤与实际结果。
3. 预期结果。
4. 操作系统、架构、.NET 环境等关键信息。
5. 日志、截图或导出的诊断文件。

## 提交代码

1. Fork 仓库并克隆到本地。
2. 从最新分支创建功能分支。
3. 完成开发后先本地构建与测试。
4. 提交更改并创建 Pull Request。
5. 在 PR 描述里说明改动范围、验证方式和剩余风险。

## 提交信息

继续沿用简洁的约定式提交格式：

```text
<type>(scope): <subject>
```

常见 `type` 包括：

- `feat`
- `fix`
- `docs`
- `refactor`
- `test`
- `build`
- `ci`
- `chore`

如果改动会破坏兼容性，请在正文或页脚中明确说明。
