using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
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
        DataContextChanged += OnDataContextChanged;
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

        ScheduleSelectionRestore();
    }

    private void ScheduleSelectionRestore()
    {
        Dispatcher.UIThread.Post(RestoreFilterSelections, DispatcherPriority.Background);
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
            DispatcherPriority.Background);
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

        await HideAnimatedAsync(_loadingCard, version);
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
            await ShowAnimatedAsync(_contentHost, version);
        }
        else
        {
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

    private void RestoreFilterSelections()
    {
        if (_observedShell is null)
        {
            return;
        }

        ApplyComboSelection(_sourceComboBox, _observedShell.SelectedDownloadResourceSourceOption);
        ApplyComboSelection(_tagComboBox, _observedShell.SelectedDownloadResourceTagOption);
        ApplyComboSelection(_sortComboBox, _observedShell.SelectedDownloadResourceSortOption);
        ApplyComboSelection(_versionComboBox, _observedShell.SelectedDownloadResourceVersionOption);
        ApplyComboSelection(_loaderComboBox, _observedShell.SelectedDownloadResourceLoaderOption);
    }

    private static void ApplyComboSelection(ComboBox comboBox, object? selectedItem)
    {
        if (selectedItem is null || ReferenceEquals(comboBox.SelectedItem, selectedItem))
        {
            return;
        }

        comboBox.SelectedItem = selectedItem;
    }
}
