using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.ShellViews;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed class App(AvaloniaCommandOptions options) : Application
{
    public override void Initialize()
    {
        var platformAdapter = new FrontendPlatformAdapter();
        FrontendShellActionService.ApplyStoredAnimationPreferences(FrontendRuntimePaths.Resolve(platformAdapter));
        AvaloniaXamlLoader.Load(this);
        ShellPaneTemplateRegistry.Register(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var platformAdapter = new FrontendPlatformAdapter();
            var shellActionService = new FrontendShellActionService(
                FrontendRuntimePaths.Resolve(platformAdapter),
                platformAdapter,
                () => desktop.Shutdown());
            desktop.MainWindow = new MainWindow
            {
                DataContext = FrontendShellViewModel.CreateBootstrap(options, shellActionService)
            };

            desktop.MainWindow.Opened += async (_, _) =>
            {
                if (FrontendFontDiagnostics.ShouldWarnAboutMissingCjkFont(this, options.ForceCjkFontWarning))
                {
                    await FrontendFontDiagnostics.ShowMissingCjkFontWarningAsync(desktop);
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
