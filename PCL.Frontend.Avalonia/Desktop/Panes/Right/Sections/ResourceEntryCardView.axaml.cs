using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right.Sections;

internal sealed partial class ResourceEntryCardView : UserControl
{
    private const double SelectedBarHeight = 32;

    private InstanceResourceEntryViewModel? _entryViewModel;
    private bool _isPressed;
    private bool? _selectionBarSelectedState;

    public ResourceEntryCardView()
    {
        InitializeComponent();
        SelectionBarMotion.Initialize(SelectionBar);

        DataContextChanged += OnDataContextChanged;
        LayoutRoot.PointerEntered += (_, _) => RefreshVisualState();
        LayoutRoot.PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshVisualState();
        };
        LayoutRoot.PointerPressed += OnLayoutRootPointerPressed;
        LayoutRoot.PointerReleased += OnLayoutRootPointerReleased;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_entryViewModel is not null)
        {
            _entryViewModel.PropertyChanged -= OnEntryPropertyChanged;
        }

        _entryViewModel = DataContext as InstanceResourceEntryViewModel;

        if (_entryViewModel is not null)
        {
            _entryViewModel.PropertyChanged += OnEntryPropertyChanged;
        }

        _isPressed = false;
        _selectionBarSelectedState = null;
        RefreshVisualState();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InstanceResourceEntryViewModel.IsSelected)
            or nameof(InstanceResourceEntryViewModel.ShowSelection)
            or nameof(InstanceResourceEntryViewModel.HasStandardActionStack)
            or nameof(InstanceResourceEntryViewModel.HasTags)
            or nameof(InstanceResourceEntryViewModel.IsEnabledState))
        {
            RefreshVisualState();
        }
    }

    private void OnLayoutRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(LayoutRoot).Properties.IsLeftButtonPressed || IsPointerOverAction())
        {
            return;
        }

        _isPressed = true;
        RefreshVisualState();
    }

    private void OnLayoutRootPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_entryViewModel is null)
        {
            _isPressed = false;
            RefreshVisualState();
            return;
        }

        if (e.InitialPressMouseButton == MouseButton.Right && !IsPointerOverAction())
        {
            if (_entryViewModel.InfoCommand?.CanExecute(null) == true)
            {
                _entryViewModel.InfoCommand.Execute(null);
            }

            _isPressed = false;
            RefreshVisualState();
            return;
        }

        var shouldHandle = _isPressed
            && e.InitialPressMouseButton == MouseButton.Left
            && !IsPointerOverAction();

        _isPressed = false;

        if (shouldHandle)
        {
            if (_entryViewModel.ShowSelection)
            {
                _entryViewModel.IsSelected = !_entryViewModel.IsSelected;
            }
            else if (_entryViewModel.ActionCommand.CanExecute(null))
            {
                _entryViewModel.ActionCommand.Execute(null);
            }
        }

        RefreshVisualState();
    }

    private bool IsPointerOverAction()
    {
        return ActionStack.IsHitTestVisible && ActionStack.IsPointerOver;
    }

    private void RefreshVisualState()
    {
        var entry = _entryViewModel;
        var isSelected = entry?.ShowSelection == true && entry.IsSelected;
        var isHovered = LayoutRoot.IsPointerOver;
        var showActionStack = entry?.HasStandardActionStack == true && (isHovered || isSelected || entry.ShowSelection is false);

        ApplySelectionBarState(isSelected);
        HoverBackground.Opacity = isHovered || isSelected ? 1.0 : 0.0;
        HoverBackground.Background = isSelected
            ? isHovered
                ? GetBrush("ColorBrushEntrySelectedHoverBackground")
                : GetBrush("ColorBrushEntrySelectedBackground")
            : _isPressed && isHovered
                ? GetBrush("ColorBrush6")
                : GetBrush("ColorBrushEntryHoverBackground");
        HoverBackground.BorderBrush = isSelected
            ? GetBrush("ColorBrush6")
            : GetBrush("ColorBrushTransparent");
        LayoutRoot.RenderTransform = _isPressed && isHovered ? new ScaleTransform(0.996, 0.996) : new ScaleTransform(1, 1);

        ActionStack.Opacity = showActionStack ? 1.0 : 0.0;
        ActionStack.IsHitTestVisible = showActionStack;
        LayoutRoot.ColumnDefinitions[5].Width = showActionStack ? GridLength.Auto : new GridLength(0);
    }

    private void ApplySelectionBarState(bool isSelected)
    {
        SelectionBarMotion.Apply(SelectionBar, ref _selectionBarSelectedState, isSelected, SelectedBarHeight);
    }

    private static IBrush GetBrush(string resourceKey)
    {
        return FrontendThemeResourceResolver.GetBrush(resourceKey);
    }
}
