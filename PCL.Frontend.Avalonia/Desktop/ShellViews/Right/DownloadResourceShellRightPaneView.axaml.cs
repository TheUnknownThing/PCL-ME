using System.ComponentModel;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class DownloadResourceShellRightPaneView : UserControl
{
    private readonly PclSearchBox _searchRow;
    private readonly PclCard _filterCard;
    private readonly Border _hintBanner;
    private readonly StackPanel _contentHost;
    private readonly PclCard _loadingCard;
    private readonly ComboBox _sourceComboBox;
    private readonly ComboBox _tagComboBox;
    private readonly ComboBox _sortComboBox;
    private readonly ComboBox _versionComboBox;
    private readonly ComboBox _loaderComboBox;
    private FrontendShellViewModel? _observedShell;
    private int _visualStateVersion;
    private string _lastHintText = string.Empty;
    private bool _isRestoringFilterSelections;
    private bool _isApplyingShellFilterState;

    public DownloadResourceShellRightPaneView()
    {
        InitializeComponent();
        _searchRow = this.FindControl<PclSearchBox>("SearchRow")
            ?? throw new InvalidOperationException("The resource download page did not contain the search box host.");
        _filterCard = this.FindControl<PclCard>("FilterCard")
            ?? throw new InvalidOperationException("The resource download page did not contain the filter card.");
        _hintBanner = this.FindControl<Border>("HintBanner")
            ?? throw new InvalidOperationException("The resource download page did not contain the hint banner.");
        _contentHost = this.FindControl<StackPanel>("ContentHost")
            ?? throw new InvalidOperationException("The resource download page did not contain the content host.");
        _loadingCard = this.FindControl<PclCard>("LoadingCard")
            ?? throw new InvalidOperationException("The resource download page did not contain the loading card.");
        _sourceComboBox = this.FindControl<ComboBox>("DownloadResourceSourceComboBox")
            ?? throw new InvalidOperationException("The resource download page did not contain the source filter.");
        _tagComboBox = this.FindControl<ComboBox>("DownloadResourceTagComboBox")
            ?? throw new InvalidOperationException("The resource download page did not contain the tag filter.");
        _sortComboBox = this.FindControl<ComboBox>("DownloadResourceSortComboBox")
            ?? throw new InvalidOperationException("The resource download page did not contain the sort filter.");
        _versionComboBox = this.FindControl<ComboBox>("DownloadResourceVersionComboBox")
            ?? throw new InvalidOperationException("The resource download page did not contain the version filter.");
        _loaderComboBox = this.FindControl<ComboBox>("DownloadResourceLoaderComboBox")
            ?? throw new InvalidOperationException("The resource download page did not contain the loader filter.");
        _sourceComboBox.SelectionChanged += (_, _) => OnFilterSelectionChanged(FilterKind.Source, _sourceComboBox);
        _tagComboBox.SelectionChanged += (_, _) => OnFilterSelectionChanged(FilterKind.Tag, _tagComboBox);
        _sortComboBox.SelectionChanged += (_, _) => OnFilterSelectionChanged(FilterKind.Sort, _sortComboBox);
        _versionComboBox.SelectionChanged += (_, _) => OnFilterSelectionChanged(FilterKind.Version, _versionComboBox);
        _loaderComboBox.SelectionChanged += (_, _) => OnFilterSelectionChanged(FilterKind.Loader, _loaderComboBox);
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            PrimeLoadingMask();
            ScheduleVisualStateSync();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            ObserveShell(null);
            _visualStateVersion++;
        };
        ConfigureSurfaceMotion();
        ScheduleVisualStateSync();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveShell(DataContext as FrontendShellViewModel);
        ScheduleSelectionRestore();
        ScheduleVisualStateSync();
    }

    private void ObserveShell(FrontendShellViewModel? shell)
    {
        if (ReferenceEquals(_observedShell, shell))
        {
            return;
        }

        if (_observedShell is not null)
        {
            _observedShell.PropertyChanged -= OnShellPropertyChanged;
        }

        _observedShell = shell;
        if (_observedShell is not null)
        {
            _observedShell.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(FrontendShellViewModel.DownloadResourceSourceOptions)
            or nameof(FrontendShellViewModel.DownloadResourceTagOptions)
            or nameof(FrontendShellViewModel.DownloadResourceSortOptions)
            or nameof(FrontendShellViewModel.DownloadResourceVersionOptions)
            or nameof(FrontendShellViewModel.DownloadResourceLoaderOptions)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceSourceOption)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceTagOption)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceSortOption)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceVersionOption)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceLoaderOption)))
        {
            if (e.PropertyName is nameof(FrontendShellViewModel.ShowDownloadResourceLoadingCard)
                or nameof(FrontendShellViewModel.ShowDownloadResourceContent)
                or nameof(FrontendShellViewModel.ShowDownloadResourceHint)
                or nameof(FrontendShellViewModel.DownloadResourceHintText))
            {
                ScheduleVisualStateSync();
            }

            return;
        }

        _isApplyingShellFilterState = true;
        ScheduleSelectionRestore();
    }

    private void ScheduleSelectionRestore()
    {
        Dispatcher.UIThread.Post(() => RestoreFilterSelections(remainingAttempts: 3), DispatcherPriority.Render);
    }

    private void ConfigureSurfaceMotion()
    {
        ConfigureAnimatedSurface(_searchRow, enterOffsetY: -12, exitOffsetY: -10);
        ConfigureAnimatedSurface(_filterCard, enterOffsetY: -12, exitOffsetY: -10);
        ConfigureAnimatedSurface(_hintBanner, enterOffsetY: -10, exitOffsetY: -8);
        ConfigureAnimatedSurface(_contentHost, enterOffsetY: -8, exitOffsetY: -8, staggerChildren: true, delayMilliseconds: 32);
        ConfigureAnimatedSurface(_loadingCard, enterOffsetY: 10, exitOffsetY: 8);
    }

    private void ScheduleVisualStateSync()
    {
        var version = ++_visualStateVersion;
        Dispatcher.UIThread.Post(
            async () => await SyncVisualStateAsync(version),
            DispatcherPriority.Render);
    }

    private void PrimeLoadingMask()
    {
        if (_contentHost.IsVisible || _loadingCard.IsVisible)
        {
            return;
        }

        _loadingCard.IsVisible = true;
        _loadingCard.Opacity = 1d;
        _loadingCard.RenderTransform = new TranslateTransform(0, 0);
    }

    private async Task SyncVisualStateAsync(int version)
    {
        if (version != _visualStateVersion || VisualRoot is null)
        {
            return;
        }

        var showLoading = _observedShell?.ShowDownloadResourceLoadingCard == true;
        var showContent = _observedShell?.ShowDownloadResourceContent == true;
        var showHint = _observedShell?.ShowDownloadResourceHint == true;
        var hintText = _observedShell?.DownloadResourceHintText ?? string.Empty;

        if (showLoading)
        {
            await TransitionToLoadingStateAsync(version);
            return;
        }

        await TransitionToContentStateAsync(showContent, showHint, hintText, version);
    }

    private async Task TransitionToLoadingStateAsync(int version)
    {
        if (version != _visualStateVersion)
        {
            return;
        }

        await HideAnimatedAsync(_contentHost, version);
        await HideAnimatedAsync(_hintBanner, version);
        await ShowAnimatedAsync(_searchRow, version);
        await ShowAnimatedAsync(_filterCard, version);
        await ShowAnimatedAsync(_loadingCard, version);
    }

    private async Task TransitionToContentStateAsync(bool showContent, bool showHint, string hintText, int version)
    {
        if (version != _visualStateVersion)
        {
            return;
        }
        await ShowAnimatedAsync(_searchRow, version);
        await ShowAnimatedAsync(_filterCard, version);

        if (showHint)
        {
            var trimmedHintText = hintText.Trim();
            var shouldReplay = _hintBanner.IsVisible
                               && !string.Equals(_lastHintText, trimmedHintText, StringComparison.Ordinal);
            await ShowAnimatedAsync(_hintBanner, version, replayWhenVisible: shouldReplay);
            _lastHintText = trimmedHintText;
        }
        else
        {
            await HideAnimatedAsync(_hintBanner, version);
            _lastHintText = string.Empty;
        }

        if (showContent)
        {
            var shouldMaskReveal = _loadingCard.IsVisible && !_contentHost.IsVisible;
            if (shouldMaskReveal)
            {
                await Task.Delay(MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(180)));
                if (version != _visualStateVersion)
                {
                    return;
                }

                await Task.WhenAll(
                    ShowAnimatedAsync(_contentHost, version),
                    HideAnimatedAsync(_loadingCard, version));
            }
            else
            {
                await HideAnimatedAsync(_loadingCard, version);
                await ShowAnimatedAsync(_contentHost, version);
            }
        }
        else
        {
            await HideAnimatedAsync(_loadingCard, version);
            await HideAnimatedAsync(_contentHost, version);
        }
    }

    private async Task ShowAnimatedAsync(Control control, int version, bool replayWhenVisible = false)
    {
        if (version != _visualStateVersion)
        {
            return;
        }

        if (control.IsVisible && !replayWhenVisible)
        {
            return;
        }

        control.IsVisible = true;
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != _visualStateVersion)
        {
            return;
        }

        await Motion.PlayEnterAsync(control);
    }

    private async Task HideAnimatedAsync(Control control, int version)
    {
        if (version != _visualStateVersion || !control.IsVisible)
        {
            return;
        }

        await Motion.PlayExitAsync(control);
        if (version != _visualStateVersion)
        {
            return;
        }

        control.IsVisible = false;
    }

    private static void ConfigureAnimatedSurface(
        Control control,
        double enterOffsetY,
        double exitOffsetY,
        bool staggerChildren = false,
        int delayMilliseconds = 0)
    {
        Motion.SetInitialOpacity(control, 0);
        Motion.SetOffsetX(control, 0);
        Motion.SetOffsetY(control, enterOffsetY);
        Motion.SetExitOffsetX(control, 0);
        Motion.SetExitOffsetY(control, exitOffsetY);
        Motion.SetOvershootTranslation(control, true);
        Motion.SetStaggerChildren(control, staggerChildren);
        Motion.SetStaggerStep(control, 38);
        Motion.SetDelay(control, delayMilliseconds);
    }

    private void RestoreFilterSelections(int remainingAttempts)
    {
        if (_observedShell is null)
        {
            _isApplyingShellFilterState = false;
            return;
        }

        _isRestoringFilterSelections = true;
        bool restoredAllSelections;
        try
        {
            restoredAllSelections =
                ApplyComboSelection(_sourceComboBox, _observedShell.SelectedDownloadResourceSourceOption)
                && ApplyComboSelection(_tagComboBox, _observedShell.SelectedDownloadResourceTagOption)
                && ApplyComboSelection(_sortComboBox, _observedShell.SelectedDownloadResourceSortOption)
                && ApplyComboSelection(_versionComboBox, _observedShell.SelectedDownloadResourceVersionOption)
                && ApplyComboSelection(_loaderComboBox, _observedShell.SelectedDownloadResourceLoaderOption);
        }
        finally
        {
            _isRestoringFilterSelections = false;
            _isApplyingShellFilterState = false;
        }

        // Version options are rebuilt during instance switches, so the target item can lag one UI tick behind.
        if (!restoredAllSelections && remainingAttempts > 0)
        {
            Dispatcher.UIThread.Post(() => RestoreFilterSelections(remainingAttempts - 1), DispatcherPriority.Render);
        }
    }

    private void OnFilterSelectionChanged(FilterKind filterKind, ComboBox comboBox)
    {
        if (_isRestoringFilterSelections || _isApplyingShellFilterState || _observedShell is null)
        {
            return;
        }

        var selectedOption = comboBox.SelectedItem as DownloadResourceFilterOptionViewModel;
        switch (filterKind)
        {
            case FilterKind.Source:
                _observedShell.SelectedDownloadResourceSourceOption = selectedOption;
                break;
            case FilterKind.Tag:
                _observedShell.SelectedDownloadResourceTagOption = selectedOption;
                break;
            case FilterKind.Sort:
                _observedShell.SelectedDownloadResourceSortOption = selectedOption;
                break;
            case FilterKind.Version:
                _observedShell.SelectedDownloadResourceVersionOption = selectedOption;
                break;
            case FilterKind.Loader:
                _observedShell.SelectedDownloadResourceLoaderOption = selectedOption;
                break;
        }
    }

    private static bool ApplyComboSelection(ComboBox comboBox, object? selectedItem)
    {
        if (selectedItem is null)
        {
            if (comboBox.SelectedItem is null)
            {
                return true;
            }

            comboBox.SelectedItem = null;
            return true;
        }

        var resolvedSelection = ResolveComboSelection(comboBox, selectedItem);
        if (resolvedSelection is null)
        {
            return false;
        }

        if (ReferenceEquals(comboBox.SelectedItem, resolvedSelection))
        {
            return true;
        }

        comboBox.SelectedItem = resolvedSelection;
        return true;
    }

    private static object? ResolveComboSelection(ComboBox comboBox, object selectedItem)
    {
        if (comboBox.ItemsSource is not IEnumerable items)
        {
            return selectedItem;
        }

        foreach (var item in items)
        {
            if (ReferenceEquals(item, selectedItem))
            {
                return item;
            }

            if (item is DownloadResourceFilterOptionViewModel option
                && selectedItem is DownloadResourceFilterOptionViewModel target
                && string.Equals(option.FilterValue, target.FilterValue, StringComparison.OrdinalIgnoreCase)
                && string.Equals(option.Label, target.Label, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private enum FilterKind
    {
        Source = 0,
        Tag = 1,
        Sort = 2,
        Version = 3,
        Loader = 4
    }
}
