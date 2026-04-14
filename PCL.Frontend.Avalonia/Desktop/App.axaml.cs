using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Core.App.I18n;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.ShellViews;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed class App : Application
{
    private readonly AvaloniaCommandOptions _options;
    private FrontendRuntimePaths? _runtimePaths;
    private I18nService? _i18nService;

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
            var startupVisual = FrontendStartupVisualCompositionService.Compose(runtimePaths);
            var splashSession = FrontendStartupSplashPresentationService.Show(startupVisual);
            desktop.Exit += OnDesktopExit;
            Dispatcher.UIThread.Post(
                () => InitializeDesktop(desktop, runtimePaths, platformAdapter, splashSession),
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
            var settingsManager = new I18nSettingsManager(
                new FrontendConfigProviderAdapter(runtimePaths.OpenSharedConfigProvider()));
            _i18nService = new I18nService(settingsManager);
            var shellActionService = new FrontendShellActionService(
                runtimePaths,
                platformAdapter,
                () => desktop.Shutdown(),
                _i18nService);
            var mainWindow = new MainWindow
            {
                DataContext = FrontendShellViewModel.CreateBootstrap(_options, shellActionService, _i18nService)
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

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _i18nService?.Dispose();
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
