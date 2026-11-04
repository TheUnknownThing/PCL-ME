using System.ComponentModel;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.UI.Icons;
using PCL.Extensions.DesktopPet;
using PCL.Extensions.GlobalSearch;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession : ILauncherServices, IAsyncDisposable
{
    private readonly Window _window;
    private readonly DesktopShellController _shell;
    private readonly LauncherViewModel _launcher;
    private readonly LauncherActionService _actions;
    private readonly II18nService _text;
    private readonly StackPanel _toolbar;
    private readonly ContentControl _workspace;
    private readonly Dictionary<string, ViewRegistration> _views = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionRegistration> _uiActions = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _hostRegistrations = [];
    private string? _currentViewOwner;
    private IDisposable? _currentViewResource;
    private bool _disposed;
    private bool _refreshQueued;
    private readonly HashSet<ExtensionManagerView> _managers = [];
    private readonly Dictionary<string, int> _settingsRevisions = [];
    internal int SettingsRevision(string owner) => _settingsRevisions.GetValueOrDefault(owner);
    internal void AttachManager(ExtensionManagerView view) => _managers.Add(view);
    internal void DetachManager(ExtensionManagerView view) => _managers.Remove(view);
    private sealed record ViewRegistration(IExtensionContext Context, ExtensionView Definition, Func<ExtensionViewContext, Control> Create);
    private sealed record ActionRegistration(IExtensionContext Context, ExtensionAction Definition);

    public FrontendExtensionSession(Window window, DesktopShellController shell, LauncherViewModel launcher,
        LauncherActionService actions, II18nService text, bool safeMode)
    {
        _window = window; _shell = shell; _launcher = launcher; _actions = actions; _text = text;
        _toolbar = window.FindControl<StackPanel>("ExtensionToolbar") ?? new StackPanel { Orientation = Orientation.Horizontal };
        _workspace = window.FindControl<ContentControl>("ExtensionWorkspace") ?? new ContentControl();
        Host = new ExtensionHost(Path.Combine(actions.RuntimePaths.DataDirectory, "Extensions"), this, () => text.Locale,
            DispatchAsync, CreateServices, safeMode: safeMode,
            workerCommand: string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase)
                ? [Environment.ProcessPath!, typeof(Program).Assembly.Location] : [Environment.ProcessPath!])
        { SupportedCapabilities = Array.AsReadOnly(AllCapabilities) };
        _launcher.ExtensionSession = this;
        AddBuiltIn("pcl.desktop-pet", () => new DesktopPetExtension(), enabledByDefault: false);
        AddBuiltIn("pcl.global-search", () => new GlobalSearchExtension());
        Host.Changed += QueueRefresh;
        Host.Discover();
        text.Changed += OnLocaleChanged;
        launcher.PropertyChanged += OnLauncherChanged;
        RegisterHostServices();
        RefreshToolbar();
    }

    public ExtensionHost Host { get; }
    internal string T(string key) => _text.T("extensions." + key);
    internal string T(string key, IReadOnlyDictionary<string, object?> arguments) => _text.T("extensions." + key, arguments);
    internal bool CanInteract => !_disposed && _launcher.IsTopLevelNavigationInteractive;
    private readonly HashSet<Window> _windows = [];
    internal IReadOnlyList<Window> Windows => _windows.ToArray();

    private void AddBuiltIn(string id, Func<IExtension> factory, bool enabledByDefault = true)
    {
        // macOS codesign interprets dotted directories as nested bundles. Keep package folders extensionless.
        var directory = Path.Combine(AppContext.BaseDirectory, "BuiltInExtensions", id.Replace('.', '-'));
        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(File.ReadAllText(Path.Combine(directory, "extension.json")), ExtensionPackage.JsonOptions)!;
        Host.AddBuiltIn(manifest, directory, factory, enabledByDefault);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await StartLifecycleAsync();
        await Host.StartAsync(cancellationToken);
        Host.PublishHostEvent("launcher.ready", JsonSerializer.SerializeToElement(new { }));
        QueueRefresh();
    }

    internal static async ValueTask DispatchAsync(Func<ValueTask> action)
    {
        if (Dispatcher.UIThread.CheckAccess()) { await action(); return; }
        await Dispatcher.UIThread.InvokeAsync(() => action().AsTask());
    }

    private void OnLocaleChanged() { _locations = _launcher.GetExtensionLocations(); Host.LocaleChanged(); RefreshContributions(recreate: true); }
    private void OnLauncherChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherViewModel.CurrentStandardRightPane) or nameof(LauncherViewModel.LaunchVersionSubtitle)
            or nameof(LauncherViewModel.DownloadInstallMinecraftVersion) or nameof(LauncherViewModel.DownloadInstallOptionCards)
            or nameof(LauncherViewModel.SelectedDownloadResourceVersionOption) or nameof(LauncherViewModel.SelectedDownloadResourceLoaderOption)
            or nameof(LauncherViewModel.SelectedDownloadResourceSourceOption) or nameof(LauncherViewModel.DownloadResourceSearchQuery) or nameof(LauncherViewModel.VersionSaveTitle)
            or nameof(LauncherViewModel.HasSelectedInstance)) RefreshContributions();
        if (e.PropertyName == nameof(LauncherViewModel.IsPromptOverlayVisible) && _launcher.IsPromptOverlayVisible) _overlay?.Dispose();
        if (e.PropertyName is nameof(LauncherViewModel.LaunchVersionSubtitle) or nameof(LauncherViewModel.HasSelectedInstance))
        {
            RefreshLifecycleWatchers();
            Host.PublishHostEvent("launcher.instance.changed", JsonSerializer.SerializeToElement(_launcher.ExtensionSelectedInstanceId));
            RefreshInstanceTabs();
            RefreshContributions();
        }
    }

    private void QueueRefresh()
    {
        if (_disposed || _refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            if (_disposed) return;
            RefreshToolbar();
            RefreshInstanceTabs();
            foreach (var manager in _managers.ToArray()) manager.Refresh();
        });
    }

    internal async void Run(Func<ValueTask> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Notify(T("operation_failed") + " " + ex.Message, true); }
    }

    internal void ShowManager()
    {
        if (!CanInteract) return;
        _shell.ShowLauncher();
        CloseWorkspace();
        _launcher.OpenExtensionManager();
    }

    private void Present(Control control, string title, string? owner)
    {
        DisposeCurrentView();
        _currentViewResource = control as IDisposable;
        _currentViewOwner = owner;
        var panel = new DockPanel { Background = _workspace.Background };
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(20, 12), Spacing = 16 };
        var back = new PclIconButton { IconData = PclIcons.Back.Data, Width = 28, Height = 28, IconScale = .8 };
        global::Avalonia.Automation.AutomationProperties.SetName(back, T("back"));
        ToolTip.SetTip(back, T("back"));
        back.Click += (_, _) => CloseWorkspace();
        heading.Children.Add(back);
        heading.Children.Add(new TextBlock { Text = title, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        panel.Children.Add(control);
        _workspace.Content = panel;
        _workspace.IsVisible = true;
        control.Focus();
    }

    internal void CloseWorkspace()
    {
        DisposeCurrentView();
        _currentViewOwner = null;
        _workspace.Content = null;
        _workspace.IsVisible = false;
    }

    private void DisposeCurrentView()
    {
        try { _currentViewResource?.Dispose(); }
        catch (Exception ex) { if (_currentViewOwner is { } owner) Run(() => new ValueTask(Host.ReportFaultAsync(owner, ex))); }
        finally { _currentViewResource = null; }
    }

    private ValueTask ShowViewAsync(string id, ExtensionViewContext? context, CancellationToken token) => DispatchAsync(() =>
    {
        token.ThrowIfCancellationRequested();
        if (!CanInteract) throw new ExtensionOperationException("launcher.busy", T("unavailable"));
        var view = _views.GetValueOrDefault(id) ?? throw new KeyNotFoundException(T("unavailable"));
        var actualContext = context ?? new(_launcher.ExtensionSelectedInstanceId);
        if (view.Definition.Location == ExtensionViewLocation.InstanceTab && actualContext.InstanceId is null)
            throw new ExtensionOperationException("instance.required", T("select_instance"));
        var content = view.Create(actualContext) ?? throw new InvalidOperationException("The extension view factory returned null.");
        _shell.ShowLauncher();
        Present(content, Localize(view.Context, view.Definition.TitleKey, view.Definition.Title), view.Context.Manifest.Id);
        return ValueTask.CompletedTask;
    });

    internal static string Localize(IExtensionContext context, string? key, string fallback) => key is null ? fallback : context.Text.Get(key, fallback);

    internal bool HasExtensionMenuEntries => _views.Values.Any(view => view.Definition.Location == ExtensionViewLocation.Page)
        || _uiActions.Values.Any(item => item.Definition.Location == ExtensionActionLocation.Menu);

    internal MenuFlyout CreateExtensionMenu()
    {
        var menu = new MenuFlyout();
        foreach (var view in _views.Values.Where(view => view.Definition.Location is ExtensionViewLocation.Page))
        {
            var entry = new MenuItem { Header = Localize(view.Context, view.Definition.TitleKey, view.Definition.Title) };
            entry.Click += (_, _) => Run(() => ShowViewAsync(view.Definition.Id, null, CancellationToken.None));
            menu.Items.Add(entry);
        }
        foreach (var contribution in _uiActions.Values.Where(item => item.Definition.Location == ExtensionActionLocation.Menu))
        {
            var entry = new MenuItem { Header = Localize(contribution.Context, contribution.Definition.TitleKey, contribution.Definition.Title) };
            entry.Click += (_, _) => Run(() => ExecuteCommandAsync(contribution.Definition.CommandId));
            menu.Items.Add(entry);
        }
        return menu;
    }

    private void RefreshToolbar()
    {
        _toolbar.Children.Clear();
        foreach (var contribution in _uiActions.Values.Where(item => item.Definition.Location == ExtensionActionLocation.Toolbar))
        {
            var title = Localize(contribution.Context, contribution.Definition.TitleKey, contribution.Definition.Title);
            var button = new PclIconButton { IconData = contribution.Definition.IconData ?? PclIcons.Extensions.Data, Width = 28, Height = 28, IconScale = .85 };
            button.Bind(PclIconButton.IconBrushProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ColorBrushTitleBarForeground"));
            button.Bind(PclIconButton.HoverBackgroundBrushProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ColorBrushTitleBarHoverBackground"));
            global::Avalonia.Automation.AutomationProperties.SetName(button, title);
            ToolTip.SetTip(button, Localize(contribution.Context, contribution.Definition.TitleKey, contribution.Definition.Title));
            button.Click += (_, _) => Run(() => ExecuteCommandAsync(contribution.Definition.CommandId));
            _toolbar.Children.Add(button);
        }
        _shell.SetExtensionTrayItems(_uiActions.Values.Where(item => item.Definition.Location == ExtensionActionLocation.Tray).Select(item =>
            new NativeMenuItem
            {
                Header = Localize(item.Context, item.Definition.TitleKey, item.Definition.Title),
                Command = new ActionCommand(() => Run(() => ExecuteCommandAsync(item.Definition.CommandId)))
            }));
    }

    private ValueTask ExecuteCommandAsync(string id)
    {
        if (!CanInteract) throw new ExtensionOperationException("launcher.busy", T("unavailable"));
        return Host.ExecuteCommandAsync(id);
    }

    internal bool TryHandleShortcut(KeyEventArgs e)
    {
        if (!CanInteract || e.Handled || OverlayContent is ExtensionUrlInstallView or ExtensionDetailsView) return false;
        foreach (var command in Host.Commands.Where(command => command.CanExecute && command.Definition.Shortcut is not null))
        {
            var text = command.Definition.Shortcut!.Replace("Primary", OperatingSystem.IsMacOS() ? "Meta" : "Ctrl", StringComparison.Ordinal);
            KeyGesture gesture;
            try { gesture = KeyGesture.Parse(text); }
            catch (Exception ex) when (ex is FormatException or ArgumentException) { continue; }
            if (!gesture.Matches(e)) continue;
            Run(() => Host.ExecuteCommandAsync(command.Definition.Id));
            e.Handled = true;
            return true;
        }
        return false;
    }

    internal IReadOnlyList<Control> CreateSettingsViews(string owner)
    {
        var controls = new List<Control>();
        foreach (var view in _views.Values.Where(view => view.Context.Manifest.Id == owner && view.Definition.Location == ExtensionViewLocation.Settings).ToArray())
        {
            try { controls.Add(view.Create(new(_launcher.ExtensionSelectedInstanceId)) ?? throw new InvalidOperationException("The extension settings factory returned null.")); }
            catch (Exception ex) { Run(() => new ValueTask(Host.ReportFaultAsync(owner, ex))); }
        }
        return controls;
    }

    internal ContextMenu CreateInstanceMenu()
    {
        var menu = _launcher.ExtensionSelectedInstanceId is { } instance
            ? CreateContributionMenu(ExtensionContextActionLocation.Instance, new() { InstanceId = instance }) : new ContextMenu();
        foreach (var contribution in _uiActions.Values.Where(item => item.Definition.Location == ExtensionActionLocation.InstanceMenu))
        {
            var item = new MenuItem { Header = Localize(contribution.Context, contribution.Definition.TitleKey, contribution.Definition.Title) };
            item.Click += (_, _) => Run(() => ExecuteCommandAsync(contribution.Definition.CommandId));
            menu.Items.Add(item);
        }
        return menu;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifecycleLifetime.Cancel();
        foreach (var watcher in _lifecycleWatchers.Values) watcher.Dispose();
        _lifecycleWatchers.Clear();
        _text.Changed -= OnLocaleChanged;
        _launcher.PropertyChanged -= OnLauncherChanged;
        _launcher.ExtensionSession = null;
        Host.Changed -= QueueRefresh;
        _overlay?.Dispose();
        CloseContributionPage();
        foreach (var region in _regions.Keys.ToArray()) DetachRegion(region);
        foreach (var menu in _contributionMenus.Keys.ToArray()) menu.Close();
        _contributionMenus.Clear();
        CloseWorkspace();
        foreach (var manager in _managers.ToArray()) manager.Dispose();
        DisposeInstanceTabs();
        await Host.DisposeAsync();
        foreach (var registration in _hostRegistrations) registration.Dispose();
        _toolbar.Children.Clear();
        _shell.SetExtensionTrayItems([]);
    }

    private readonly List<(string Owner, IDisposable Resource)> _instanceTabResources = [];
    private void DisposeInstanceTabs()
    {
        foreach (var (owner, resource) in _instanceTabResources)
            try { resource.Dispose(); }
            catch (Exception ex) { Run(() => new ValueTask(Host.ReportFaultAsync(owner, ex))); }
        _instanceTabResources.Clear();
        _launcher.SetExtensionInstanceTabs(null);
    }
    private void RefreshInstanceTabs()
    {
        DisposeInstanceTabs();
        if (_launcher.ExtensionSelectedInstanceId is not { } instance) return;
        var definitions = _views.Values.Where(view => view.Definition.Location == ExtensionViewLocation.InstanceTab).ToArray();
        if (definitions.Length == 0) return;
        var tabs = definitions.Select(view => new TabItem { Header = Localize(view.Context, view.Definition.TitleKey, view.Definition.Title), Tag = view }).ToArray();
        var host = new TabControl { ItemsSource = tabs };
        void CreateSelectedView()
        {
            if ((host.SelectedItem as TabItem ?? tabs[0]) is not { Content: null, Tag: ViewRegistration view } tab) return;
            try
            {
                var content = view.Create(new(instance)) ?? throw new InvalidOperationException("The extension instance tab factory returned null.");
                tab.Content = content;
                if (content is IDisposable resource) _instanceTabResources.Add((view.Context.Manifest.Id, resource));
            }
            catch (Exception ex)
            {
                tab.Content = new TextBlock { Text = T("operation_failed") };
                Run(() => new ValueTask(Host.ReportFaultAsync(view.Context.Manifest.Id, ex)));
            }
        }
        host.SelectionChanged += (_, _) => CreateSelectedView();
        _launcher.SetExtensionInstanceTabs(host);
        host.SelectedIndex = 0;
        CreateSelectedView();
    }

    private sealed class ScopedUi : IExtensionUi
    {
        private readonly FrontendExtensionSession session;
        private readonly IExtensionContext context;
        public ScopedUi(FrontendExtensionSession session, IExtensionContext context)
        {
            this.session = session;
            this.context = context;
            context.Own(new ExtensionRegistration(() =>
            {
                if (session._overlay?.Owner == context) session._overlay.Dispose();
            }));
        }
        private void ValidateId(string id)
        {
            context.Lifetime.ThrowIfCancellationRequested();
            if (!id.StartsWith(context.Manifest.Id + ".", StringComparison.Ordinal)) throw new ArgumentException("UI contributions must use the extension namespace.", nameof(id));
        }
        public IDisposable RegisterView(ExtensionView view, Func<ExtensionViewContext, Control> create)
        {
            Dispatcher.UIThread.VerifyAccess();
            ValidateId(view.Id);
            session._views.Add(view.Id, new(context, view, create));
            if (view.Location == ExtensionViewLocation.Settings) session._settingsRevisions[context.Manifest.Id] = session.SettingsRevision(context.Manifest.Id) + 1;
            session.QueueRefresh();
            return context.Own(new ExtensionRegistration(() =>
            {
                session._views.Remove(view.Id);
                if (view.Location == ExtensionViewLocation.Settings) session._settingsRevisions[context.Manifest.Id] = session.SettingsRevision(context.Manifest.Id) + 1;
                if (session._currentViewOwner == context.Manifest.Id) session.CloseWorkspace();
                session.QueueRefresh();
            }));
        }
        public IDisposable RegisterAction(ExtensionAction action)
        {
            Dispatcher.UIThread.VerifyAccess();
            ValidateId(action.Id);
            session._uiActions.Add(action.Id, new(context, action));
            session.QueueRefresh();
            return context.Own(new ExtensionRegistration(() => { session._uiActions.Remove(action.Id); session.QueueRefresh(); }));
        }
        public ValueTask ShowViewAsync(string viewId, ExtensionViewContext? viewContext = null, CancellationToken cancellationToken = default)
        { ValidateId(viewId); return session.ShowViewAsync(viewId, viewContext, cancellationToken); }
        public IExtensionOverlay ShowOverlay(Control content, ExtensionOverlayOptions? options = null)
            => session.ShowOverlay(context, content, options ?? new());
        public IDisposable ShowWindow(Window window, ExtensionWindowOptions? options = null)
        {
            Dispatcher.UIThread.VerifyAccess();
            context.Lifetime.ThrowIfCancellationRequested();
            FrontendWindowIdentity.Apply(window);
            IDisposable? recovery = options?.IsRecoverySurface == true ? session._shell.RegisterRecoverySurface(window) : null;
            var closed = false;
            session._windows.Add(window);
            var registration = new ExtensionRegistration(() =>
            {
                session._windows.Remove(window);
                recovery?.Dispose();
                if (!closed) window.Close();
            });
            window.Closed += (_, _) => { closed = true; registration.Dispose(); };
            context.Own(registration);
            try
            {
                if (options?.ShowWithOwner == true) window.Show(session._window);
                else window.Show();
            }
            catch { registration.Dispose(); throw; }
            return registration;
        }
        public async ValueTask ShowDialogAsync(Window dialog, CancellationToken cancellationToken = default)
        {
            Dispatcher.UIThread.VerifyAccess();
            context.Lifetime.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
            FrontendWindowIdentity.Apply(dialog);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.Lifetime, cancellationToken);
            using var cancellation = linked.Token.Register(() => Dispatcher.UIThread.Post(dialog.Close));
            var closed = false;
            session._windows.Add(dialog);
            using var registration = context.Own(new ExtensionRegistration(() =>
            {
                session._windows.Remove(dialog);
                if (!closed) dialog.Close();
            }));
            dialog.Closed += (_, _) => { closed = true; registration.Dispose(); };
            await dialog.ShowDialog(session._window);
            linked.Token.ThrowIfCancellationRequested();
        }
        public void Dispatch(Action action) => Dispatcher.UIThread.Post(() =>
        {
            if (context.Lifetime.IsCancellationRequested) return;
            try { action(); }
            catch (Exception ex) { session.Run(() => new ValueTask(session.Host.ReportFaultAsync(context.Manifest.Id, ex))); }
        });
    }
}
