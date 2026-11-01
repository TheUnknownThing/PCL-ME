using PCL.Core.App.I18n;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal static class FrontendMigrationDiagnostics
{
    public static async Task ShowMigrationWarningsAsync(
        LauncherActionService launcherActionService,
        FrontendRuntimePaths runtimePaths,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(launcherActionService);
        ArgumentNullException.ThrowIfNull(runtimePaths);
        ArgumentNullException.ThrowIfNull(i18n);

        if (runtimePaths.MigrationWarnings.Count == 0)
        {
            return;
        }

        var message = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            runtimePaths.MigrationWarnings);
        message = i18n.T(
            "shell.startup.migration_warnings.message",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["warnings"] = message
            });

        await launcherActionService.ConfirmAsync(
            i18n.T("shell.startup.migration_warnings.title"),
            message,
            i18n.T("common.actions.continue"),
            isDanger: true);
    }
}
