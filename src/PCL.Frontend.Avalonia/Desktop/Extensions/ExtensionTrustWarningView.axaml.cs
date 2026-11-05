using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Extensions.Avalonia;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class ExtensionTrustWarningView : UserControl, IDisposable
{
    private FrontendExtensionSession? _session;
    private IExtensionOverlay? _popup;
    private bool _disposed;
    internal event Action? Acknowledged;

    public ExtensionTrustWarningView()
    {
        InitializeComponent();
        AcknowledgeButton.Command = new ActionCommand(Acknowledge);
    }

    internal void Open(FrontendExtensionSession session)
    {
        _session = session;
        RefreshText();
        IsVisible = true;
        _popup = session.ShowOverlay(this, new ExtensionOverlayOptions
        {
            MaxWidth = 620, Margin = new(25), VerticalAlignment = VerticalAlignment.Center,
            DismissOnOutsideClick = false, DismissOnEscape = false
        }, isWarning: true);
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) AcknowledgeButton.FindControl<Button>("ButtonHost")!.Focus();
        }, DispatcherPriority.Input);
    }

    internal void RefreshText()
    {
        if (_session is not { } session) return;
        WarningTitle.Text = session.T("trust_warning.title");
        WarningText.Text = session.T("trust_warning.message");
        AcknowledgeButton.Text = session.T("trust_warning.acknowledge");
        global::Avalonia.Automation.AutomationProperties.SetName(this, WarningTitle.Text);
        global::Avalonia.Automation.AutomationProperties.SetName(AcknowledgeButton, AcknowledgeButton.Text);
    }

    private void Acknowledge()
    {
        if (_disposed) return;
        _session?.AcknowledgeTrustWarning();
        Acknowledged?.Invoke();
        _popup?.Dispose();
    }

    internal void Close()
    {
        _popup?.Dispose();
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
