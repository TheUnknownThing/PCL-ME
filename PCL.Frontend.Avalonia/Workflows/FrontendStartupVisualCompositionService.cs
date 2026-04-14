using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendStartupVisualCompositionService
{
    public static LauncherStartupVisualPlan Compose(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        return Compose(runtimePaths.OpenLocalConfigProvider());
    }

    internal static LauncherStartupVisualPlan Compose(YamlFileProvider localConfig)
    {
        ArgumentNullException.ThrowIfNull(localConfig);

        return LauncherStartupVisualService.GetVisualPlan(ReadValue(localConfig, "UiLauncherLogo", true));
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }
}
