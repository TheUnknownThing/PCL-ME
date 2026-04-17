using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.I18n;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.Processes;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendShellActionService(
    FrontendRuntimePaths runtimePaths,
    FrontendPlatformAdapter platformAdapter,
    Action exitLauncher,
    II18nService i18nService)
{
    private static readonly HttpClient JavaRuntimeHttpClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromSeconds(100));

    public FrontendRuntimePaths RuntimePaths { get; } = runtimePaths;

    public FrontendPlatformAdapter PlatformAdapter { get; } = platformAdapter;

    private II18nService I18n { get; } = i18nService;

    public Func<string, string, string, bool, Task<bool>>? ConfirmPresenter { get; set; }

    public Func<string, string, string, string, string?, bool, Task<string?>>? TextInputPresenter { get; set; }

    public Func<string, string, IReadOnlyList<PclChoiceDialogOption>, string?, string, Task<string?>>? ChoicePresenter { get; set; }

    public void ExitLauncher()
    {
        exitLauncher();
    }

    private void ApplyPostLaunchShellPlan(MinecraftGameShellPlan shellPlan)
    {
        ApplyLauncherShellAction(shellPlan.LauncherAction);
    }

    private void ApplyPostLaunchShellPlanOnUiThread(MinecraftGameShellPlan shellPlan)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyPostLaunchShellPlan(shellPlan);
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() => ApplyPostLaunchShellPlan(shellPlan)).GetAwaiter().GetResult();
    }

    private void ApplyLauncherShellAction(MinecraftLaunchShellAction action)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        switch (action.Kind)
        {
            case MinecraftLaunchShellActionKind.ExitLauncher:
                exitLauncher();
                break;
            case MinecraftLaunchShellActionKind.HideLauncher:
                desktop.MainWindow.Hide();
                break;
            case MinecraftLaunchShellActionKind.MinimizeLauncher:
                desktop.MainWindow.WindowState = global::Avalonia.Controls.WindowState.Minimized;
                break;
            case MinecraftLaunchShellActionKind.ShowLauncher:
                desktop.MainWindow.Show();
                desktop.MainWindow.WindowState = global::Avalonia.Controls.WindowState.Normal;
                desktop.MainWindow.Activate();
                break;
        }
    }
}
