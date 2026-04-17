using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{

    private async Task<bool> ConfirmDownloadFavoriteBatchInstallAsync(
        DownloadFavoriteInstallTargetSnapshot targetSnapshot,
        CommunityProjectInstallBuildResult result)
    {
        try
        {
            return await _shellActionService.ConfirmAsync(
                T("download.favorites.batch_install.confirmation.title"),
                BuildDownloadFavoriteBatchInstallConfirmationMessage(targetSnapshot, result),
                T("download.favorites.batch_install.confirmation.confirm"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.batch_install.confirmation.failed"), ex.Message);
            return false;
        }
    }

    private CommunityProjectInstallBuildResult BuildDownloadFavoriteBatchInstallResult(
        IReadOnlyList<FrontendDownloadCatalogEntry> selectedEntries,
        DownloadFavoriteInstallTargetSnapshot targetSnapshot)
    {
        var targetComposition = FrontendInstanceCompositionService.Compose(_shellActionService.RuntimePaths, targetSnapshot.Instance.Name);
        if (!targetComposition.Selection.HasSelection)
        {
            return new CommunityProjectInstallBuildResult([], [T("download.favorites.batch_install.target_unavailable", ("target_name", targetSnapshot.Instance.Name))]);
        }

        var roots = new List<CommunityProjectInstallRootRequest>();
        var skipped = new List<string>();
        foreach (var favorite in selectedEntries)
        {
            if (string.IsNullOrWhiteSpace(favorite.Identity))
            {
                skipped.Add(T("download.favorites.batch_install.skip.missing_project_id", ("title", favorite.Title)));
                continue;
            }

            if (favorite.OriginSubpage is null)
            {
                skipped.Add(T("download.favorites.batch_install.skip.unknown_type", ("title", favorite.Title)));
                continue;
            }

            if (favorite.OriginSubpage == LauncherFrontendSubpageKey.DownloadPack)
            {
                skipped.Add(T("download.favorites.batch_install.skip.pack_unsupported", ("title", favorite.Title)));
                continue;
            }

            roots.Add(new CommunityProjectInstallRootRequest(
                favorite.Identity,
                favorite.Title,
                favorite.OriginSubpage.Value));
        }

        var buildResult = BuildCommunityProjectInstallBuildResult(
            roots,
            targetComposition,
            targetSnapshot.Instance.LoaderLabel,
            includeDependencies: true,
            datapackSaveSelection: targetSnapshot.DatapackSaveSelection);
        return new CommunityProjectInstallBuildResult(
            buildResult.Plans,
            skipped.Concat(buildResult.Skipped).ToArray());
    }

    private void RegisterDownloadFavoriteBatchInstallTask(CommunityProjectInstallPlan plan)
    {
        RegisterCommunityProjectInstallTask(plan, T("download.favorites.batch_install.task_title"));
    }

    private void RegisterCommunityProjectInstallTask(CommunityProjectInstallPlan plan, string activityTitle)
    {
        TaskCenter.Register(new FrontendManagedFileDownloadTask(
            T("download.favorites.batch_install.task_display", ("activity_title", activityTitle), ("title", plan.Title)),
            plan.SourceUrl,
            plan.TargetPath,
            ResolveDownloadRequestTimeout(),
            onStarted: _ => AvaloniaHintBus.Show(T("download.favorites.batch_install.task_started", ("instance_name", plan.InstanceName)), AvaloniaHintTheme.Info),
            onCompleted: downloadedPath =>
            {
                var installedPath = FinalizeCommunityProjectInstalledArtifact(plan.Route, downloadedPath, plan.ReplacedPath);
                Dispatcher.UIThread.Post(() =>
                {
                    CleanupReplacedDownloadFavoriteResource(plan.ReplacedPath);
                    if (plan.IsCurrentInstanceTarget)
                    {
                        ReloadInstanceComposition(reloadDependentCompositions: false, initializeAllSurfaces: false);
                        if (plan.Route == LauncherFrontendSubpageKey.DownloadDataPack)
                        {
                            ReloadVersionSavesComposition();
                        }
                    }

                    AddActivity(activityTitle, T("download.favorites.batch_install.task_completed", ("title", plan.Title), ("path", installedPath)));
                    AvaloniaHintBus.Show(T("download.favorites.batch_install.task_completed_hint", ("title", plan.Title), ("instance_name", plan.InstanceName)), AvaloniaHintTheme.Success);
                });
            },
            onFailed: message =>
            {
                Dispatcher.UIThread.Post(() => AddFailureActivity(T("download.favorites.batch_install.task_failed", ("activity_title", activityTitle)), T("download.favorites.batch_install.task_failed_body", ("title", plan.Title), ("message", message))));
            }));
    }

    private CommunityProjectInstallBuildResult BuildCommunityProjectInstallBuildResult(
        IReadOnlyList<CommunityProjectInstallRootRequest> roots,
        FrontendInstanceComposition targetComposition,
        string? preferredLoader,
        bool includeDependencies,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        if (!targetComposition.Selection.HasSelection)
        {
            return new CommunityProjectInstallBuildResult([], [T("download.favorites.batch_install.target_unavailable")]);
        }

        var plans = new List<CommunityProjectInstallPlan>();
        var skipped = new List<string>();
        var resolvedResults = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var resolvingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preferredVersion = targetComposition.Selection.VanillaVersion;

        foreach (var root in roots)
        {
            ResolveInstallRoot(root, isDependency: false);
        }

        return new CommunityProjectInstallBuildResult(plans, skipped);

        bool ResolveInstallRoot(CommunityProjectInstallRootRequest request, bool isDependency)
        {
            if (string.IsNullOrWhiteSpace(request.ProjectId))
            {
                skipped.Add(T("download.favorites.batch_install.skip.missing_project_id", ("title", request.Title)));
                return false;
            }

            var requestKey = $"{request.Route}:{request.ProjectId}";
            if (resolvedResults.TryGetValue(requestKey, out var resolved))
            {
                return resolved;
            }

            if (!resolvingKeys.Add(requestKey))
            {
                return true;
            }

            try
            {
                var projectState = request.ProjectState ?? FrontendCommunityProjectService.GetProjectState(
                    request.ProjectId,
                    preferredVersion,
                    _selectedCommunityDownloadSourceIndex);
                var projectTitle = string.IsNullOrWhiteSpace(projectState.Title) ? request.Title : projectState.Title;
                var projectAliases = BuildCommunityProjectInstallAliases(
                    request.Route,
                    projectTitle,
                    projectState.Website,
                    filePath: request.Release?.SuggestedFileName);
                if (projectAliases.Any(resolvedAliases.Contains))
                {
                    resolvedResults[requestKey] = true;
                    return true;
                }

                var release = request.Release ?? SelectPreferredCommunityProjectReleaseForTarget(
                    projectState.Releases.Where(entry => entry.IsDirectDownload && !string.IsNullOrWhiteSpace(entry.Target)),
                    preferredVersion,
                    preferredLoader,
                    request.Route);
                var installTargetName = ResolveCommunityProjectInstallTargetName(
                    targetComposition.Selection.InstanceName,
                    request.Route,
                    datapackSaveSelection);
                if (release is null || string.IsNullOrWhiteSpace(release.Target))
                {
                    skipped.Add(T("download.favorites.batch_install.skip.no_version", ("title", projectTitle), ("instance_name", targetComposition.Selection.InstanceName)));
                    resolvedResults[requestKey] = false;
                    return false;
                }

                if (includeDependencies && request.Route == LauncherFrontendSubpageKey.DownloadMod)
                {
                    foreach (var dependency in release.Dependencies.Where(ShouldAutoInstallCommunityProjectDependency))
                    {
                        var dependencyResolved = ResolveInstallRoot(
                            new CommunityProjectInstallRootRequest(
                                dependency.ProjectId,
                                dependency.Title,
                                request.Route),
                            isDependency: true);
                        if (!dependencyResolved && dependency.Kind == FrontendCommunityProjectDependencyKind.Required)
                        {
                            skipped.Add(T("download.favorites.batch_install.skip.missing_dependency", ("title", projectTitle), ("dependency_title", dependency.Title)));
                            resolvedResults[requestKey] = false;
                            return false;
                        }
                    }
                }

                var targetDirectory = ResolveCommunityProjectInstallDirectory(targetComposition.Selection, request.Route, datapackSaveSelection);
                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    skipped.Add(T("download.favorites.batch_install.skip.no_install_dir", ("title", projectTitle), ("instance_name", targetComposition.Selection.InstanceName)));
                    resolvedResults[requestKey] = false;
                    return false;
                }

                Directory.CreateDirectory(targetDirectory);
                var targetFileName = FrontendGameManagementService.ResolveCommunityResourceFileName(
                    projectTitle,
                    release.SuggestedFileName,
                    release.Title,
                    SelectedFileNameFormatIndex);
                targetFileName = NormalizeCommunityProjectInstallArtifactFileName(request.Route, targetFileName);
                var installed = FindInstalledCommunityProjectResource(targetComposition, request.Route, projectTitle, projectState, datapackSaveSelection);
                if (request.Route != LauncherFrontendSubpageKey.DownloadWorld
                    && installed is not null
                    && !ShouldInstallFavoriteResourceUpdate(installed, targetFileName, release))
                {
                    skipped.Add(T("download.favorites.batch_install.skip.already_installed", ("title", projectTitle), ("instance_name", targetComposition.Selection.InstanceName)));
                    resolvedAliases.UnionWith(projectAliases);

                    resolvedResults[requestKey] = true;
                    return true;
                }

                var targetPath = ResolveCommunityProjectInstallTargetPath(
                    targetDirectory,
                    targetFileName,
                    request.Route,
                    installed,
                    plans);
                plans.Add(new CommunityProjectInstallPlan(
                    projectAliases,
                    string.IsNullOrWhiteSpace(projectState.ProjectId) ? request.ProjectId : projectState.ProjectId,
                    projectTitle,
                    release.Title,
                    string.IsNullOrWhiteSpace(release.Meta) ? release.Info : release.Meta,
                    release.Target!,
                    targetPath,
                    targetComposition.Selection.InstanceName,
                    installTargetName,
                    request.Route,
                    installed is not null && !string.Equals(installed.Path, targetPath, StringComparison.OrdinalIgnoreCase)
                        ? installed.Path
                        : null,
                    string.Equals(targetComposition.Selection.InstanceName, _instanceComposition.Selection.InstanceName, StringComparison.OrdinalIgnoreCase),
                    isDependency));
                resolvedAliases.UnionWith(projectAliases);
                resolvedResults[requestKey] = true;
                return true;
            }
            catch (Exception ex)
            {
                skipped.Add(T("download.favorites.batch_install.skip.exception", ("title", request.Title), ("message", ex.Message)));
                resolvedResults[requestKey] = false;
                return false;
            }
            finally
            {
                resolvingKeys.Remove(requestKey);
            }
        }
    }

    private static void CleanupReplacedDownloadFavoriteResource(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Keep the newly installed file even if cleanup of the old version fails.
        }
    }

    private static FrontendCommunityProjectReleaseEntry? SelectPreferredCommunityProjectReleaseForTarget(
        IEnumerable<FrontendCommunityProjectReleaseEntry> releases,
        string? preferredVersion,
        string? preferredLoader,
        LauncherFrontendSubpageKey? originSubpage)
    {
        return releases
            .Where(release => IsCompatibleCommunityProjectInstallRelease(
                release,
                preferredVersion,
                preferredLoader,
                originSubpage))
            .OrderByDescending(release => ReleaseMatchesExactInstanceVersion(release, NormalizeMinecraftVersion(preferredVersion)))
            .ThenByDescending(release => ReleaseMatchesExactInstanceLoader(release, preferredLoader))
            .ThenByDescending(release => release.PublishedUnixTime)
            .ThenBy(release => release.Title, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    private static string? ResolveCommunityProjectInstallDirectory(
        FrontendInstanceSelectionState selection,
        LauncherFrontendSubpageKey route,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        if (!selection.HasSelection)
        {
            return null;
        }

        return route switch
        {
            LauncherFrontendSubpageKey.DownloadResourcePack => Path.Combine(selection.IndieDirectory, "resourcepacks"),
            LauncherFrontendSubpageKey.DownloadShader => Path.Combine(selection.IndieDirectory, "shaderpacks"),
            LauncherFrontendSubpageKey.DownloadWorld => Path.Combine(selection.IndieDirectory, "saves"),
            LauncherFrontendSubpageKey.DownloadDataPack => datapackSaveSelection?.HasSelection == true ? datapackSaveSelection.DatapackDirectory : null,
            _ => Path.Combine(selection.IndieDirectory, "mods")
        };
    }

    private static InstalledFavoriteResource? FindInstalledFavoriteResource(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey route,
        FrontendDownloadCatalogEntry favorite,
        FrontendCommunityProjectState projectState,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        return FindInstalledCommunityProjectResource(composition, route, favorite.Title, projectState, datapackSaveSelection);
    }

    private static InstalledFavoriteResource? FindInstalledCommunityProjectResource(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey route,
        string title,
        FrontendCommunityProjectState projectState,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        if (route == LauncherFrontendSubpageKey.DownloadWorld)
        {
            return null;
        }

        var installedResources = GetInstalledFavoriteResources(composition, route, datapackSaveSelection);
        var projectAliases = BuildCommunityProjectInstallAliases(route, title, projectState.Website);
        if (!string.IsNullOrWhiteSpace(projectState.Website))
        {
            var websiteMatch = installedResources.FirstOrDefault(entry =>
                string.Equals(entry.Website, projectState.Website, StringComparison.OrdinalIgnoreCase));
            if (websiteMatch is not null)
            {
                return websiteMatch;
            }
        }

        return installedResources.FirstOrDefault(entry =>
            string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase)
            || entry.InstallAliases.Any(projectAliases.Contains));
    }

    private static IReadOnlyList<InstalledFavoriteResource> GetInstalledFavoriteResources(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey route,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => composition.Mods.Entries
                .Concat(composition.DisabledMods.Entries)
                .Select(entry => new InstalledFavoriteResource(
                    entry.Title,
                    entry.Path,
                    entry.Version,
                    entry.Website,
                    BuildCommunityProjectInstallAliases(route, entry.Title, entry.Website, entry.Identity, entry.Path)))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadResourcePack => composition.ResourcePacks.Entries
                .Select(entry => new InstalledFavoriteResource(
                    entry.Title,
                    entry.Path,
                    entry.Version,
                    entry.Website,
                    BuildCommunityProjectInstallAliases(route, entry.Title, entry.Website, entry.Identity, entry.Path)))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadShader => composition.Shaders.Entries
                .Select(entry => new InstalledFavoriteResource(
                    entry.Title,
                    entry.Path,
                    entry.Version,
                    entry.Website,
                    BuildCommunityProjectInstallAliases(route, entry.Title, entry.Website, entry.Identity, entry.Path)))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadDataPack => datapackSaveSelection?.HasSelection == true
                ? EnumerateDirectoryInstallArtifacts(datapackSaveSelection.DatapackDirectory)
                : [],
            _ => []
        };
    }

    private static string ResolveCommunityProjectInstallTargetName(
        string instanceName,
        LauncherFrontendSubpageKey route,
        FrontendVersionSaveSelectionState? datapackSaveSelection)
    {
        if (route == LauncherFrontendSubpageKey.DownloadDataPack && datapackSaveSelection?.HasSelection == true)
        {
            return $"{instanceName} • {datapackSaveSelection.SaveName}";
        }

        return instanceName;
    }

    private static InstalledFavoriteResource[] EnumerateDirectoryInstallArtifacts(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new InstalledFavoriteResource(
                Path.GetFileNameWithoutExtension(path),
                path,
                string.Empty,
                string.Empty,
                BuildCommunityProjectInstallAliases(route: LauncherFrontendSubpageKey.DownloadDataPack, title: Path.GetFileNameWithoutExtension(path), website: null, filePath: path)));
        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new InstalledFavoriteResource(
                Path.GetFileName(path),
                path,
                string.Empty,
                string.Empty,
                BuildCommunityProjectInstallAliases(route: LauncherFrontendSubpageKey.DownloadDataPack, title: Path.GetFileName(path), website: null, filePath: path)));
        return files.Concat(folders).ToArray();
    }

    private static bool ShouldInstallFavoriteResourceUpdate(
        InstalledFavoriteResource existing,
        string targetFileName,
        FrontendCommunityProjectReleaseEntry release)
    {
        var existingFileName = Path.GetFileName(existing.Path);
        if (string.Equals(existingFileName, targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var existingVersion = GetComparableFavoriteVersion(existing.Version, existingFileName, existing.Title);
        var releaseVersion = GetComparableFavoriteVersion(
            release.Title,
            release.SuggestedFileName,
            release.Info,
            release.Meta);
        if (existingVersion > new Version(0, 0) && releaseVersion > new Version(0, 0))
        {
            return releaseVersion > existingVersion;
        }

        return true;
    }

    private static Version GetComparableFavoriteVersion(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var match = System.Text.RegularExpressions.Regex.Match(candidate, @"\d+(?:\.\d+){1,3}");
            if (match.Success && Version.TryParse(match.Value, out var parsed))
            {
                return parsed;
            }
        }

        return new Version(0, 0);
    }

    private string BuildDownloadFavoriteBatchInstallConfirmationMessage(
        DownloadFavoriteInstallTargetSnapshot targetSnapshot,
        CommunityProjectInstallBuildResult result)
    {
        return BuildCommunityProjectInstallConfirmationMessage(targetSnapshot.DisplayName, result);
    }

    private string BuildCommunityProjectInstallConfirmationMessage(
        string instanceName,
        CommunityProjectInstallBuildResult result)
    {
        var lines = new List<string>
        {
            T("download.favorites.batch_install.confirmation.instance", ("instance_name", instanceName)),
            T("download.favorites.batch_install.confirmation.count", ("count", result.Plans.Count))
        };

        if (result.Plans.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(T("download.favorites.batch_install.confirmation.plans"));
            foreach (var plan in result.Plans)
            {
                var releaseText = string.IsNullOrWhiteSpace(plan.ReleaseSummary)
                    ? plan.ReleaseTitle
                    : $"{plan.ReleaseTitle} | {plan.ReleaseSummary}";
                lines.Add(plan.IsDependency
                    ? T("download.favorites.batch_install.confirmation.plan_dependency", ("title", plan.Title), ("release_text", releaseText))
                    : T("download.favorites.batch_install.confirmation.plan", ("title", plan.Title), ("release_text", releaseText)));
            }
        }

        if (result.Skipped.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(T("download.favorites.batch_install.confirmation.skipped"));
            foreach (var skipped in result.Skipped)
            {
                lines.Add(T("download.favorites.batch_install.confirmation.skipped_item", ("value", skipped)));
            }
        }

        lines.Add(string.Empty);
        lines.Add(T("download.favorites.batch_install.confirmation.footer"));
        return string.Join(Environment.NewLine, lines);
    }

    private static bool ShouldAutoInstallCommunityProjectDependency(FrontendCommunityProjectDependencyEntry dependency)
    {
        return dependency.Kind is FrontendCommunityProjectDependencyKind.Required
            or FrontendCommunityProjectDependencyKind.Tool
            or FrontendCommunityProjectDependencyKind.Include;
    }

    private static string ResolveCommunityProjectInstallTargetPath(
        string targetDirectory,
        string targetFileName,
        LauncherFrontendSubpageKey route,
        InstalledFavoriteResource? installed,
        IEnumerable<CommunityProjectInstallPlan> existingPlans)
    {
        if (route == LauncherFrontendSubpageKey.DownloadWorld)
        {
            return GetUniqueChildPath(targetDirectory, targetFileName);
        }

        var preferredPath = installed is not null
                            && string.Equals(Path.GetFileName(installed.Path), targetFileName, StringComparison.OrdinalIgnoreCase)
            ? installed.Path
            : Path.Combine(targetDirectory, targetFileName);
        var isOccupied = existingPlans.Any(plan => string.Equals(plan.TargetPath, preferredPath, StringComparison.OrdinalIgnoreCase))
                         || (installed is null && (File.Exists(preferredPath) || Directory.Exists(preferredPath)));
        return isOccupied ? GetUniqueChildPath(targetDirectory, targetFileName) : preferredPath;
    }

    private static HashSet<string> BuildCommunityProjectInstallAliases(
        LauncherFrontendSubpageKey route,
        string? title,
        string? website,
        string? identity = null,
        string? filePath = null)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(identity));
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(title));
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(ExtractCommunityProjectWebsiteSlug(website)));
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(ExtractCommunityProjectStemAlias(filePath)));
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(filePath is null ? null : Path.GetFileNameWithoutExtension(filePath)));
        return aliases;
    }

    private static string ExtractCommunityProjectWebsiteSlug(string? website)
    {
        if (string.IsNullOrWhiteSpace(website)
            || !Uri.TryCreate(website, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return uri.Segments
            .Select(segment => segment.Trim('/'))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .LastOrDefault(segment =>
                !segment.Equals("mod", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("plugin", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("project", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("mc-mods", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("texture-packs", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("resource-packs", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("shader-packs", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }

    private static string NormalizeCommunityProjectAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\p{L}\p{Nd}]+", " ");
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token is not (
                "mod" or "mods" or "api" or "library" or "lib" or "shader" or "shaders" or "resource" or "resources" or
                "resourcepack" or "resourcepacks" or "resource-pack" or "resource-packs" or
                "pack" or "packs" or "plugin" or "plugins" or
                "fabric" or "forge" or "neoforge" or "quilt"))
            .ToArray();
        return tokens.Length == 0 ? string.Empty : string.Concat(tokens);
    }

    private static string ExtractCommunityProjectStemAlias(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return string.Empty;
        }

        var tokens = System.Text.RegularExpressions.Regex
            .Split(stem, @"[^A-Za-z0-9]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        var kept = new List<string>();
        foreach (var token in tokens)
        {
            if (LooksLikeVersionToken(token))
            {
                break;
            }

            kept.Add(token);
        }

        return kept.Count == 0 ? stem : string.Join(' ', kept);
    }

    private static bool LooksLikeVersionToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(token, @"^\d+$"))
        {
            return true;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(token, @"^\d+(?:[A-Za-z]?\d+)*(?:v\d+)?$");
    }

    private static void AddCommunityProjectInstallAlias(
        ISet<string> aliases,
        LauncherFrontendSubpageKey route,
        string alias)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            aliases.Add($"{route}:{alias}");
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

}
