using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Windows.Input;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclCard : UserControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<PclCard, string>(nameof(Header), string.Empty);

    public static readonly StyledProperty<bool> ShowChevronProperty =
        AvaloniaProperty.Register<PclCard, bool>(nameof(ShowChevron));

    public static readonly StyledProperty<bool> IsChevronExpandedProperty =
        AvaloniaProperty.Register<PclCard, bool>(nameof(IsChevronExpanded), true);

    public static readonly StyledProperty<Thickness> ContentMarginProperty =
        AvaloniaProperty.Register<PclCard, Thickness>(nameof(ContentMargin), new Thickness(20, 38, 20, 18));

    public static readonly StyledProperty<object?> CardContentProperty =
        AvaloniaProperty.Register<PclCard, object?>(nameof(CardContent));

    public static readonly StyledProperty<ICommand?> HeaderCommandProperty =
        AvaloniaProperty.Register<PclCard, ICommand?>(nameof(HeaderCommand));

    private bool _isHovered;

    public PclCard()
    {
        InitializeComponent();

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        RefreshState();
    }

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool ShowChevron
    {
        get => GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }

    public bool IsChevronExpanded
    {
        get => GetValue(IsChevronExpandedProperty);
        set => SetValue(IsChevronExpandedProperty, value);
    }

    public Thickness ContentMargin
    {
        get => GetValue(ContentMarginProperty);
        set => SetValue(ContentMarginProperty, value);
    }

    public object? CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public ICommand? HeaderCommand
    {
        get => GetValue(HeaderCommandProperty);
        set => SetValue(HeaderCommandProperty, value);
    }

    public bool HasHeader => !string.IsNullOrWhiteSpace(Header);

    public bool IsContentVisible => !ShowChevron || IsChevronExpanded;

    public Thickness EffectiveContentMargin
    {
        get
        {
            // TODO: May require special margin where header is present.
            return ContentMargin;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HeaderProperty)
        {
            HeaderTextBlock.Text = change.GetNewValue<string>() ?? string.Empty;
            RaisePropertyChanged(HasHeaderProperty, false, HasHeader);
            RaisePropertyChanged(EffectiveContentMarginProperty, default, EffectiveContentMargin);
        }
        else if (change.Property == IsChevronExpandedProperty)
        {
            ChevronPath.RenderTransform = change.GetNewValue<bool>()
                ? new RotateTransform(180)
                : new RotateTransform(0);
            RaisePropertyChanged(IsContentVisibleProperty, !change.GetNewValue<bool>(), IsContentVisible);
        }
        else if (change.Property == ShowChevronProperty)
        {
            RaisePropertyChanged(IsContentVisibleProperty, false, IsContentVisible);
        }
        else if (change.Property == ContentMarginProperty)
        {
            RaisePropertyChanged(EffectiveContentMarginProperty, default, EffectiveContentMargin);
        }
    }

    private static readonly DirectProperty<PclCard, bool> HasHeaderProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(HasHeader), x => x.HasHeader);

    private static readonly DirectProperty<PclCard, bool> IsContentVisibleProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(IsContentVisible), x => x.IsContentVisible);

    private static readonly DirectProperty<PclCard, Thickness> EffectiveContentMarginProperty =
        AvaloniaProperty.RegisterDirect<PclCard, Thickness>(nameof(EffectiveContentMargin), x => x.EffectiveContentMargin);

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isHovered = true;
        RefreshState();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isHovered = false;
        RefreshState();
    }

    private void RefreshState()
    {
        CardShadow.Background = _isHovered
            ? Brush.Parse("#0B5BCB")
            : Brush.Parse("#343D4A");
        CardShadow.Opacity = _isHovered ? 0.3 : 0.1;
        CardBorder.BorderBrush = _isHovered
            ? Brush.Parse("#28D5E6FD")
            : Brush.Parse("#00FFFFFF");
        CardBorder.Background = _isHovered
            ? Brush.Parse("#E6FFFFFF")
            : Brush.Parse("#CDFFFFFF");
        HeaderTextBlock.Foreground = _isHovered
            ? Brush.Parse("#0B5BCB")
            : Brush.Parse("#343D4A");
        ChevronPath.Fill = HeaderTextBlock.Foreground;
        HeaderButton.Cursor = (ShowChevron || HeaderCommand is not null) ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow);
    }
}
