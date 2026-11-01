using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.UI.Controls;
using PCL.UI.Commands;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private sealed record PageRegistration(IExtensionContext Context, ExtensionPage Definition, Func<ExtensionSurfaceContext, Control> Create);
    private sealed record SectionRegistration(IExtensionContext Context, ExtensionSection Definition, Func<ExtensionSurfaceContext, Control> Create);
    private sealed record ContextActionRegistration(IExtensionContext Context, ExtensionContextAction Definition,
        Func<ExtensionActionContext, CancellationToken, ValueTask> Execute, Func<ExtensionActionContext, bool>? CanExecute);
    private sealed class MountedContribution(CancellationTokenSource lifetime, Control content) : IDisposable
    {
        public void Dispose()
        {
            try { lifetime.Cancel(); }
            finally { try { (content as IDisposable)?.Dispose(); } finally { lifetime.Dispose(); } }
        }
    }
    private readonly Dictionary<string, PageRegistration> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SectionRegistration> _sections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContextActionRegistration> _contextActions = new(StringComparer.Ordinal);
    private sealed record MountedSection(SectionRegistration Registration, MountedContribution Mount, PclCard Card, ExtensionSurfaceContext Surface);
    private readonly Dictionary<ExtensionRegionHost, Dictionary<string, MountedSection>> _regions = [];
    private readonly Dictionary<ContextMenu, IExtensionContext[]> _contributionMenus = [];
    internal IReadOnlyList<ContextMenu> OpenContributionMenus => _contributionMenus.Keys.Where(menu => menu.IsOpen).ToArray();
    private MountedContribution? _contributionPageMount;
    private string? _contributionPageId;
    private string? _contributionRoute;
    private ExtensionSurfaceContext? _surfaceSelection;
    private bool _refreshingContributions;
    private bool _contributionRefreshQueued;
    private bool _recreateContributionsQueued;
    private IReadOnlyList<LauncherLocation> _contributionLocations = [];
    private static readonly string[] SupportedRegions =
    [ExtensionRegions.DownloadInstallExtras, ExtensionRegions.DownloadCatalogExtras, ExtensionRegions.DownloadResourceExtras,
        ExtensionRegions.InstanceOverviewCards, ExtensionRegions.InstanceResourceExtras, ExtensionRegions.SettingsSections];

    private static void ValidateContributionId(IExtensionContext context, string id)
    {
        context.Lifetime.ThrowIfCancellationRequested();
        if (!id.StartsWith(context.Manifest.Id + ".", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(id[(context.Manifest.Id.Length + 1)..]))
            throw new ArgumentException("Contribution IDs must use the extension namespace.", nameof(id));
    }

    internal void AttachRegion(ExtensionRegionHost region)
    {
        if (_regions.TryAdd(region, [])) RefreshContributions();
    }
    internal void DetachRegion(ExtensionRegionHost region)
    {
        if (_regions.Remove(region, out var mounts))
            foreach (var mount in mounts.Values) DisposeSection(mount);
        region.Children.Clear();
    }
    private void DisposeSection(MountedSection section)
    {
        try { section.Mount.Dispose(); } catch (Exception ex) { Host.LogContributionError(section.Registration.Context.Manifest.Id, ex); }
    }
    private ExtensionSurfaceContext SurfaceContext(CancellationToken lifetime, bool install = false, string? region = null)
    {
        var context = _launcher.GetExtensionSurfaceContext(install, region);
        return context with { Lifetime = lifetime };
    }
    private void RefreshRegion(ExtensionRegionHost region, bool recreate = false)
    {
        if (!_regions.TryGetValue(region, out var mounts)) return;
        var definitions = _sections.Values.Where(item => item.Definition.Region == region.Region)
            .OrderBy(item => item.Definition.Order).ThenBy(item => item.Definition.Id, StringComparer.Ordinal).ToArray();
        var surface = _launcher.GetExtensionSurfaceContext(region.Region == ExtensionRegions.DownloadInstallExtras, region.Region);
        foreach (var (id, mount) in mounts.ToArray())
        {
            if (!recreate && mount.Surface == surface && definitions.Any(item => ReferenceEquals(item, mount.Registration))) continue;
            mounts.Remove(id); DisposeSection(mount); region.Children.Remove(mount.Card);
        }
        foreach (var item in definitions)
        {
            if (mounts.ContainsKey(item.Definition.Id)) continue;
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(item.Context.Lifetime);
            try
            {
                var content = item.Create(surface with { Lifetime = lifetime.Token })
                              ?? throw new InvalidOperationException("The extension section factory returned null.");
                var card = new PclCard
                {
                    Name = "ExtensionSection_" + item.Definition.Id.Replace('.', '_').Replace('-', '_'),
                    Header = Localize(item.Context, item.Definition.TitleKey, item.Definition.Title), CardContent = content
                };
                mounts.Add(item.Definition.Id, new(item, new MountedContribution(lifetime, content), card, surface));
            }
            catch (Exception ex) { CancelFailedMount(lifetime, item.Context); Host.LogContributionError(item.Context.Manifest.Id, ex); }
        }
        var index = 0;
        foreach (var definition in definitions)
            if (mounts.TryGetValue(definition.Definition.Id, out var mount))
            {
                var current = region.Children.IndexOf(mount.Card);
                if (current < 0) region.Children.Insert(index, mount.Card);
                else if (current != index) region.Children.Move(current, index);
                index++;
            }
    }
    private void RefreshContributions(bool recreate = false)
    {
        if (_disposed) return;
        if (_refreshingContributions)
        {
            _recreateContributionsQueued |= recreate;
            if (!_contributionRefreshQueued)
            {
                _contributionRefreshQueued = true;
                Dispatcher.UIThread.Post(() =>
                {
                    _contributionRefreshQueued = false;
                    var force = _recreateContributionsQueued;
                    _recreateContributionsQueued = false;
                    RefreshContributions(force);
                });
            }
            return;
        }
        _refreshingContributions = true;
        try
        {
            _contributionLocations = _pages.Values.OrderBy(item => item.Definition.Order).ThenBy(item => item.Definition.Id, StringComparer.Ordinal)
                .Select(item => new LauncherLocation("extension-page/" + item.Definition.Id,
                    Localize(item.Context, item.Definition.TitleKey, item.Definition.Title),
                    _locations.FirstOrDefault(location => location.Route == PageAreaRoute(item.Definition.Area))?.Title ?? item.Definition.Area.ToString())).ToArray();
            var route = _launcher.ExtensionRoute;
            if (_contributionRoute != route)
            {
                _contributionRoute = route;
                CloseContributionPage();
            }
            if (_contributionPageId is { } page && !_pages.ContainsKey(page)) CloseContributionPage();
            var surface = _launcher.GetExtensionSurfaceContext();
            var selectionChanged = surface != _surfaceSelection;
            _surfaceSelection = surface;
            foreach (var region in _regions.Keys.ToArray()) RefreshRegion(region, recreate);
            if ((selectionChanged || recreate) && _contributionPageId is { } selected) ShowContributionPage(selected);
            var entries = _pages.Values.Where(item => item.Definition.Area == _launcher.ExtensionPageArea)
                .OrderBy(item => item.Definition.Order).ThenBy(item => item.Definition.Id, StringComparer.Ordinal)
                .Select(item => new ExtensionNavigationEntry(Localize(item.Context, item.Definition.TitleKey, item.Definition.Title),
                    item.Definition.Id == _contributionPageId, new ActionCommand(() => ShowContributionPage(item.Definition.Id)))).ToArray();
            _launcher.SetExtensionNavigation(entries);

        }
        finally { _refreshingContributions = false; }
    }
    private void ShowContributionPage(string id)
    {
        if (!CanInteract || !_pages.TryGetValue(id, out var page)) return;
        CloseContributionPage();
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(page.Context.Lifetime);
        try
        {
            var content = page.Create(SurfaceContext(lifetime.Token)) ?? throw new InvalidOperationException("The extension page factory returned null.");
            _contributionPageMount = new(lifetime, content);
            _contributionPageId = id;
            _launcher.SetExtensionPage(content);
            RefreshContributions();
        }
        catch (Exception ex)
        {
            if (_contributionPageId == id) CloseContributionPage(); else CancelFailedMount(lifetime, page.Context);
            Host.LogContributionError(page.Context.Manifest.Id, ex);
        }
    }
    private void CancelFailedMount(CancellationTokenSource lifetime, IExtensionContext context)
    {
        try { lifetime.Cancel(); }
        catch (Exception ex) { Host.LogContributionError(context.Manifest.Id, ex); }
        finally { lifetime.Dispose(); }
    }

    internal void CloseContributionPage()
    {
        var owner = _contributionPageId is { } id ? _pages.GetValueOrDefault(id)?.Context.Manifest.Id : null;
        _contributionPageId = null;
        try { _contributionPageMount?.Dispose(); }
        catch (Exception ex) { if (owner is not null) Host.LogContributionError(owner, ex); }
        _contributionPageMount = null;
        _launcher.SetExtensionPage(null);
    }

    internal ContextMenu CreateContributionMenu(ExtensionContextActionLocation location, ExtensionActionContext target)
    {
        var menu = new ContextMenu();
        var owners = new List<IExtensionContext>();
        foreach (var item in _contextActions.Values.Where(item => item.Definition.Location == location ||
                         location == ExtensionContextActionLocation.Mod && item.Definition.Location == ExtensionContextActionLocation.Resource)
                     .OrderBy(item => item.Definition.Order).ThenBy(item => item.Definition.Id, StringComparer.Ordinal))
        {
            bool enabled;
            try { enabled = item.CanExecute?.Invoke(target) ?? true; }
            catch (Exception ex) { Host.LogContributionError(item.Context.Manifest.Id, ex); continue; }
            var entry = new MenuItem { Header = Localize(item.Context, item.Definition.TitleKey, item.Definition.Title), IsEnabled = enabled };
            owners.Add(item.Context);
            entry.Click += (_, _) => Run(async () =>
            {
                item.Context.Lifetime.ThrowIfCancellationRequested();
                if (!_contextActions.ContainsKey(item.Definition.Id)) return;
                try { await item.Execute(target, item.Context.Lifetime); }
                catch (OperationCanceledException) when (item.Context.Lifetime.IsCancellationRequested) { }
                catch (ExtensionOperationException ex) { Notify(ex.Message, true); }
                catch (Exception ex) { Host.LogContributionError(item.Context.Manifest.Id, ex); Notify(ex.Message, true); }
            });
            menu.Items.Add(entry);
        }
        if (menu.ItemCount > 0)
        {
            _contributionMenus[menu] = owners.ToArray();
            menu.Closed += (_, _) => _contributionMenus.Remove(menu);
        }
        return menu;
    }

    private sealed class ScopedContributions(FrontendExtensionSession session, IExtensionContext context) : IExtensionContributions
    {
        public ValueTask ShowPageAsync(string pageId, CancellationToken cancellationToken = default)
        {
            ValidateContributionId(context, pageId);
            return session.NavigateAsync("extension-page/" + pageId, cancellationToken: cancellationToken);
        }
        public IDisposable RegisterPage(ExtensionPage page, Func<ExtensionSurfaceContext, Control> create)
        {
            Dispatcher.UIThread.VerifyAccess(); ValidateContributionId(context, page.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(page.Title);
            if (!Enum.IsDefined(page.Area)) throw new ArgumentException("Unknown page area.");
            session._pages.Add(page.Id, new(context, page, create));
            session.RefreshContributions();
            return context.Own(new ExtensionRegistration(() =>
            {
                session._pages.Remove(page.Id);
                session.RefreshContributions();
            }));
        }
        public IDisposable RegisterSection(ExtensionSection section, Func<ExtensionSurfaceContext, Control> create)
        {
            Dispatcher.UIThread.VerifyAccess(); ValidateContributionId(context, section.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(section.Title);
            if (!SupportedRegions.Contains(section.Region, StringComparer.Ordinal)) throw new ArgumentException("Unknown extension region.");
            session._sections.Add(section.Id, new(context, section, create));
            session.RefreshContributions();
            return context.Own(new ExtensionRegistration(() =>
            {
                session._sections.Remove(section.Id);
                session.RefreshContributions();
            }));
        }
        public IDisposable RegisterAction(ExtensionContextAction action, Func<ExtensionActionContext, CancellationToken, ValueTask> execute,
            Func<ExtensionActionContext, bool>? canExecute = null)
        {
            Dispatcher.UIThread.VerifyAccess(); ValidateContributionId(context, action.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(action.Title);
            if (!Enum.IsDefined(action.Location)) throw new ArgumentException("Unknown context action location.");
            session._contextActions.Add(action.Id, new(context, action, execute, canExecute));
            return context.Own(new ExtensionRegistration(() =>
            {
                session._contextActions.Remove(action.Id);
                foreach (var menu in session._contributionMenus.Where(pair => pair.Value.Contains(context)).Select(pair => pair.Key).ToArray())
                { menu.Close(); session._contributionMenus.Remove(menu); }
            }));
        }
    }

    private static string PageAreaRoute(ExtensionPageArea area) => area == ExtensionPageArea.Settings ? "Setup" : area.ToString();
}
