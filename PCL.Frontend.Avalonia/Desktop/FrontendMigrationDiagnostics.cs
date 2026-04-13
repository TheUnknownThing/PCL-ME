using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal static class FrontendMigrationDiagnostics
{
    public static async Task ShowMigrationWarningsAsync(
        FrontendShellActionService shellActionService,
        FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(shellActionService);
        ArgumentNullException.ThrowIfNull(runtimePaths);

        if (runtimePaths.MigrationWarnings.Count == 0)
        {
            return;
        }

        var message = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            runtimePaths.MigrationWarnings);
        message =
            $"启动器无法迁移配置文件，将继续运行，但某些设置可能会回退到默认值。{Environment.NewLine}{Environment.NewLine}{message}";

        await shellActionService.ConfirmAsync(
            "配置文件迁移失败",
            message,
            "继续",
            isDanger: true);
    }
}
