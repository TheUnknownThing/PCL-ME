using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Windows.Input;
using PCL.Frontend.Spike.Desktop.Animation;
using PCL.Frontend.Spike.Icons;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclCard : UserControl
{
    private static readonly Thickness StandardHeaderTextMargin = new(15, 12, 0, 0);
    private static readonly Thickness CollapsibleHeaderTextMargin = new(15, 0, 40, 0);
    private static readonly Thickness StandardChevronMargin = new(0, 17, 16, 0);
    private static readonly Thickness CollapsibleChevronMargin = new(0, 12, 16, 0);

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

    public static readonly StyledProperty<object?> HeaderOverlayContentProperty =
        AvaloniaProperty.Register<PclCard, object?>(nameof(HeaderOverlayContent));

    public static readonly StyledProperty<ICommand?> HeaderCommandProperty =
        AvaloniaProperty.Register<PclCard, ICommand?>(nameof(HeaderCommand));

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<PclCard, bool>(nameof(IsClosable));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<PclCard, ICommand?>(nameof(CloseCommand));

    private bool _isHovered;
    private bool _isCloseAnimationPending;

    public PclCard()
    {
        InitializeComponent();

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        HeaderButton.Click += OnHeaderButtonClick;
        CloseButton.IconData = FrontendIconCatalog.Close.Data;
        CloseButton.Click += OnCloseButtonClick;
        RefreshHeaderLayout();
        RefreshHeaderMetrics();
        RefreshChevronState();
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

    public object? HeaderOverlayContent
    {
        get => GetValue(HeaderOverlayContentProperty);
        set => SetValue(HeaderOverlayContentProperty, value);
    }

    public ICommand? HeaderCommand
    {
        get => GetValue(HeaderCommandProperty);
        set => SetValue(HeaderCommandProperty, value);
    }

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public bool HasHeader => !string.IsNullOrWhiteSpace(Header);

    public bool HasHeaderOverlayContent => HeaderOverlayContent is not null;

    public bool IsContentVisible => !ShowChevron || IsChevronExpanded;

    public bool ShowCloseButton => IsClosable && !ShowChevron && CloseCommand is not null;

    public Thickness EffectiveContentMargin
    {
        get
        {
            if (!HasHeader)
            {
                return ContentMargin;
            }

            if (ShowChevron)
            {
                return new Thickness(
                    ContentMargin.Left,
                    Math.Max(0, ContentMargin.Top - 40),
                    ContentMargin.Right,
                    ContentMargin.Bottom);
            }

            if (ContentMargin.Top < 30)
            {
                return ContentMargin;
            }

            // Most migrated cards copied the original absolute top inset,
            // so in Avalonia they end up counting both the header row and the old inset.
            return new Thickness(
                ContentMargin.Left,
                Math.Max(10, ContentMargin.Top - 28),
                ContentMargin.Right,
                ContentMargin.Bottom);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HeaderProperty)
        {
            HeaderTextBlock.Text = change.GetNewValue<string>() ?? string.Empty;
            RaisePropertyChanged(HasHeaderProperty, false, HasHeader);
            RefreshHeaderLayout();
            RefreshHeaderMetrics();
            RaisePropertyChanged(EffectiveContentMarginProperty, default, EffectiveContentMargin);
        }
        else if (change.Property == IsChevronExpandedProperty)
        {
            RefreshChevronState();
            RaisePropertyChanged(IsContentVisibleProperty, !change.GetNewValue<bool>(), IsContentVisible);
        }
        else if (change.Property == ShowChevronProperty)
        {
            RefreshHeaderLayout();
            RefreshHeaderMetrics();
            RefreshChevronState();
            RefreshState();
            RaisePropertyChanged(ShowCloseButtonProperty, false, ShowCloseButton);
            RaisePropertyChanged(IsContentVisibleProperty, false, IsContentVisible);
            RaisePropertyChanged(EffectiveContentMarginProperty, default, EffectiveContentMargin);
        }
        else if (change.Property == ContentMarginProperty)
        {
            RaisePropertyChanged(EffectiveContentMarginProperty, default, EffectiveContentMargin);
        }
        else if (change.Property == HeaderCommandProperty)
        {
            RefreshState();
        }
        else if (change.Property == IsClosableProperty || change.Property == CloseCommandProperty)
        {
            RaisePropertyChanged(ShowCloseButtonProperty, false, ShowCloseButton);
        }
        else if (change.Property == HeaderOverlayContentProperty)
        {
            RaisePropertyChanged(HasHeaderOverlayContentProperty, false, HasHeaderOverlayContent);
        }
        else if (change.Property == IsVisibleProperty && change.GetNewValue<bool>() && !_isCloseAnimationPending)
        {
            ResetCloseVisualState();
        }
    }

    private static readonly DirectProperty<PclCard, bool> HasHeaderProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(HasHeader), x => x.HasHeader);

    private static readonly DirectProperty<PclCard, bool> HasHeaderOverlayContentProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(HasHeaderOverlayContent), x => x.HasHeaderOverlayContent);

    private static readonly DirectProperty<PclCard, bool> IsContentVisibleProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(IsContentVisible), x => x.IsContentVisible);

    private static readonly DirectProperty<PclCard, Thickness> EffectiveContentMarginProperty =
        AvaloniaProperty.RegisterDirect<PclCard, Thickness>(nameof(EffectiveContentMargin), x => x.EffectiveContentMargin);

    private static readonly DirectProperty<PclCard, bool> ShowCloseButtonProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(ShowCloseButton), x => x.ShowCloseButton);

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
        var isHeaderInteractive = ShowChevron || HeaderCommand is not null;
        CardBorder.BorderBrush = _isHovered
            ? Brush.Parse("#28D5E6FD")
            : Brush.Parse("#00FFFFFF");
        CardBorder.Background = _isHovered
            ? Brush.Parse("#E6FFFFFF")
            : Brush.Parse("#CDFFFFFF");
        CardBorder.BoxShadow = BoxShadows.Parse(_isHovered
            ? "0 10 24 0 #200B5BCB"
            : "0 6 18 0 #14343D4A");
        HeaderTextBlock.Foreground = _isHovered
            ? Brush.Parse("#0B5BCB")
            : Brush.Parse("#343D4A");
        ChevronPath.Fill = HeaderTextBlock.Foreground;
        HeaderButton.Cursor = isHeaderInteractive ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow);
        HeaderButton.IsHitTestVisible = isHeaderInteractive;
    }

    private void RefreshHeaderLayout()
    {
        LayoutRoot.RowDefinitions[0].Height = HasHeader
            ? (ShowChevron ? new GridLength(40) : GridLength.Auto)
            : new GridLength(0);
        CardBorder.MinHeight = HasHeader && ShowChevron ? 40 : 0;
    }

    private void RefreshHeaderMetrics()
    {
        HeaderTextBlock.Margin = ShowChevron ? CollapsibleHeaderTextMargin : StandardHeaderTextMargin;
        HeaderTextBlock.VerticalAlignment = ShowChevron ? Avalonia.Layout.VerticalAlignment.Center : Avalonia.Layout.VerticalAlignment.Top;
        ChevronPath.Margin = ShowChevron ? CollapsibleChevronMargin : StandardChevronMargin;
        ChevronPath.VerticalAlignment = ShowChevron ? Avalonia.Layout.VerticalAlignment.Center : Avalonia.Layout.VerticalAlignment.Top;
    }

    private void RefreshChevronState()
    {
        ChevronPath.RenderTransform = new RotateTransform(IsChevronExpanded ? 180 : 270);
    }

    private void OnHeaderButtonClick(object? sender, RoutedEventArgs e)
    {
        if (HeaderCommand is not null)
        {
            if (HeaderCommand.CanExecute(null))
            {
                HeaderCommand.Execute(null);
            }

            return;
        }

        if (ShowChevron)
        {
            SetCurrentValue(IsChevronExpandedProperty, !IsChevronExpanded);
        }
    }

    private async void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_isCloseAnimationPending || !ShowCloseButton || CloseCommand?.CanExecute(null) != true)
        {
            return;
        }

        _isCloseAnimationPending = true;
        try
        {
            Motion.SetExitOffsetX(this, 0);
            Motion.SetExitOffsetY(this, -10);
            await Motion.PlayExitAsync(this);
            CloseCommand.Execute(null);
        }
        finally
        {
            _isCloseAnimationPending = false;
            if (IsVisible)
            {
                ResetCloseVisualState();
            }
        }
    }

    private void ResetCloseVisualState()
    {
        Opacity = 1;
        if (RenderTransform is TranslateTransform transform)
        {
            transform.X = 0;
            transform.Y = 0;
        }
        else
        {
            RenderTransform = new TranslateTransform();
        }
    }
}
