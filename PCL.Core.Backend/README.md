# PCL.Core.Backend

`PCL.Core.Backend` 是 PCL-ME 当前正在使用的共享后端程序集。

旧的 `PCL.Core` 代码树已经经过针对跨平台使用的完全重构。启动器的核心共享逻辑现在集中维护在这里，并由 Avalonia 前端与后端测试项目共同使用。

## 目录职责

- `Foundation/`：更底层、可复用、偏基础设施的能力层，例如配置存储、应用环境与路径布局、日志、下载、Java 扫描与解析、代理、操作系统与运行时信息、加密、校验、通用工具等
- `App/`：更贴近启动器业务的服务，例如启动引导、前端导航与提示面、身份与密钥处理、任务中心模型等
- `Minecraft/`：负责 Minecraft 启动、崩溃分析、Java 运行时下载、启动配置生成等工作流的编排
- `Utils/`：仍由后端直接持有、但尚未归入 `Foundation/` 的部分工具类。

## 命名空间说明

虽然项目程序集名是 `PCL.Core.Backend`，但 `.csproj` 中的 `RootNamespace` 仍然设置为 `PCL.Core`。

这意味着这里的源码仍会编译到：

- `PCL.Core.App`
- `PCL.Core.Minecraft`
- `PCL.Core.Utils.*`

这样的历史命名空间下。

这是当前预期行为，不代表这些类型来自已经移除的旧 `PCL.Core` 项目树。

## 项目特征

- 目标框架：`net10.0`
- 已启用 `nullable`
- C# 语言版本：`14.0`
- 关闭默认 `Compile` 收集，改为在项目文件中显式声明编译项
- 前端使用者：[PCL.Frontend.Avalonia](/Users/theunknownthing/PCL-CE/PCL.Frontend.Avalonia)
- 配套测试项目：
  - [PCL.Core.Backend.Test](/Users/theunknownthing/PCL-CE/PCL.Core.Backend.Test)
  - [PCL.Core.Backend.Foundation.Test](/Users/theunknownthing/PCL-CE/PCL.Core.Backend.Foundation.Test)

## 常用命令

仅构建后端项目：

```bash
dotnet build /Users/theunknownthing/PCL-CE/PCL.Core.Backend/PCL.Core.Backend.csproj --no-restore
```

在修改后端后，按当前仓库约定串行验证活动栈：

```bash
dotnet build /Users/theunknownthing/PCL-CE/PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj --no-restore
dotnet test /Users/theunknownthing/PCL-CE/PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj --no-restore
dotnet test /Users/theunknownthing/PCL-CE/PCL.Core.Backend.Foundation.Test/PCL.Core.Backend.Foundation.Test.csproj --no-restore
```

## 修改建议

- 若能力是通用基础设施，而不是启动器策略本身，优先放入 `Foundation/`
- 若逻辑已经涉及启动器业务、前端交互或 Minecraft 启动流程，优先放在 `App/` 或 `Minecraft/`
- 若某个方法需要被前端使用，尽量保持可移植、可序列化、与 UI 框架解耦

## 在仓库中的位置

如果你正在追踪当前真正生效的共享逻辑，应当从这里开始。

- 启动器启动主要在 `App/Essentials`
- Java 运行时相关基础能力主要在 `Foundation/Minecraft/Java`
- 启动编排主要在 `Minecraft/Launch`
- 崩溃分析与导出主要在 `Minecraft/`
- 可移植配置提供器与路径/环境布局主要在 `Foundation/App/Configuration` 与 `Foundation/App/Environment`
