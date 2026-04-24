using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed partial class DownloadInstallRightPaneView : UserControl
{
    private readonly ScrollViewer _installScrollViewer;
    private readonly StackPanel _minecraftCatalogStage;
    private readonly PCL.Frontend.Avalonia.Desktop.Controls.PclCard _minecraftCatalogLoadingCard;
    private readonly PCL.Frontend.Avalonia.Desktop.Controls.PclCard _minecraftCatalogEmptyStateCard;
    private readonly ItemsControl _minecraftCatalogContentHost;
    private readonly StackPanel _selectionStage;
    private LauncherViewModel? _observedLauncher;
    private bool _hasAppliedInitialStageVisibility;
    private DownloadInstallStage _currentStage = DownloadInstallStage.Catalog;
    private int _stageAnimationVersion;
    private int _catalogVisualStateVersion;

    public DownloadInstallRightPaneView()
    {
        InitializeComponent();
        _installScrollViewer = this.FindControl<ScrollViewer>("InstallScrollViewer")
            ?? throw new InvalidOperationException("The install page did not contain the scroll viewer.");
        _minecraftCatalogStage = this.FindControl<StackPanel>("MinecraftCatalogStage")
            ?? throw new InvalidOperationException("The install page did not contain the Minecraft version selection stage.");
        _minecraftCatalogLoadingCard = this.FindControl<PCL.Frontend.Avalonia.Desktop.Controls.PclCard>("MinecraftCatalogLoadingCard")
            ?? throw new InvalidOperationException("The install page did not contain the Minecraft version loading card.");
        _minecraftCatalogEmptyStateCard = this.FindControl<PCL.Frontend.Avalonia.Desktop.Controls.PclCard>("MinecraftCatalogEmptyStateCard")
            ?? throw new InvalidOperationException("The install page did not contain the Minecraft version empty state.");
        _minecraftCatalogContentHost = this.FindControl<ItemsControl>("MinecraftCatalogContentHost")
            ?? throw new InvalidOperationException("The install page did not contain the Minecraft version content host.");
        _selectionStage = this.FindControl<StackPanel>("SelectionStage")
            ?? throw new InvalidOperationException("The install page did not contain the loader selection stage.");
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) =>
        {
            ObserveLauncher(null);
            _stageAnimationVersion++;
            _catalogVisualStateVersion++;
            _hasAppliedInitialStageVisibility = false;
        };
        ConfigureStageMotion();
        ConfigureCatalogMotion();
        ScheduleStageSync();
        ScheduleCatalogStateSync();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveLauncher(DataContext as LauncherViewModel);
        ScheduleStageSync();
        ScheduleCatalogStateSync();
    }

    private void ObserveLauncher(LauncherViewModel? shell)
    {
        if (ReferenceEquals(_observedLauncher, shell))
        {
            return;
        }

        if (_observedLauncher is not null)
        {
            _observedLauncher.PropertyChanged -= OnLauncherPropertyChanged;
        }

        _observedLauncher = shell;
        if (_observedLauncher is not null)
        {
            _observedLauncher.PropertyChanged += OnLauncherPropertyChanged;
        }
    }

    private void OnLauncherPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is (
            nameof(LauncherViewModel.ShowDownloadInstallMinecraftCatalog)
            or nameof(LauncherViewModel.ShowDownloadInstallSelectionStage)))
        {
            ScheduleStageSync();
        }

        if (e.PropertyName is (
            nameof(LauncherViewModel.ShowDownloadInstallMinecraftCatalogLoading)
            or nameof(LauncherViewModel.ShowDownloadInstallMinecraftCatalogContent)
            or nameof(LauncherViewModel.ShowDownloadInstallMinecraftCatalogEmptyState)))
        {
            ScheduleCatalogStateSync();
        }
    }

    private void ScheduleStageSync()
    {
        var version = ++_stageAnimationVersion;
        Dispatcher.UIThread.Post(
            async () => await SyncStageVisibilityAsync(version),
            DispatcherPriority.Background);
    }

    private async Task SyncStageVisibilityAsync(int version)
    {
        if (version != _stageAnimationVersion)
        {
            return;
        }

        var targetStage = ResolveTargetStage();
        if (!_hasAppliedInitialStageVisibility || VisualRoot is null)
        {
            ApplyStageVisibility(targetStage);
            _currentStage = targetStage;
            _hasAppliedInitialStageVisibility = true;
            return;
        }

        if (_currentStage == targetStage
            && _minecraftCatalogStage.IsVisible == (targetStage == DownloadInstallStage.Catalog)
            && _selectionStage.IsVisible == (targetStage == DownloadInstallStage.Selection))
        {
            return;
        }

        switch (targetStage)
        {
            case DownloadInstallStage.Selection:
                await TransitionToSelectionStageAsync(version);
                break;
            default:
                await TransitionToMinecraftCatalogStageAsync(version);
                break;
        }

        if (version == _stageAnimationVersion)
        {
            _currentStage = targetStage;
        }

        ScheduleCatalogStateSync();
    }

    private DownloadInstallStage ResolveTargetStage()
    {
        return _observedLauncher?.ShowDownloadInstallSelectionStage == true
            ? DownloadInstallStage.Selection
            : DownloadInstallStage.Catalog;
    }

    private void ApplyStageVisibility(DownloadInstallStage stage)
    {
        _minecraftCatalogStage.IsVisible = stage == DownloadInstallStage.Catalog;
        _selectionStage.IsVisible = stage == DownloadInstallStage.Selection;
        if (stage == DownloadInstallStage.Selection)
        {
            _installScrollViewer.ScrollToHome();
        }
    }

    private async Task TransitionToSelectionStageAsync(int version)
    {
        if (version != _stageAnimationVersion)
        {
            return;
        }

        _installScrollViewer.ScrollToHome();
        await HideAnimatedAsync(_minecraftCatalogStage, version);
        await ShowAnimatedAsync(_selectionStage, version);
    }

    private async Task TransitionToMinecraftCatalogStageAsync(int version)
    {
        if (version != _stageAnimationVersion)
        {
            return;
        }

        _installScrollViewer.ScrollToHome();
        await HideAnimatedAsync(_selectionStage, version);
        await ShowAnimatedAsync(_minecraftCatalogStage, version);
    }

    private void ScheduleCatalogStateSync()
    {
        var version = ++_catalogVisualStateVersion;
        Dispatcher.UIThread.Post(
            async () => await SyncCatalogVisualStateAsync(version),
            DispatcherPriority.Background);
    }

    private async Task SyncCatalogVisualStateAsync(int version)
    {
        if (version != _catalogVisualStateVersion)
        {
            return;
        }

        var showCatalog = _observedLauncher?.ShowDownloadInstallMinecraftCatalog == true;
        var showLoading = showCatalog && _observedLauncher?.ShowDownloadInstallMinecraftCatalogLoading == true;
        var showContent = showCatalog && _observedLauncher?.ShowDownloadInstallMinecraftCatalogContent == true;
        var showEmptyState = showCatalog && _observedLauncher?.ShowDownloadInstallMinecraftCatalogEmptyState == true;

        if (VisualRoot is null)
        {
            ApplyCatalogVisibility(showLoading, showContent, showEmptyState);
            return;
        }

        if (showLoading)
        {
            await HideAnimatedAsync(_minecraftCatalogContentHost, version, _catalogVisualStateVersion);
            await HideAnimatedAsync(_minecraftCatalogEmptyStateCard, version, _catalogVisualStateVersion);
            await ShowAnimatedAsync(_minecraftCatalogLoadingCard, version, _catalogVisualStateVersion);
            return;
        }

        await HideAnimatedAsync(_minecraftCatalogLoadingCard, version, _catalogVisualStateVersion);

        if (showEmptyState)
        {
            await HideAnimatedAsync(_minecraftCatalogContentHost, version, _catalogVisualStateVersion);
            await ShowAnimatedAsync(_minecraftCatalogEmptyStateCard, version, _catalogVisualStateVersion);
            return;
        }

        await HideAnimatedAsync(_minecraftCatalogEmptyStateCard, version, _catalogVisualStateVersion);
        if (showContent)
        {
            await ShowAnimatedAsync(_minecraftCatalogContentHost, version, _catalogVisualStateVersion);
        }
        else
        {
            await HideAnimatedAsync(_minecraftCatalogContentHost, version, _catalogVisualStateVersion);
        }
    }

    private async Task ShowAnimatedAsync(Control control, int version)
    {
        await ShowAnimatedAsync(control, version, _stageAnimationVersion);
    }

    private async Task ShowAnimatedAsync(Control control, int version, int expectedVersion)
    {
        if (version != expectedVersion)
        {
            return;
        }

        if (control.IsVisible)
        {
            return;
        }

        control.IsVisible = true;
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != expectedVersion)
        {
            return;
        }

        await Motion.PlayEnterAsync(control);
    }

    private async Task HideAnimatedAsync(Control control, int version)
    {
        await HideAnimatedAsync(control, version, _stageAnimationVersion);
    }

    private async Task HideAnimatedAsync(Control control, int version, int expectedVersion)
    {
        if (version != expectedVersion || !control.IsVisible)
        {
            return;
        }

        await Motion.PlayExitAsync(control);
        if (version != expectedVersion)
        {
            return;
        }

        control.IsVisible = false;
    }

    private void ConfigureStageMotion()
    {
        ConfigureStage(_minecraftCatalogStage, enterOffsetX: -48, exitOffsetX: -48);
        ConfigureStage(_selectionStage, enterOffsetX: 48, exitOffsetX: 48);
    }

    private void ConfigureCatalogMotion()
    {
        ConfigureStage(_minecraftCatalogContentHost, enterOffsetX: 0, exitOffsetX: 0, enterOffsetY: -8, exitOffsetY: -8, overshootTranslation: true);
        ConfigureStage(_minecraftCatalogEmptyStateCard, enterOffsetX: 0, exitOffsetX: 0, enterOffsetY: -6, exitOffsetY: -6, overshootTranslation: true);
        ConfigureStage(_minecraftCatalogLoadingCard, enterOffsetX: 0, exitOffsetX: 0, enterOffsetY: 10, exitOffsetY: 8, overshootTranslation: true);
        Motion.SetStaggerChildren(_minecraftCatalogContentHost, true);
        Motion.SetStaggerStep(_minecraftCatalogContentHost, 38);
        Motion.SetDelay(_minecraftCatalogContentHost, 32);
    }

    private void ApplyCatalogVisibility(bool showLoading, bool showContent, bool showEmptyState)
    {
        _minecraftCatalogLoadingCard.IsVisible = showLoading;
        _minecraftCatalogContentHost.IsVisible = showContent;
        _minecraftCatalogEmptyStateCard.IsVisible = showEmptyState;
    }

    private static void ConfigureStage(
        Control control,
        double enterOffsetX,
        double exitOffsetX,
        double enterOffsetY = 0,
        double exitOffsetY = 0,
        bool overshootTranslation = false)
    {
        Motion.SetInitialOpacity(control, 0);
        Motion.SetOffsetX(control, enterOffsetX);
        Motion.SetOffsetY(control, enterOffsetY);
        Motion.SetExitOffsetX(control, exitOffsetX);
        Motion.SetExitOffsetY(control, exitOffsetY);
        Motion.SetOvershootTranslation(control, overshootTranslation);
    }

    private enum DownloadInstallStage
    {
        Catalog,
        Selection
    }
}
