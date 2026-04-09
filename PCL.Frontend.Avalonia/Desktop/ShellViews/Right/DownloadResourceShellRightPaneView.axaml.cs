using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class DownloadResourceShellRightPaneView : UserControl
{
    private static readonly IBrush SearchRowIdleBackgroundBrush = Brush.Parse("#F8FBFF");
    private static readonly IBrush SearchRowIdleBorderBrush = Brush.Parse("#D5E6FD");
    private static readonly IBrush SearchRowHoverBackgroundBrush = Brush.Parse("#F2F7FF");
    private static readonly IBrush SearchRowHoverBorderBrush = Brush.Parse("#B7CDEE");
    private static readonly IBrush TransparentBrush = Brushes.Transparent;

    private readonly TextBox _searchTextBox;
    private readonly Border _searchRowBorder;
    private readonly ComboBox _sourceComboBox;
    private readonly ComboBox _tagComboBox;
    private readonly ComboBox _sortComboBox;
    private readonly ComboBox _versionComboBox;
    private readonly ComboBox _loaderComboBox;
    private FrontendShellViewModel? _observedShell;
    private bool _isSearchRowHovered;

    public DownloadResourceShellRightPaneView()
    {
        InitializeComponent();
        _searchRowBorder = this.FindControl<Border>("SearchRowBorder")
            ?? throw new InvalidOperationException("下载资源页面未找到搜索框容器。");
        _searchTextBox = this.FindControl<TextBox>("DownloadResourceSearchTextBox")
            ?? throw new InvalidOperationException("下载资源页面未找到搜索文本框。");
        _sourceComboBox = this.FindControl<ComboBox>("DownloadResourceSourceComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到来源筛选框。");
        _tagComboBox = this.FindControl<ComboBox>("DownloadResourceTagComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到标签筛选框。");
        _sortComboBox = this.FindControl<ComboBox>("DownloadResourceSortComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到排序筛选框。");
        _versionComboBox = this.FindControl<ComboBox>("DownloadResourceVersionComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到版本筛选框。");
        _loaderComboBox = this.FindControl<ComboBox>("DownloadResourceLoaderComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到加载器筛选框。");
        _searchTextBox.KeyDown += SearchTextBoxOnKeyDown;
        _searchTextBox.GotFocus += (_, _) => ApplySearchTextBoxChrome();
        _searchTextBox.LostFocus += (_, _) => ApplySearchTextBoxChrome();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => ObserveShell(null);
        PointerMoved += OnViewPointerMoved;
        PointerExited += OnViewPointerExited;
        PointerPressed += OnViewPointerPressed;
        ApplySearchTextBoxChrome();
        ApplySearchRowVisualState();
    }

    private void SearchTextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is not FrontendShellViewModel { SearchDownloadResourceCommand: var searchCommand }
            || !searchCommand.CanExecute(null))
        {
            return;
        }

        e.Handled = true;
        searchCommand.Execute(null);
    }

    private void OnViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not StyledElement source)
        {
            return;
        }

        for (StyledElement? current = source; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, _searchRowBorder))
            {
                return;
            }
        }

        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveShell(DataContext as FrontendShellViewModel);
        ScheduleSelectionRestore();
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

    private void OnViewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Source is not StyledElement source)
        {
            UpdateSearchRowHoverState(false);
            return;
        }

        UpdateSearchRowHoverState(IsWithinSearchRow(source));
    }

    private void OnViewPointerExited(object? sender, PointerEventArgs e)
    {
        UpdateSearchRowHoverState(false);
    }

    private bool IsWithinSearchRow(StyledElement source)
    {
        for (StyledElement? current = source; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, _searchRowBorder))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateSearchRowHoverState(bool isHovered)
    {
        if (_isSearchRowHovered == isHovered)
        {
            return;
        }

        _isSearchRowHovered = isHovered;
        ApplySearchRowVisualState();
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
            return;
        }

        ScheduleSelectionRestore();
    }

    private void ApplySearchRowVisualState()
    {
        _searchRowBorder.Background = _isSearchRowHovered
            ? SearchRowHoverBackgroundBrush
            : SearchRowIdleBackgroundBrush;
        _searchRowBorder.BorderBrush = _isSearchRowHovered
            ? SearchRowHoverBorderBrush
            : SearchRowIdleBorderBrush;
    }

    private void ApplySearchTextBoxChrome()
    {
        _searchTextBox.Background = TransparentBrush;
        _searchTextBox.BorderBrush = TransparentBrush;
        _searchTextBox.BorderThickness = new Thickness(0);
    }

    private void ScheduleSelectionRestore()
    {
        Dispatcher.UIThread.Post(RestoreFilterSelections, DispatcherPriority.Background);
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
