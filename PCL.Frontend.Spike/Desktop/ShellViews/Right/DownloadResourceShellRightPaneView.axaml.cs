using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.ShellViews.Right;

internal sealed partial class DownloadResourceShellRightPaneView : UserControl
{
    private static readonly IBrush SearchRowIdleBackgroundBrush = Brush.Parse("#F8FBFF");
    private static readonly IBrush SearchRowIdleBorderBrush = Brush.Parse("#D5E6FD");
    private static readonly IBrush SearchRowHoverBackgroundBrush = Brush.Parse("#F2F7FF");
    private static readonly IBrush SearchRowHoverBorderBrush = Brush.Parse("#B7CDEE");
    private static readonly IBrush TransparentBrush = Brushes.Transparent;

    private readonly TextBox _searchTextBox;
    private readonly Border _searchRowBorder;
    private bool _isSearchRowHovered;

    public DownloadResourceShellRightPaneView()
    {
        InitializeComponent();
        _searchRowBorder = this.FindControl<Border>("SearchRowBorder")
            ?? throw new InvalidOperationException("下载资源页面未找到搜索框容器。");
        _searchTextBox = this.FindControl<TextBox>("DownloadResourceSearchTextBox")
            ?? throw new InvalidOperationException("下载资源页面未找到搜索文本框。");
        _searchTextBox.KeyDown += SearchTextBoxOnKeyDown;
        _searchTextBox.GotFocus += (_, _) => ApplySearchTextBoxChrome();
        _searchTextBox.LostFocus += (_, _) => ApplySearchTextBoxChrome();
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
}
