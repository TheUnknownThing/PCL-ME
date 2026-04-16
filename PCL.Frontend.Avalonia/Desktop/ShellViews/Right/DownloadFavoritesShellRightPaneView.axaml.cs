using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class DownloadFavoritesShellRightPaneView : UserControl
{
    private const double SelectionActionSpacerMinimumHeight = 80;
    private const double SelectionActionSpacerPadding = 28;
    private const string HiddenSelectionActionTransform = "translate(0px,10px) scale(0.985)";
    private const string VisibleSelectionActionTransform = "translate(0px,-25px) scale(1)";

    private FrontendShellViewModel? _shellViewModel;
    private bool _selectionActionCardShown;
    private int _selectionActionAnimationVersion;

    public DownloadFavoritesShellRightPaneView()
    {
        InitializeComponent();

        ConfigureButtons();
        SelectionActionCard.SizeChanged += (_, _) => UpdateSelectionActionSpacerHeight();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => DetachShellViewModel();
    }

    private void ConfigureButtons()
    {
        ManageTargetButton.IconData = FrontendIconCatalog.SettingsFilledData;
        ManageTargetButton.IconScale = FrontendIconCatalog.SettingsFilledScale;

        var installIcon = FrontendIconCatalog.GetNavigationIcon("download");
        SelectionInstallButton.IconData = installIcon.Data;
        SelectionInstallButton.IconScale = installIcon.Scale;

        var shareIcon = FrontendIconCatalog.GetSidebarIcon("online");
        SelectionShareButton.IconData = shareIcon.Data;
        SelectionShareButton.IconScale = shareIcon.Scale;

        SelectionFavoriteButton.IconData = FrontendIconCatalog.FavoriteOutline.Data;
        SelectionFavoriteButton.IconScale = FrontendIconCatalog.FavoriteOutline.Scale;
        SelectionRemoveButton.IconData = FrontendIconCatalog.FavoriteFilled.Data;
        SelectionRemoveButton.IconScale = FrontendIconCatalog.FavoriteFilled.Scale;
        var selectAllIcon = FrontendIconCatalog.GetSidebarIcon("game_manage");
        SelectionSelectAllButton.IconData = selectAllIcon.Data;
        SelectionSelectAllButton.IconScale = selectAllIcon.Scale;
        SelectionCancelButton.IconData = FrontendIconCatalog.Close.Data;
        SelectionCancelButton.IconScale = 0.8;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachShellViewModel();

        _shellViewModel = DataContext as FrontendShellViewModel;
        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
        }

        _ = UpdateSelectionActionCardAsync(animated: false);
    }

    private void DetachShellViewModel()
    {
        if (_shellViewModel is null)
        {
            return;
        }

        _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
        _shellViewModel = null;
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrontendShellViewModel.ShowDownloadFavoriteBatchActions))
        {
            Dispatcher.UIThread.Post(() => _ = UpdateSelectionActionCardAsync(animated: true), DispatcherPriority.Background);
        }
    }

    private bool ShouldShowSelectionActionCard()
    {
        return _shellViewModel?.ShowDownloadFavoriteBatchActions == true;
    }

    private async Task UpdateSelectionActionCardAsync(bool animated)
    {
        var shouldShow = ShouldShowSelectionActionCard();
        var wasShown = _selectionActionCardShown;
        var version = ++_selectionActionAnimationVersion;

        UpdateSelectionActionSpacerHeight();

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

    private void UpdateSelectionActionSpacerHeight()
    {
        if (!ShouldShowSelectionActionCard())
        {
            SelectionActionSpacer.Height = 0d;
            return;
        }

        var measuredHeight = SelectionActionCard.Bounds.Height > 0
            ? SelectionActionCard.Bounds.Height + SelectionActionSpacerPadding
            : 0d;
        SelectionActionSpacer.Height = Math.Max(SelectionActionSpacerMinimumHeight, measuredHeight);
    }

    private static global::Avalonia.Media.Transformation.TransformOperations ParseTransform(string value)
    {
        return global::Avalonia.Media.Transformation.TransformOperations.Parse(value);
    }
}
