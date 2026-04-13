using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclSearchBox : UserControl
{
    private static readonly IBrush IdleBackgroundBrush = Brush.Parse("#F8FBFF");
    private static readonly IBrush IdleBorderBrush = Brush.Parse("#D5E6FD");
    private static readonly IBrush HoverBackgroundBrush = Brush.Parse("#F2F7FF");
    private static readonly IBrush HoverBorderBrush = Brush.Parse("#B7CDEE");

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclSearchBox, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<PclSearchBox, string>(nameof(Watermark), string.Empty);

    public static readonly StyledProperty<bool> ShowSearchButtonProperty =
        AvaloniaProperty.Register<PclSearchBox, bool>(nameof(ShowSearchButton));

    public static readonly StyledProperty<string> SearchButtonTextProperty =
        AvaloniaProperty.Register<PclSearchBox, string>(nameof(SearchButtonText), "搜索");

    public static readonly StyledProperty<ICommand?> SearchCommandProperty =
        AvaloniaProperty.Register<PclSearchBox, ICommand?>(nameof(SearchCommand));

    private bool _isHovered;

    public PclSearchBox()
    {
        InitializeComponent();

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        SearchTextBox.GotFocus += OnSearchTextBoxFocusChanged;
        SearchTextBox.LostFocus += OnSearchTextBoxFocusChanged;
        SearchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        ClearButton.Click += OnClearButtonClick;

        RefreshClearButtonState();
        RefreshLayoutMetrics();
        RefreshChrome();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public bool ShowSearchButton
    {
        get => GetValue(ShowSearchButtonProperty);
        set => SetValue(ShowSearchButtonProperty, value);
    }

    public string SearchButtonText
    {
        get => GetValue(SearchButtonTextProperty);
        set => SetValue(SearchButtonTextProperty, value);
    }

    public ICommand? SearchCommand
    {
        get => GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            RefreshClearButtonState();
            RefreshLayoutMetrics();
        }
        else if (change.Property == ShowSearchButtonProperty)
        {
            RefreshLayoutMetrics();
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isHovered = true;
        RefreshChrome();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isHovered = false;
        RefreshChrome();
    }

    private void OnSearchTextBoxFocusChanged(object? sender, RoutedEventArgs e)
    {
        RefreshChrome();
    }

    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || SearchCommand?.CanExecute(null) != true)
        {
            return;
        }

        e.Handled = true;
        SearchCommand.Execute(null);
    }

    private void OnClearButtonClick(object? sender, RoutedEventArgs e)
    {
        Text = string.Empty;
        SearchTextBox.Focus();
    }

    private void RefreshClearButtonState()
    {
        var hasText = !string.IsNullOrWhiteSpace(Text);
        ClearButton.IsEnabled = hasText;
        ClearButton.Opacity = hasText ? 1 : 0;
        ClearButton.IsHitTestVisible = hasText;
    }

    private void RefreshLayoutMetrics()
    {
        var hasText = !string.IsNullOrWhiteSpace(Text);
        SearchTextBox.Padding = new Thickness(34, 0, ShowSearchButton
            ? (hasText ? 102 : 78)
            : 40, 0);
        ClearButton.Margin = ShowSearchButton
            ? new Thickness(0, 0, 70, 0)
            : new Thickness(0, 0, 10, 0);
    }

    private void RefreshChrome()
    {
        var isActive = _isHovered || SearchTextBox.IsFocused;
        SearchRowBorder.Background = isActive ? HoverBackgroundBrush : IdleBackgroundBrush;
        SearchRowBorder.BorderBrush = isActive ? HoverBorderBrush : IdleBorderBrush;
    }
}
