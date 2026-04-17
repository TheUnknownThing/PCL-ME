# 如何为 PCL-ME 做贡献

感谢你愿意参与 PCL-ME。

## 关于 AI 生成代码

我们接受使用 AI 工具辅助分析、编写或重构代码，也接受包含 AI 生成内容的 Issue、PR 与文档修改。

但请注意：

- 请提交者至少阅读并理解 AI 生成内容的含义、实现细节与潜在风险；如果你无法解释、无法复现或无法验证 AI 生成内容，我们将会拒绝接受相关提交。
- 提交者必须对最终提交内容的正确性、可维护性、测试结果与许可证风险负责。
- 如果你提交的是 Bug Issue，请务必附上可复现的错误环境与运行日志；否则维护者通常无法判断问题是否真实存在、是否已被修复，或是否能够稳定复现。
- 如果你希望提交某个新功能的实现，请先提出对应的 Feature Issue，并在 maintainer 明确认可方向后再提交针对该功能的 PR。

## 开始之前

- 先阅读 [README](README.md) 与 [PCL.Frontend.Avalonia/README.md](PCL.Frontend.Avalonia/README.md)。
- 查看当前 [Issues](https://github.com/TheUnknownThing/PCL-ME/issues)，确认问题或提案是否已经存在。
- 如果你准备为新功能提交 PR，请先创建对应的 Feature Issue，并等待 maintainer 在 Issue 中确认方向。
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

提交 Issue 时请尽量按类型提供完整信息：

1. 所有 Issue 都应包含清晰的问题描述或改进目标，并尽量避免把多个无关问题塞进同一个 Issue。
2. Bug / 崩溃 Issue 必须包含可复现的错误环境，例如操作系统、架构、启动器版本、.NET 环境、游戏版本、模组加载器或其他与问题相关的上下文。
3. Bug / 崩溃 Issue 必须包含明确的复现步骤、实际结果与预期结果。
4. Bug / 崩溃 Issue 必须附上运行日志、错误报告、截图或导出的诊断文件；缺少可复现环境与日志的反馈可能会被直接关闭。
5. Feature / Improve Issue 应说明具体诉求、使用场景与这样设计的原因。
6. 如果你计划自己实现某个新功能，请先提交 Feature Issue，等待 maintainer 确认接受该方向后，再提交相应 PR。

## 提交代码

1. Fork 仓库并克隆到本地。
2. 从最新分支创建功能分支。
3. 完成开发后先本地构建与测试。
4. 如果改动对应一个新功能，请先确认已有 maintainer 接受的 Feature Issue。
5. 提交更改并创建 Pull Request。
6. 在 PR 描述里说明改动范围、验证方式和剩余风险。

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
