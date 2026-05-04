using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed partial class InstanceResourceRightPaneView : UserControl
{
    private const double ContentSectionSpacing = 15;
    private const double SelectionActionSpacerMinimumHeight = 80;
    private const double SelectionActionSpacerPadding = 28;
    private const string HiddenSelectionActionTransform = "translate(0px,10px) scale(0.985)";
    private const string VisibleSelectionActionTransform = "translate(0px,-25px) scale(1)";

    private LauncherViewModel? _launcherViewModel;
    private bool _selectionActionCardShown;
    private int _selectionActionAnimationVersion;

    public InstanceResourceRightPaneView()
    {
        InitializeComponent();

        ConfigureSelectionActionButtons();
        ContentGrid.SizeChanged += (_, _) => UpdateEntryListAvailableHeight();
        HeaderSection.SizeChanged += (_, _) => UpdateEntryListAvailableHeight();
        SelectionActionCard.SizeChanged += (_, _) => UpdateEntryListBottomPadding();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => DetachLauncherViewModel();
    }

    private void ConfigureSelectionActionButtons()
    {
        var selectAllIcon = FrontendIconCatalog.GetSidebarIcon("game_manage");
        SelectionSelectAllButton.IconData = selectAllIcon.Data;
        SelectionSelectAllButton.IconScale = selectAllIcon.Scale;
        SelectionEnableButton.IconData = FrontendIconCatalog.EnableCircle.Data;
        SelectionEnableButton.IconScale = FrontendIconCatalog.EnableCircle.Scale;
        SelectionDisableButton.IconData = FrontendIconCatalog.DisableCircle.Data;
        SelectionDisableButton.IconScale = FrontendIconCatalog.DisableCircle.Scale;
        var exportIcon = FrontendIconCatalog.GetSidebarIcon("log");
        SelectionExportButton.IconData = exportIcon.Data;
        SelectionExportButton.IconScale = exportIcon.Scale;
        SelectionCheckButton.IconData = FrontendIconCatalog.InfoCircle.Data;
        SelectionCheckButton.IconScale = 0.82;
        SelectionDeleteButton.IconData = FrontendIconCatalog.DeleteOutline.Data;
        SelectionDeleteButton.IconScale = FrontendIconCatalog.DeleteOutline.Scale;
        SelectionCancelButton.IconData = FrontendIconCatalog.Close.Data;
        SelectionCancelButton.IconScale = 0.8;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachLauncherViewModel();

        _launcherViewModel = DataContext as LauncherViewModel;
        if (_launcherViewModel is not null)
        {
            _launcherViewModel.PropertyChanged += OnLauncherViewModelPropertyChanged;
        }

        _ = UpdateSelectionActionCardAsync(animated: false);
    }

    private void DetachLauncherViewModel()
    {
        if (_launcherViewModel is null)
        {
            return;
        }

        _launcherViewModel.PropertyChanged -= OnLauncherViewModelPropertyChanged;
        _launcherViewModel = null;
    }

    private void OnLauncherViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherViewModel.ShowInstanceResourceBatchActions)
            or nameof(LauncherViewModel.ShowInstanceResourceContent))
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    UpdateEntryListAvailableHeight();
                    _ = UpdateSelectionActionCardAsync(animated: true);
                },
                DispatcherPriority.Background);
        }
    }

    private bool ShouldShowSelectionActionCard()
    {
        return _launcherViewModel?.ShowInstanceResourceContent == true &&
               _launcherViewModel.ShowInstanceResourceBatchActions;
    }

    private async Task UpdateSelectionActionCardAsync(bool animated)
    {
        var shouldShow = ShouldShowSelectionActionCard();
        var wasShown = _selectionActionCardShown;
        var version = ++_selectionActionAnimationVersion;

        UpdateEntryListBottomPadding();

        if (!animated)
        {
            ApplySelectionActionCardState(shouldShow);
            return;
        }

        if (shouldShow == wasShown)
        {
            if (shouldShow && !SelectionActionCard.IsVisible)
            {
                ApplySelectionActionCardState(true);
            }

            return;
        }

        _selectionActionCardShown = shouldShow;

        if (shouldShow)
        {
            SelectionActionCard.IsVisible = true;
            SelectionActionCard.IsHitTestVisible = false;
            SelectionActionCard.Opacity = 0d;
            SelectionActionCard.RenderTransform = ParseTransform(HiddenSelectionActionTransform);
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);

            if (version != _selectionActionAnimationVersion || !_selectionActionCardShown)
            {
                return;
            }

            SelectionActionCard.Opacity = 1d;
            SelectionActionCard.RenderTransform = ParseTransform(VisibleSelectionActionTransform);
            await Task.Delay(MotionDurations.QuickState);
            if (version != _selectionActionAnimationVersion || !_selectionActionCardShown)
            {
                return;
            }

            SelectionActionCard.IsHitTestVisible = true;
            return;
        }

        SelectionActionCard.IsHitTestVisible = false;
        SelectionActionCard.Opacity = 0d;
        SelectionActionCard.RenderTransform = ParseTransform(HiddenSelectionActionTransform);

        if (!SelectionActionCard.IsVisible)
        {
            return;
        }

        await Task.Delay(MotionDurations.QuickState);
        if (version != _selectionActionAnimationVersion || _selectionActionCardShown)
        {
            return;
        }

        SelectionActionCard.IsVisible = false;
    }

    private void ApplySelectionActionCardState(bool shouldShow)
    {
        _selectionActionCardShown = shouldShow;
        SelectionActionCard.IsVisible = shouldShow;
        SelectionActionCard.IsHitTestVisible = shouldShow;
        SelectionActionCard.Opacity = shouldShow ? 1d : 0d;
        SelectionActionCard.RenderTransform = ParseTransform(
            shouldShow ? VisibleSelectionActionTransform : HiddenSelectionActionTransform);
    }

    private void UpdateEntryListBottomPadding()
    {
        if (!ShouldShowSelectionActionCard())
        {
            EntriesSection.SetListBottomPadding(0d);
            return;
        }

        var measuredHeight = SelectionActionCard.Bounds.Height > 0
            ? SelectionActionCard.Bounds.Height + SelectionActionSpacerPadding
            : 0d;
        EntriesSection.SetListBottomPadding(Math.Max(SelectionActionSpacerMinimumHeight, measuredHeight));
    }

    private void UpdateEntryListAvailableHeight()
    {
        if (ContentGrid.Bounds.Height <= 0)
        {
            return;
        }

        var availableHeight = ContentGrid.Bounds.Height - HeaderSection.Bounds.Height - ContentSectionSpacing;
        if (availableHeight > 0)
        {
            EntriesSection.SetListAvailableHeight(availableHeight);
        }
    }

    private static global::Avalonia.Media.Transformation.TransformOperations ParseTransform(string value)
    {
        return global::Avalonia.Media.Transformation.TransformOperations.Parse(value);
    }
}
