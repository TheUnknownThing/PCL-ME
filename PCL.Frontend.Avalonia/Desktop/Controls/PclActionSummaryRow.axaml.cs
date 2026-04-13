using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclActionSummaryRow : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclActionSummaryRow, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> InfoProperty =
        AvaloniaProperty.Register<PclActionSummaryRow, string>(nameof(Info), string.Empty);

    public static readonly StyledProperty<string> MetaProperty =
        AvaloniaProperty.Register<PclActionSummaryRow, string>(nameof(Meta), string.Empty);

    public static readonly StyledProperty<bool> ShowMetaProperty =
        AvaloniaProperty.Register<PclActionSummaryRow, bool>(nameof(ShowMeta));

    public static readonly StyledProperty<string> ActionTextProperty =
        AvaloniaProperty.Register<PclActionSummaryRow, string>(nameof(ActionText), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclActionSummaryRow, ICommand?>(nameof(Command));

    public PclActionSummaryRow()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
    }

    public string Meta
    {
        get => GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    public bool ShowMeta
    {
        get => GetValue(ShowMetaProperty);
        set => SetValue(ShowMetaProperty, value);
    }

    public string ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
