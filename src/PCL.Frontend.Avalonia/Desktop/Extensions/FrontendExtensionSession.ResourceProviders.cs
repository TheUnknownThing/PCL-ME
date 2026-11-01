using System.Collections.Concurrent;
using PCL.Core.App.Essentials;
using PCL.Extensions.Contracts;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private void ResourceProvidersChanged() => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    { if (!_disposed) _launcher.RefreshExtensionResourceProviders(); });

    internal IReadOnlyList<ExtensionResourceProviderDefinition> GetResourceProviderDefinitions() => ResourceProviders().Select(item => item.Definition).ToArray();

    private sealed class LauncherResourceProvider(FrontendExtensionSession session, ExtensionResourceProviderDefinition definition) : IExtensionResourceProvider
    {
        private readonly ConcurrentDictionary<string, ExtensionResourceKind> _kinds = new(StringComparer.Ordinal);
        public ValueTask<ExtensionResourcePage> SearchAsync(ExtensionResourceQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var composition = FrontendInstanceCompositionService.Compose(session._actions.RuntimePaths, FrontendInstanceCompositionService.LoadMode.Lightweight);
            var result = FrontendCommunityResourceCatalogService.QueryResources(ResourceRoute(query.Kind),
                new(query.Text, definition.Title, "", "", query.MinecraftVersion ?? "", query.Loader ?? ""), composition, session._launcher.SelectedCommunityDownloadSourceIndex, query.Offset + query.Limit);
            cancellationToken.ThrowIfCancellationRequested();
            var projects = result.State.Entries.Skip(query.Offset).Take(query.Limit).Select(entry =>
            {
                if (!FrontendCommunityProjectService.TryParseCompDetailTarget(entry.TargetPath, out var id)) return null;
                _kinds[id] = query.Kind;
                return new ExtensionResourceProject { ProviderId = definition.Id, ProjectId = id, Title = entry.Title, Description = entry.Info, Kind = query.Kind };
            }).OfType<ExtensionResourceProject>().ToArray();
            return ValueTask.FromResult(new ExtensionResourcePage { Projects = projects, HasMore = result.State.HasMoreEntries || result.State.Entries.Count > query.Offset + query.Limit });
        }
        public ValueTask<IReadOnlyList<ExtensionResourceRelease>> GetReleasesAsync(string projectId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = FrontendCommunityProjectService.GetProjectState(projectId, "", session._launcher.SelectedCommunityDownloadSourceIndex);
            if (state.Status == "load_failed") throw new ExtensionOperationException("provider.failed", state.WarningText);
            if (!_kinds.TryGetValue(projectId, out var kind))
            {
                var metadata = FrontendCommunityProjectService.LookupProjects([projectId], session._launcher.SelectedCommunityDownloadSourceIndex).Projects.GetValueOrDefault(projectId);
                kind = metadata?.ProjectType?.ToLowerInvariant() switch
                { "resourcepack" or "resource_pack" => ExtensionResourceKind.ResourcePack, "shader" => ExtensionResourceKind.Shader, "world" => ExtensionResourceKind.World, "datapack" or "data_pack" => ExtensionResourceKind.DataPack, _ => ExtensionResourceKind.Mod };
            }
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ExtensionResourceRelease> releases = state.Releases.Where(item => item.IsDirectDownload && item.Target is not null && item.SuggestedFileName is not null)
                .Select(item => new ExtensionResourceRelease
                {
                    Id = item.ReleaseId ?? item.FileId ?? item.Target!, Title = item.Title, FileName = item.SuggestedFileName!, DownloadUrl = item.Target!, Kind = kind,
                    MinecraftVersions = item.GameVersions.ToArray(), Loaders = item.Loaders.Select(value => value.ToLowerInvariant()).ToArray(), Sha1 = item.Sha1, Sha512 = item.Sha512,
                    Dependencies = item.Dependencies.Where(dependency => dependency.Kind == FrontendCommunityProjectDependencyKind.Required)
                        .Select(dependency => new ExtensionResourceReference { ProviderId = definition.Id, ProjectId = dependency.ProjectId, ReleaseId = dependency.ReleaseId }).ToArray()
                }).ToArray();
            return ValueTask.FromResult(releases);
        }
    }
    internal static LauncherFrontendSubpageKey ResourceRoute(ExtensionResourceKind kind) => kind switch
    {
        ExtensionResourceKind.Mod => LauncherFrontendSubpageKey.DownloadMod,
        ExtensionResourceKind.ResourcePack => LauncherFrontendSubpageKey.DownloadResourcePack,
        ExtensionResourceKind.Shader => LauncherFrontendSubpageKey.DownloadShader,
        ExtensionResourceKind.World => LauncherFrontendSubpageKey.DownloadWorld,
        ExtensionResourceKind.DataPack => LauncherFrontendSubpageKey.DownloadDataPack,
        _ => throw new ArgumentException("This resource kind uses an extension page.")
    };
    internal static ExtensionResourceKind? ResourceKind(LauncherFrontendSubpageKey route) => route switch
    {
        LauncherFrontendSubpageKey.DownloadMod => ExtensionResourceKind.Mod,
        LauncherFrontendSubpageKey.DownloadResourcePack => ExtensionResourceKind.ResourcePack,
        LauncherFrontendSubpageKey.DownloadShader => ExtensionResourceKind.Shader,
        LauncherFrontendSubpageKey.DownloadWorld => ExtensionResourceKind.World,
        LauncherFrontendSubpageKey.DownloadDataPack => ExtensionResourceKind.DataPack,
        _ => null
    };
}
