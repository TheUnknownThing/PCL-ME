using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.ShellViews;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed class App : Application
{
    private readonly AvaloniaCommandOptions _options;
    private readonly FrontendRuntimePaths _runtimePaths;
    private readonly FrontendPlatformAdapter _platformAdapter;

    public App(
        AvaloniaCommandOptions options,
        FrontendRuntimePaths runtimePaths,
        FrontendPlatformAdapter platformAdapter)
    {
        _options = options;
        _runtimePaths = runtimePaths;
        _platformAdapter = platformAdapter;
    }

    public override void Initialize()
    {
        FrontendLoggingBootstrap.Initialize(_runtimePaths);
        FrontendHttpProxyService.ApplyStoredProxySettings(_runtimePaths);
        FrontendHttpProxyService.ApplyStoredDnsSettings(_runtimePaths);
        FrontendShellActionService.ApplyStoredAnimationPreferences(_runtimePaths);
        AvaloniaXamlLoader.Load(this);
        FrontendAppearanceService.ApplyStoredAppearance(this, _runtimePaths);
        ShellPaneTemplateRegistry.Register(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startupVisual = FrontendStartupVisualCompositionService.Compose(_runtimePaths);
            var splashSession = FrontendStartupSplashPresentationService.Show(startupVisual);
            desktop.Exit += OnDesktopExit;
            Dispatcher.UIThread.Post(
                () => InitializeDesktop(desktop, _runtimePaths, _platformAdapter, splashSession),
                DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeDesktop(
        IClassicDesktopStyleApplicationLifetime desktop,
        FrontendRuntimePaths runtimePaths,
        FrontendPlatformAdapter platformAdapter,
        FrontendStartupSplashSession? splashSession)
    {
        try
        {
            var shellActionService = new FrontendShellActionService(
                runtimePaths,
                platformAdapter,
                () => desktop.Shutdown());
            var mainWindow = new MainWindow
            {
                DataContext = FrontendShellViewModel.CreateBootstrap(_options, shellActionService)
            };

            mainWindow.Opened += async (_, _) =>
            {
                await CloseSplashScreenAsync(splashSession);
                await FrontendMigrationDiagnostics.ShowMigrationWarningsAsync(shellActionService, runtimePaths);

                if (FrontendFontDiagnostics.ShouldWarnAboutMissingCjkFont(this, _options.ForceCjkFontWarning))
                {
                    await FrontendFontDiagnostics.ShowMissingCjkFontWarningAsync(shellActionService);
                }
            };

            desktop.MainWindow = mainWindow;
            if (!mainWindow.IsVisible)
            {
                mainWindow.Show();
            }
        }
        catch
        {
            splashSession?.Dispose();
            throw;
        }
    }

    private static void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        FrontendLoggingBootstrap.Dispose();
    }

    private static async Task CloseSplashScreenAsync(FrontendStartupSplashSession? splashSession)
    {
        if (splashSession is null)
        {
            return;
        }

        await splashSession.CloseAsync();
        splashSession.Dispose();
    }
}
