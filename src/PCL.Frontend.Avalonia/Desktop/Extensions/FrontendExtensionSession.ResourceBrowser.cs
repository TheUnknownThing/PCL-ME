using Avalonia.Controls;
using PCL.Core.App.Essentials;
using PCL.Extensions.Contracts;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;
using PCL.UI.Commands;
using PCL.UI.Controls;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private const string ResourceTargetPrefix = "extension-resource:";

    internal async Task<FrontendCommunityResourceQueryResult?> QueryExtensionResourcesAsync(LauncherFrontendSubpageKey route,
        FrontendCommunityResourceQuery query, int targetCount, CancellationToken token)
    {
        var provider = ResourceProviders().FirstOrDefault(item => item.Context is not null && item.Definition.Id == query.Source);
        if (provider is null) return null;
        var kind = ResourceKind(route) ?? throw new ExtensionOperationException("provider.kind", "The provider does not support this resource page.");
        var operations = CreateOperations(provider.Context!);
        var projects = new List<ExtensionResourceProject>();
        bool more;
        do
        {
            var page = await operations.SearchAsync(new() { ProviderId = query.Source, Kind = kind, Text = query.SearchText,
                MinecraftVersion = query.Version, Loader = query.Loader, Offset = projects.Count, Limit = Math.Min(100, Math.Max(1, targetCount - projects.Count)) }, token);
            projects.AddRange(page.Projects);
            more = page.HasMore && page.Projects.Count > 0;
        } while (more && projects.Count < targetCount);
        var entries = projects.Select(project => new FrontendDownloadResourceEntry(project.Title, project.Description, provider.Definition.Title,
            query.Version, query.Loader, [], string.IsNullOrEmpty(query.Version) ? [] : [query.Version], string.IsNullOrEmpty(query.Loader) ? [] : [query.Loader],
            "view_details", null, null, null, ResourceTargetPrefix + provider.Definition.Id + ":" + Uri.EscapeDataString(project.ProjectId), 0, 0, 0, 0, provider.Definition.Id)).ToArray();
        return new(new(provider.Definition.Title, kind != ExtensionResourceKind.World, false, kind == ExtensionResourceKind.Shader, "", [], projects.Count, more, entries), [], []);
    }

    internal ActionCommand? CreateExtensionResourceCommand(string target, string title)
    {
        if (!target.StartsWith(ResourceTargetPrefix, StringComparison.Ordinal)) return null;
        var parts = target[ResourceTargetPrefix.Length..].Split(':', 2);
        return new ActionCommand(() => Run(() => OpenExtensionResourceAsync(parts[0], Uri.UnescapeDataString(parts[1]), title)));
    }

    private async ValueTask OpenExtensionResourceAsync(string providerId, string projectId, string title)
    {
        if (!CanInteract) return;
        var provider = ResourceProviders().FirstOrDefault(item => item.Definition.Id == providerId && item.Context is not null)
                       ?? throw new ExtensionOperationException("provider.unavailable", T("unavailable"));
        var context = provider.Context!;
        var operations = CreateOperations(context);
        var view = new ResourceProjectView(context.Lifetime);
        var feedback = new TextBlock { Name = "ExtensionResourceFeedback", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        view.Children.Add(feedback);
        Present(view, title, context.Manifest.Id);
        var resource = new ExtensionResourceReference { ProviderId = providerId, ProjectId = projectId };
        var releases = await operations.GetReleasesAsync(resource, view.Lifetime);
        view.Lifetime.ThrowIfCancellationRequested();
        foreach (var release in releases)
        {
            var review = new PclButton { Name = "ReviewResource_" + release.Id.Replace('.', '_'), Text = T("resource_review") };
            var row = new StackPanel { Spacing = 10 };
            row.Children.Add(new TextBlock { Text = release.Title }); row.Children.Add(review);
            view.Children.Add(new PclCard { Header = release.Title, CardContent = row });
            review.Command = new ActionCommand(() => Run(async () =>
            {
                review.IsEnabled = false;
                try
                {
                    var instance = _launcher.ExtensionSelectedInstanceId ?? throw new ExtensionOperationException("instance.required", T("select_instance"));
                    var plan = await operations.PrepareInstallAsync(new() { InstanceId = instance, WorldPath = _launcher.ExtensionSelectedWorldPath,
                        Resources = [resource with { ReleaseId = release.Id }] }, view.Lifetime);
                    view.Lifetime.ThrowIfCancellationRequested();
                    var preview = new StackPanel { Name = "ExtensionInstallPreview", Spacing = 10 };
                    foreach (var entry in plan.Entries)
                    {
                        preview.Children.Add(new TextBlock { Text = entry.Release.Title + " → " + Path.GetFileName(entry.Destination), TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
                        foreach (var replaced in entry.ReplacedPaths) preview.Children.Add(new TextBlock { Text = T("resource_replace") + " " + Path.GetFileName(replaced) });
                    }
                    var install = new PclButton { Name = "ApplyExtensionInstall", Text = T("resource_install") };
                    preview.Children.Add(install); row.Children.Add(preview);
                    install.Command = new ActionCommand(() => Run(async () =>
                    {
                        install.IsEnabled = false;
                        try
                        {
                            await new ScopedTasks(this, context).RunAsync(new() { Id = context.Manifest.Id + ".install", Title = title },
                                async (progress, token) => await operations.ApplyInstallAsync(plan.Id, progress, token), view.Lifetime);
                            if (!view.Lifetime.IsCancellationRequested) feedback.Text = T("resource_installed");
                        }
                        catch (OperationCanceledException) { if (!view.Lifetime.IsCancellationRequested) feedback.Text = T("resource_canceled"); }
                        catch (Exception ex) { if (!view.Lifetime.IsCancellationRequested) feedback.Text = ex.Message; }
                    }));
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { feedback.Text = ex.Message; review.IsEnabled = true; }
            }));
        }
        if (releases.Count == 0) feedback.Text = T("resource_empty");
    }

    private sealed class ResourceProjectView : ScrollViewer, IDisposable
    {
        private readonly CancellationTokenSource _lifetime;
        private readonly StackPanel _body = new() { Margin = new(24), Spacing = 15 };
        private bool _disposed;
        public CancellationToken Lifetime { get; }
        public global::Avalonia.Controls.Controls Children => _body.Children;
        public ResourceProjectView(CancellationToken lifetime)
        {
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            Lifetime = _lifetime.Token;
            Content = _body;
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); }
    }
}
