using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.ShellViews;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed class App : Application
{
    private readonly AvaloniaCommandOptions _options;
    private FrontendRuntimePaths? _runtimePaths;

    public App(AvaloniaCommandOptions options)
    {
        _options = options;
    }

    public override void Initialize()
    {
        var platformAdapter = new FrontendPlatformAdapter();
        _runtimePaths = FrontendRuntimePaths.Resolve(platformAdapter);
        FrontendLoggingBootstrap.Initialize(_runtimePaths);
        FrontendShellActionService.ApplyStoredAnimationPreferences(_runtimePaths);
        AvaloniaXamlLoader.Load(this);
        FrontendAppearanceService.ApplyStoredAppearance(this, _runtimePaths);
        ShellPaneTemplateRegistry.Register(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var platformAdapter = new FrontendPlatformAdapter();
            var runtimePaths = _runtimePaths ?? FrontendRuntimePaths.Resolve(platformAdapter);
            desktop.Exit += OnDesktopExit;
            var shellActionService = new FrontendShellActionService(
                runtimePaths,
                platformAdapter,
                () => desktop.Shutdown());
            desktop.MainWindow = new MainWindow
            {
                DataContext = FrontendShellViewModel.CreateBootstrap(_options, shellActionService)
            };

            desktop.MainWindow.Opened += async (_, _) =>
            {
                await FrontendMigrationDiagnostics.ShowMigrationWarningsAsync(shellActionService, runtimePaths);

                if (FrontendFontDiagnostics.ShouldWarnAboutMissingCjkFont(this, _options.ForceCjkFontWarning))
                {
                    await FrontendFontDiagnostics.ShowMissingCjkFontWarningAsync(shellActionService);
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        FrontendLoggingBootstrap.Dispose();
    }
}
