using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed partial class DownloadInstallRightPaneView : UserControl
{
    private readonly ScrollViewer _installScrollViewer;
    private readonly Grid _installPageGrid;
    private readonly Grid _minecraftCatalogStage;
    private readonly PCL.Frontend.Avalonia.Desktop.Controls.PclCard _minecraftCatalogLoadingCard;
    private readonly PCL.Frontend.Avalonia.Desktop.Controls.PclCard _minecraftCatalogEmptyStateCard;
    private readonly ItemsControl _minecraftCatalogGroupListHost;
    private readonly PCL.Frontend.Avalonia.Desktop.Controls.PclCard _minecraftCatalogExpandedHost;
    private readonly ItemsControl _minecraftCatalogExpandedList;
    private readonly StackPanel _selectionStage;
    private LauncherViewModel? _observedLauncher;
    private bool _hasAppliedInitialStageVisibility;
    private DownloadInstallStage _currentStage = DownloadInstallStage.Catalog;
    private int _stageAnimationVersion;
    private int _catalogVisualStateVersion;
    private DownloadInstallMinecraftSectionViewModel? _expandedCatalogSection;
    private readonly HashSet<DownloadInstallMinecraftSectionViewModel> _observedCatalogSections = new();

    public DownloadInstallRightPaneView()
    {
        InitializeComponent();
        _installScrollViewer = this.FindControl<ScrollViewer>("InstallScrollViewer")
            ?? throw new InvalidOperationException("The install page did not contain the scroll viewer.");
        _installPageGrid = this.FindControl<Grid>("InstallPageGrid")
            ?? throw new InvalidOperationException("The install page did not contain the page grid.");
        _minecraftCatalogStage = this.FindControl<Grid>("MinecraftCatalogStage")
            ?? throw new InvalidOperationException("The install page did not contain the Minecraft version selection stage.");
        _minecraftCatalogLoadingCard = this.FindControl<PCL.Frontend.Avalonia.Desktop.Controls.PclCard>("MinecraftCatalogLoadingCard")
            ?? throw new InvalidOperationException("The install page did not contain the Minecraft version loading card.");
        _minecraftCatalogEmptyStateCard = this.FindControl<PCL.Frontend.Avalonia.Desktop.Controls.PclCard>("MinecraftCatalogEmptyStateCard")
            ?? throw new InvalidOperationException("The install page did not contain the Minecraft version empty state.");
        _minecraftCatalogGroupListHost = this.FindControl<ItemsControl>("MinecraftCatalogGroupListHost")
            ?? throw new InvalidOperationException("The install page did not contain the Minecraft version group list.");
        _minecraftCatalogExpandedHost = this.FindControl<PCL.Frontend.Avalonia.Desktop.Controls.PclCard>("MinecraftCatalogExpandedHost")
            ?? throw new InvalidOperationException("The install page did not contain the expanded Minecraft version host.");
        _minecraftCatalogExpandedList = this.FindControl<ItemsControl>("MinecraftCatalogExpandedList")
            ?? throw new InvalidOperationException("The install page did not contain the expanded Minecraft version list.");
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
            _observedLauncher.DownloadInstallMinecraftSections.CollectionChanged -= OnCatalogSectionsCollectionChanged;
        }

        _observedLauncher = shell;
        if (_observedLauncher is not null)
        {
            _observedLauncher.PropertyChanged += OnLauncherPropertyChanged;
            _observedLauncher.DownloadInstallMinecraftSections.CollectionChanged += OnCatalogSectionsCollectionChanged;
        }

        UnobserveAllCatalogSections();
        if (_observedLauncher is not null)
        {
            foreach (var section in _observedLauncher.DownloadInstallMinecraftSections)
            {
                ObserveCatalogSection(section);
            }
        }

        ScheduleCatalogFillModeSync();
    }

    private void OnCatalogSectionsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // A Reset (ReplaceItems / Clear) reports OldItems == null, so we must drop the
        // subscriptions we already hold before observing the replacement sections.
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            UnobserveAllCatalogSections();
            _expandedCatalogSection = null;
        }
        else
        {
            if (e.OldItems is not null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is DownloadInstallMinecraftSectionViewModel section)
                    {
                        UnobserveCatalogSection(section);
                    }
                }
            }

            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is DownloadInstallMinecraftSectionViewModel section)
                    {
                        ObserveCatalogSection(section);
                    }
                }
            }
        }

        ScheduleCatalogFillModeSync();
    }

    private void ObserveCatalogSection(DownloadInstallMinecraftSectionViewModel section)
    {
        if (!_observedCatalogSections.Add(section))
        {
            return;
        }

        section.PropertyChanged += OnCatalogSectionPropertyChanged;
    }

    private void UnobserveCatalogSection(DownloadInstallMinecraftSectionViewModel section)
    {
        if (!_observedCatalogSections.Remove(section))
        {
            return;
        }

        section.PropertyChanged -= OnCatalogSectionPropertyChanged;
    }

    private void UnobserveAllCatalogSections()
    {
        foreach (var section in _observedCatalogSections.ToArray())
        {
            UnobserveCatalogSection(section);
        }
    }

    private void OnCatalogSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadInstallMinecraftSectionViewModel.IsExpanded))
        {
            // Expanding/collapsing a group switches between the compact group list and
            // the full-page expanded view, and also toggles the outer page scroll.
            ScheduleCatalogStateSync();
            ScheduleCatalogFillModeSync();
        }
    }

    private bool _catalogFillModeSyncScheduled;

    private void ScheduleCatalogFillModeSync()
    {
        if (_catalogFillModeSyncScheduled)
        {
            return;
        }

        _catalogFillModeSyncScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _catalogFillModeSyncScheduled = false;
            SyncCatalogFillMode();
        }, DispatcherPriority.Background);
    }

    private void SyncCatalogFillMode()
    {
        // The catalog only fills the window when a collapsible group is expanded. In
        // that state the outer page scroll is disabled so the page (and therefore the
        // catalog's star row) gets a bounded height, which the expanded card fills and
        // whose inner list then scrolls. When nothing is expanded the page scrolls
        // normally.
        var sections = _observedLauncher?.DownloadInstallMinecraftSections;
        var anyExpanded = sections is not null && sections.Any(section => section.CanCollapse && section.IsExpanded);
        var isCatalogStage = _currentStage == DownloadInstallStage.Catalog
            && (_observedLauncher?.ShowDownloadInstallMinecraftCatalog == true);
        var fillMode = anyExpanded && isCatalogStage;
        var desiredScroll = fillMode ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        var desiredStageRow = fillMode ? GridLength.Star : GridLength.Auto;

        if (_installScrollViewer.VerticalScrollBarVisibility != desiredScroll)
        {
            _installScrollViewer.VerticalScrollBarVisibility = desiredScroll;
        }

        if (_installPageGrid.RowDefinitions[0].Height != desiredStageRow)
        {
            _installPageGrid.RowDefinitions[0].Height = desiredStageRow;
        }

        if (!fillMode)
        {
            _installScrollViewer.ScrollToHome();
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
            ScheduleCatalogFillModeSync();
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
        ScheduleCatalogFillModeSync();
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

        // Selecting a version leaves the catalog. Collapse the group that was
        // expanded so returning with the Back button restores the group list
        // instead of re-opening the expanded host.
        var expandedSection = ResolveExpandedCatalogSection();
        if (expandedSection is not null)
        {
            expandedSection.IsExpanded = false;
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

        var expandedSection = ResolveExpandedCatalogSection();

        if (VisualRoot is null)
        {
            ApplyCatalogVisibility(showLoading, showContent, showEmptyState, expandedSection);
            return;
        }

        if (showLoading)
        {
            await HideAnimatedAsync(_minecraftCatalogGroupListHost, version, _catalogVisualStateVersion);
            await HideAnimatedAsync(_minecraftCatalogExpandedHost, version, _catalogVisualStateVersion);
            await HideAnimatedAsync(_minecraftCatalogEmptyStateCard, version, _catalogVisualStateVersion);
            await ShowAnimatedAsync(_minecraftCatalogLoadingCard, version, _catalogVisualStateVersion);
            return;
        }

        await HideAnimatedAsync(_minecraftCatalogLoadingCard, version, _catalogVisualStateVersion);

        if (showEmptyState)
        {
            await HideAnimatedAsync(_minecraftCatalogGroupListHost, version, _catalogVisualStateVersion);
            await HideAnimatedAsync(_minecraftCatalogExpandedHost, version, _catalogVisualStateVersion);
            await ShowAnimatedAsync(_minecraftCatalogEmptyStateCard, version, _catalogVisualStateVersion);
            return;
        }

        await HideAnimatedAsync(_minecraftCatalogEmptyStateCard, version, _catalogVisualStateVersion);
        if (!showContent)
        {
            await HideAnimatedAsync(_minecraftCatalogGroupListHost, version, _catalogVisualStateVersion);
            await HideAnimatedAsync(_minecraftCatalogExpandedHost, version, _catalogVisualStateVersion);
            return;
        }

        // When a group is expanded, the catalog shows a single full-page view of that
        // group's versions (which scrolls internally). Otherwise it shows the compact
        // list of group headers.
        if (expandedSection is not null)
        {
            ApplyExpandedCatalogSection(expandedSection);
            await HideAnimatedAsync(_minecraftCatalogGroupListHost, version, _catalogVisualStateVersion);
            await ShowAnimatedAsync(_minecraftCatalogExpandedHost, version, _catalogVisualStateVersion);
        }
        else
        {
            await HideAnimatedAsync(_minecraftCatalogExpandedHost, version, _catalogVisualStateVersion);
            await ShowAnimatedAsync(_minecraftCatalogGroupListHost, version, _catalogVisualStateVersion);
        }
    }

    private DownloadInstallMinecraftSectionViewModel? ResolveExpandedCatalogSection()
    {
        var sections = _observedLauncher?.DownloadInstallMinecraftSections;
        if (sections is null)
        {
            return null;
        }

        return sections.FirstOrDefault(section => section.CanCollapse && section.IsExpanded);
    }

    private void ApplyExpandedCatalogSection(DownloadInstallMinecraftSectionViewModel section)
    {
        if (ReferenceEquals(_expandedCatalogSection, section))
        {
            return;
        }

        _expandedCatalogSection = section;
        _minecraftCatalogExpandedHost.Header = section.Title;
        _minecraftCatalogExpandedHost.HeaderCommand = section.HeaderCommand;
        _minecraftCatalogExpandedList.ItemsSource = section.Choices;
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
        ConfigureStage(_minecraftCatalogGroupListHost, enterOffsetX: 0, exitOffsetX: 0, enterOffsetY: -8, exitOffsetY: -8, overshootTranslation: true);
        ConfigureStage(_minecraftCatalogExpandedHost, enterOffsetX: 0, exitOffsetX: 0, enterOffsetY: -10, exitOffsetY: -10, overshootTranslation: true);
        ConfigureStage(_minecraftCatalogEmptyStateCard, enterOffsetX: 0, exitOffsetX: 0, enterOffsetY: -6, exitOffsetY: -6, overshootTranslation: true);
        ConfigureStage(_minecraftCatalogLoadingCard, enterOffsetX: 0, exitOffsetX: 0, enterOffsetY: 10, exitOffsetY: 8, overshootTranslation: true);
        Motion.SetStaggerChildren(_minecraftCatalogGroupListHost, true);
        Motion.SetStaggerStep(_minecraftCatalogGroupListHost, 38);
        Motion.SetDelay(_minecraftCatalogGroupListHost, 32);
        Motion.SetStaggerChildren(_minecraftCatalogExpandedList, true);
        Motion.SetStaggerStep(_minecraftCatalogExpandedList, 24);
    }

    private void ApplyCatalogVisibility(
        bool showLoading,
        bool showContent,
        bool showEmptyState,
        DownloadInstallMinecraftSectionViewModel? expandedSection)
    {
        _minecraftCatalogLoadingCard.IsVisible = showLoading;
        _minecraftCatalogEmptyStateCard.IsVisible = showEmptyState;
        if (expandedSection is not null)
        {
            ApplyExpandedCatalogSection(expandedSection);
        }
        _minecraftCatalogGroupListHost.IsVisible = showContent && expandedSection is null;
        _minecraftCatalogExpandedHost.IsVisible = showContent && expandedSection is not null;
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
