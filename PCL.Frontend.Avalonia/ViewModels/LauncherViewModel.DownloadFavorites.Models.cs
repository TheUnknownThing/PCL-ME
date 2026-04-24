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

internal sealed partial class LauncherViewModel
{

    private sealed record CommunityProjectInstallPlan(
        IReadOnlyCollection<string> InstallAliases,
        string ProjectId,
        string Title,
        string ReleaseTitle,
        string ReleaseSummary,
        string SourceUrl,
        string TargetPath,
        string InstanceIndieDirectory,
        string InstanceName,
        string TargetName,
        LauncherFrontendSubpageKey Route,
        string ProjectSource,
        string? ReleaseId,
        string? FileId,
        string? SuggestedFileName,
        string? Sha1,
        string? Sha512,
        string? ReplacedPath,
        bool IsCurrentInstanceTarget,
        bool IsDependency);

    private sealed record CommunityProjectInstallBuildResult(
        IReadOnlyList<CommunityProjectInstallPlan> Plans,
        IReadOnlyList<string> Skipped);

    private sealed record CommunityProjectInstallRootRequest(
        string ProjectId,
        string Title,
        LauncherFrontendSubpageKey Route,
        FrontendCommunityProjectState? ProjectState = null,
        FrontendCommunityProjectReleaseEntry? Release = null);

    private sealed record DownloadFavoriteInstallTargetSnapshot(
        InstanceSelectionSnapshot Instance,
        FrontendVersionSaveSelectionState? DatapackSaveSelection)
    {
        public string DisplayName => DatapackSaveSelection?.HasSelection == true
            ? $"{Instance.Name} • {DatapackSaveSelection.SaveName}"
            : Instance.Name;
    }

    private sealed record InstalledFavoriteResource(
        string Title,
        string Path,
        string Version,
        string Website,
        IReadOnlyCollection<string> InstallAliases);

}
