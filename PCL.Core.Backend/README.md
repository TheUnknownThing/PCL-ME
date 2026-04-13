# PCL.Core.Backend

`PCL.Core.Backend` 是 PCL-ME 当前使用的共享后端程序集，由 Avalonia 前端与后端测试项目共同使用。

## 目录结构

- `Foundation/`：可复用的基础设施能力，例如配置存储、路径、环境访问、日志、下载、Java 发现、代理、校验与跨平台辅助逻辑
- `App/`：更贴近启动器业务的服务，例如启动流程、导航支持、提示面、身份处理、密钥处理与任务模型
- `Minecraft/`：启动编排、Java 运行时下载、崩溃分析、导出工作流等 Minecraft 相关服务
- `Utils/`：仍归属于后端层的其他共享工具

## 构建与测试

仅构建后端：

```bash
dotnet build PCL.Core.Backend/PCL.Core.Backend.csproj --no-restore
```

修改后端后的常见验证流程：

```bash
dotnet build PCL.Frontend.Avalonia/PCL.Frontend.Avalonia.csproj --no-restore
dotnet test PCL.Core.Backend.Test/PCL.Core.Backend.Test.csproj --no-restore
dotnet test PCL.Core.Backend.Foundation.Test/PCL.Core.Backend.Foundation.Test.csproj --no-restore
```

## 许可证

这个后端项目遵循 [../LICENSE](../LICENSE) 中的 Apache License 2.0，除非某个文件另有说明。

`PCL.Frontend.Avalonia/` 下的前端 UI 层则单独遵循 [../LICENCE](../LICENCE)。
