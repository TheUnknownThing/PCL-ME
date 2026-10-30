using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Core.App.I18n;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.Panes;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed partial class App : Application
{
    private readonly AvaloniaCommandOptions _options;
    private readonly FrontendRuntimePaths _runtimePaths;
    private readonly FrontendPlatformAdapter _platformAdapter;
    private I18nService? _i18nService;
    private LauncherActionService? _launcherActionService;

    public App(
        AvaloniaCommandOptions options,
        FrontendRuntimePaths runtimePaths,
        FrontendPlatformAdapter platformAdapter)
    {
        _options = options;
        _runtimePaths = runtimePaths;
        _platformAdapter = platformAdapter;
        // Avalonia's macOS backend exports this name as the process/menu-bar title
        // before setup completes; the default would otherwise read "Avalonia Application".
        Name = FrontendApplicationIdentity.DisplayName;
    }

    public override void Initialize()
    {
        FrontendLoggingBootstrap.Initialize(_runtimePaths);
        FrontendHttpProxyService.ApplyStoredProxySettings(_runtimePaths);
        FrontendHttpProxyService.ApplyStoredDnsSettings(_runtimePaths);
        LauncherActionService.ApplyStoredAnimationPreferences(_runtimePaths);
        AvaloniaXamlLoader.Load(this);
        FrontendAppearanceService.ApplyStoredAppearance(this, _runtimePaths);
        PaneTemplateRegistry.Register(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
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
            FrontendAppearanceService.ReapplyCurrentAppearance(this);
            var localeDirectory = Path.Combine(AppContext.BaseDirectory, "Locales");
            var availableLocales = Directory.EnumerateFiles(localeDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToArray();
            var settingsManager = new I18nSettingsManager(
                new FrontendConfigProviderAdapter(runtimePaths),
                initialLocaleProvider: () => CultureInfo.CurrentUICulture.Name,
                availableLocales: availableLocales);
            _i18nService = new I18nService(settingsManager);
            var launcherActionService = new LauncherActionService(
                runtimePaths,
                platformAdapter,
                () => desktop.Shutdown(),
                _i18nService);
            _launcherActionService = launcherActionService;
            var launcherViewModel = LauncherViewModel.CreateBootstrap(_options, launcherActionService, _i18nService);
            var mainWindow = new MainWindow
            {
                DataContext = launcherViewModel
            };

            async void OnMainWindowOpened(object? sender, EventArgs args)
            {
                mainWindow.Opened -= OnMainWindowOpened;
                await CloseSplashScreenAsync(splashSession);
                await FrontendMigrationDiagnostics.ShowMigrationWarningsAsync(launcherActionService, runtimePaths, _i18nService!);

                if (FrontendFontDiagnostics.ShouldWarnAboutMissingCjkFont(this, _options.ForceCjkFontWarning))
                {
                    await FrontendFontDiagnostics.ShowMissingCjkFontWarningAsync(launcherActionService);
                }
            }

            mainWindow.Opened += OnMainWindowOpened;

            desktop.MainWindow = mainWindow;
            InitializeDesktopShell(mainWindow, launcherViewModel, launcherActionService, _i18nService);
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
        try
        {
            _launcherActionService?.FlushPendingSettings();
        }
        finally
        {
            DisposeDesktopShell();
            _i18nService?.Dispose();
            FrontendLoggingBootstrap.Dispose();
        }
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
