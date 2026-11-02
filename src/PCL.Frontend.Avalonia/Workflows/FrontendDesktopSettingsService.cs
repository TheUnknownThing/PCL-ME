using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendDesktopSettingsService
{
    internal const string KeepRunningInTrayKey = "UiKeepRunningInTray";
    internal const string WindowCloseActionKey = "UiWindowCloseAction";

    public static FrontendDesktopSettings Read(YamlFileProvider config) => new(
        config.Exists(KeepRunningInTrayKey) && config.Get<bool>(KeepRunningInTrayKey),
        config.Exists(WindowCloseActionKey) && Enum.TryParse<WindowCloseAction>(config.Get<string>(WindowCloseActionKey), out var action)
            ? action : WindowCloseAction.MinimizeToTray);
}
