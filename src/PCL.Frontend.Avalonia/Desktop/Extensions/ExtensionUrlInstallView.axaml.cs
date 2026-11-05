using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using PCL.UI.Icons;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class ExtensionUrlInstallView : UserControl, IDisposable
{
    private FrontendExtensionSession? _session;
    private IReadOnlyList<ExtensionReleaseAsset> _releases = [];
    private CancellationTokenSource? _operation;
    private string? _expectedId;
    private bool _refreshingText;
    private bool _disposed;
    private IExtensionOverlay? _popup;
    internal FrontendExtensionInstallService Service { get; set; } = FrontendExtensionInstallService.Instance;
    internal event Action<ExtensionInfo>? Installed;
    internal event Action? Closed;
    internal event Action? BusyChanged;
    internal bool IsBusy => _operation is not null;

    public ExtensionUrlInstallView()
    {
        InitializeComponent();
        ReleaseChoice.GroupName = SourceChoice.GroupName = "ExtensionInstall_" + Guid.NewGuid().ToString("N");
        LoadButton.Command = new ActionCommand(Load);
        ConfirmInstallButton.Command = new ActionCommand(Install);
        CancelInstallButton.Command = new ActionCommand(Close);
        CloseInstallButton.Command = new ActionCommand(Close);
        CloseInstallButton.IconData = PclIcons.Close.Data;
        UrlInput.TextChanged += (_, _) =>
        {
            _releases = [];
            ReleaseInput.ItemsSource = null;
            BranchInput.ItemsSource = null;
            ReleaseEmptyText.IsVisible = false;
            InstallErrorText.IsVisible = false;
            UpdateOptions();
        };
        SourceChoice.IsCheckedChanged += (_, _) => { InstallErrorText.IsVisible = false; UpdateOptions(); };
        ChannelInput.SelectionChanged += (_, _) => { if (!_refreshingText) FilterReleases(); };
        ReleaseInput.SelectionChanged += (_, _) => UpdateOptions();
        BranchInput.SelectionChanged += (_, _) => UpdateOptions();
    }

    internal void Open(FrontendExtensionSession session, string? expectedId = null)
    {
        _session = session;
        _expectedId = expectedId;
        RefreshText();
        IsVisible = true;
        _popup = session.ShowOverlay(this, new ExtensionOverlayOptions
        {
            MaxWidth = 620, Margin = new(24), VerticalAlignment = VerticalAlignment.Center,
            DismissOnOutsideClick = false
        });
        _popup.Closed += () => { IsVisible = false; Closed?.Invoke(); };
        Dispatcher.UIThread.Post(() => { if (!_disposed) UrlInput.Focus(); }, DispatcherPriority.Input);
    }

    internal void RefreshText()
    {
        if (_session is not { } session) return;
        InstallTitle.Text = session.T(_expectedId is null ? "install_url" : "update_url");
        global::Avalonia.Automation.AutomationProperties.SetName(this, InstallTitle.Text);
        UrlInput.Watermark = session.T("url_hint");
        global::Avalonia.Automation.AutomationProperties.SetName(UrlInput, session.T("install_url"));
        LoadButton.Text = session.T("load_options");
        ReleaseChoice.Content = session.T("from_release");
        SourceChoice.Content = session.T("from_source");
        ReleaseLabel.Text = session.T("release_label");
        ReleaseEmptyText.Text = session.T("resource_empty");
        BranchLabel.Text = session.T("branch_label");
        var channel = Math.Max(0, ChannelInput.SelectedIndex);
        _refreshingText = true;
        ChannelInput.ItemsSource = new[] { session.T("channels.stable"), session.T("channels.beta"), session.T("channels.alpha") };
        ChannelInput.SelectedIndex = channel;
        _refreshingText = false;
        ProjectInput.Watermark = session.T("project_hint");
        SourceHint.Text = session.T("source_hint");
        InstallTrustText.Text = session.T("native_code");
        ConfirmInstallButton.Text = session.T(_expectedId is null ? "resource_install" : "update");
        CancelInstallButton.Text = session.T("cancel");
        ToolTip.SetTip(CloseInstallButton, session.T("cancel"));
        global::Avalonia.Automation.AutomationProperties.SetName(CloseInstallButton, session.T("cancel"));
        global::Avalonia.Automation.AutomationProperties.SetName(ChannelInput, session.T("release_label"));
        global::Avalonia.Automation.AutomationProperties.SetName(ReleaseInput, session.T("from_release"));
        global::Avalonia.Automation.AutomationProperties.SetName(BranchInput, session.T("branch_label"));
        global::Avalonia.Automation.AutomationProperties.SetName(ProjectInput, session.T("project_hint"));
        UpdateOptions();
    }

    private bool IsDirectPackage => Uri.TryCreate(UrlInput.Text?.Trim(), UriKind.Absolute, out var url)
        && FrontendExtensionInstallService.IsPackageUrl(url);

    private void UpdateOptions()
    {
        var busy = _operation is not null;
        var source = SourceChoice.IsChecked == true;
        RepositoryOptions.IsVisible = !IsDirectPackage;
        ReleaseOptions.IsVisible = !source;
        SourceOptions.IsVisible = source;
        LoadButton.IsVisible = !IsDirectPackage;
        UrlInput.IsEnabled = RepositoryOptions.IsEnabled = LoadButton.IsEnabled = !busy;
        ConfirmInstallButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(UrlInput.Text)
            && (IsDirectPackage || (source ? BranchInput.SelectedItem is string : ReleaseInput.SelectedItem is ExtensionReleaseAsset));
    }

    private void FilterReleases()
    {
        var selected = ReleaseInput.SelectedItem as ExtensionReleaseAsset;
        var channel = (ExtensionReleaseChannel)Math.Max(0, ChannelInput.SelectedIndex);
        var releases = _releases.Where(release => release.Channel == channel).ToArray();
        ReleaseInput.ItemsSource = releases;
        ReleaseInput.SelectedItem = releases.FirstOrDefault(release => release == selected) ?? releases.FirstOrDefault();
        ReleaseEmptyText.IsVisible = _releases.Count > 0 && ReleaseInput.ItemCount == 0;
        UpdateOptions();
    }

    private async void Load()
    {
        if (_operation is not null || _session is null || _disposed) return;
        var source = SourceChoice.IsChecked == true;
        using var operation = _operation = new CancellationTokenSource();
        BusyChanged?.Invoke();
        InstallErrorText.IsVisible = false;
        ProgressText.Text = _session.T("loading");
        ProgressText.IsVisible = true;
        UpdateOptions();
        try
        {
            var repository = FrontendExtensionInstallService.ParseRepository(FrontendExtensionInstallService.ParseUrl(UrlInput.Text ?? ""));
            if (source)
            {
                var branches = await Service.GetBranchesAsync(repository, operation.Token);
                if (_disposed || operation.IsCancellationRequested) return;
                BranchInput.ItemsSource = branches;
                BranchInput.SelectedIndex = branches.Count > 0 ? 0 : -1;
                if (branches.Count == 0) throw new ExtensionInstallException("no_branches");
            }
            else
            {
                _releases = await Service.GetReleasesAsync(repository, operation.Token);
                if (_disposed || operation.IsCancellationRequested) return;
                FilterReleases();
                if (_releases.Count == 0) throw new ExtensionInstallException("no_releases");
            }
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
        catch (Exception ex) { if (!_disposed) ShowError(ex); }
        finally
        {
            _operation = null;
            BusyChanged?.Invoke();
            ProgressText.IsVisible = false;
            UpdateOptions();
        }
    }

    private async void Install()
    {
        if (_operation is not null || _session is null || _disposed) return;
        using var operation = _operation = new CancellationTokenSource();
        BusyChanged?.Invoke();
        InstallErrorText.IsVisible = false;
        var source = !IsDirectPackage && SourceChoice.IsChecked == true;
        ProgressText.Text = _session.T(source ? "building" : "installing");
        ProgressText.IsVisible = true;
        UpdateOptions();
        try
        {
            var url = FrontendExtensionInstallService.ParseUrl(UrlInput.Text ?? "");
            ExtensionInfo info;
            if (source)
                info = await Service.InstallSourceAsync(_session.Host, FrontendExtensionInstallService.ParseRepository(url),
                    (string)BranchInput.SelectedItem!, ProjectInput.Text, _expectedId, operation.Token);
            else
                info = await Service.InstallUrlAsync(_session.Host, IsDirectPackage ? url : ((ExtensionReleaseAsset)ReleaseInput.SelectedItem!).Url,
                    _expectedId, operation.Token);
            if (_disposed) return;
            Installed?.Invoke(info);
            Close();
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
        catch (Exception ex) { if (!_disposed) ShowError(ex); }
        finally
        {
            _operation = null;
            BusyChanged?.Invoke();
            ProgressText.IsVisible = false;
            UpdateOptions();
        }
    }

    private void ShowError(Exception ex)
    {
        InstallErrorText.Text = ex is ExtensionInstallException install ? _session!.T("errors." + install.Key)
            : _session!.T("operation_failed") + " " + ex.Message;
        InstallErrorText.IsVisible = true;
    }

    internal void Close()
    {
        _popup?.Dispose();
    }

    public void Dispose()
    {
        _disposed = true;
        _operation?.Cancel();
    }
}
