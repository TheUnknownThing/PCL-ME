using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclPromptCard : UserControl
{
    public static readonly StyledProperty<string> SourceProperty =
        AvaloniaProperty.Register<PclPromptCard, string>(nameof(Source), string.Empty);

    public static readonly StyledProperty<string> SeverityProperty =
        AvaloniaProperty.Register<PclPromptCard, string>(nameof(Severity), string.Empty);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclPromptCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<PclPromptCard, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<PclPromptCard, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> BadgeBackgroundBrushProperty =
        AvaloniaProperty.Register<PclPromptCard, IBrush?>(nameof(BadgeBackgroundBrush));

    public static readonly StyledProperty<object?> OptionsContentProperty =
        AvaloniaProperty.Register<PclPromptCard, object?>(nameof(OptionsContent));

    public PclPromptCard()
    {
        InitializeComponent();
    }

    public string Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush? BadgeBackgroundBrush
    {
        get => GetValue(BadgeBackgroundBrushProperty);
        set => SetValue(BadgeBackgroundBrushProperty, value);
    }

    public object? OptionsContent
    {
        get => GetValue(OptionsContentProperty);
        set => SetValue(OptionsContentProperty, value);
    }
}
