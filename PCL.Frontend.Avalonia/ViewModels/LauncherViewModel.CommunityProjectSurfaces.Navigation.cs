using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{

    public void OpenCommunityProjectDetail(
        string projectId,
        string? projectTitle = null,
        string? initialVersionFilter = null,
        string? initialLoaderFilter = null,
        LauncherFrontendSubpageKey? originSubpage = null)
    {
        var normalizedProjectId = projectId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProjectId))
        {
            return;
        }

        if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail
            && ShouldPushCommunityProjectNavigationState(normalizedProjectId))
        {
            _communityProjectNavigationStack.Add(CreateCommunityProjectNavigationState());
        }
        else if (_currentRoute.Page != LauncherFrontendPageKey.CompDetail)
        {
            _communityProjectNavigationStack.Clear();
        }

        var targetOriginSubpage = originSubpage ?? _selectedCommunityProjectOriginSubpage;
        var shouldSyncFiltersWithInstance = ShouldAutoSyncCommunityProjectFiltersWithInstance(targetOriginSubpage);
        var versionFilter = shouldSyncFiltersWithInstance
            ? NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion)
                ?? NormalizeMinecraftVersion(initialVersionFilter)
                ?? initialVersionFilter?.Trim()
                ?? string.Empty
            : NormalizeMinecraftVersion(initialVersionFilter) ?? initialVersionFilter?.Trim() ?? string.Empty;
        var syncedLoaderFilter = SupportsCommunityProjectLoaderFiltering(targetOriginSubpage)
            ? ResolvePreferredInstanceLoaderLabel(_instanceComposition, targetOriginSubpage)
            : null;
        var loaderFilter = shouldSyncFiltersWithInstance
            ? syncedLoaderFilter ?? initialLoaderFilter?.Trim() ?? string.Empty
            : initialLoaderFilter?.Trim() ?? string.Empty;
        ApplyCommunityProjectNavigationState(new CommunityProjectNavigationState(
            normalizedProjectId,
            projectTitle?.Trim() ?? string.Empty,
            targetOriginSubpage,
            versionFilter,
            loaderFilter));
        var activityMessage = string.IsNullOrWhiteSpace(projectTitle)
            ? T("resource_detail.activities.open_detail")
            : T("resource_detail.activities.open_detail_for_project", ("project_title", projectTitle));
        if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail)
        {
            RefreshLauncherState(activityMessage);
            return;
        }

        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.CompDetail), activityMessage);
    }

    private bool TryNavigateBackWithinCommunityProjectDetail()
    {
        if (_currentRoute.Page != LauncherFrontendPageKey.CompDetail || _communityProjectNavigationStack.Count == 0)
        {
            return false;
        }

        var state = _communityProjectNavigationStack[^1];
        _communityProjectNavigationStack.RemoveAt(_communityProjectNavigationStack.Count - 1);
        ApplyCommunityProjectNavigationState(state);
        RefreshLauncherState(T("resource_detail.activities.navigate_back", ("target_title", state.TitleHintOrProjectId)));
        return true;
    }

    private void ResetCommunityProjectNavigationStack()
    {
        _communityProjectNavigationStack.Clear();
    }

    private bool HasCommunityProjectNavigationHistory => _communityProjectNavigationStack.Count > 0;

    private bool ShouldPushCommunityProjectNavigationState(string nextProjectId)
    {
        return !string.IsNullOrWhiteSpace(_selectedCommunityProjectId)
               && !string.Equals(_selectedCommunityProjectId, nextProjectId, StringComparison.OrdinalIgnoreCase);
    }

    private CommunityProjectNavigationState CreateCommunityProjectNavigationState()
    {
        return new CommunityProjectNavigationState(
            _selectedCommunityProjectId,
            _selectedCommunityProjectTitleHint,
            _selectedCommunityProjectOriginSubpage,
            _selectedCommunityProjectVersionFilter,
            _selectedCommunityProjectLoaderFilter);
    }

    private void ApplyCommunityProjectNavigationState(CommunityProjectNavigationState state)
    {
        _selectedCommunityProjectId = state.ProjectId;
        _selectedCommunityProjectTitleHint = state.TitleHint;
        _selectedCommunityProjectOriginSubpage = state.OriginSubpage;
        _selectedCommunityProjectVersionFilter = state.VersionFilter;
        _selectedCommunityProjectLoaderFilter = state.LoaderFilter;
    }

    private void RefreshCompDetailSurface()
    {
        CommunityProjectLoadingText = T("resource_detail.loading.releases");
        if (string.IsNullOrWhiteSpace(_selectedCommunityProjectId))
        {
            _communityProjectState = new FrontendCommunityProjectState(
                string.Empty,
                T("resource_detail.selection.none_selected_title"),
                T("resource_detail.selection.none_selected_summary"),
                string.Empty,
                T("resource_detail.selection.unspecified_source"),
                null,
                null,
                string.Empty,
                T("resource_detail.selection.waiting"),
                T("resource_detail.selection.not_loaded"),
                0,
                0,
                T("resource_detail.selection.no_compatibility"),
                T("resource_detail.selection.no_tags"),
                [],
                [],
                T("resource_detail.selection.missing_project_id"),
                true);
            SetCommunityProjectLoading(false);
            ApplyCommunityProjectIcon();
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            return;
        }

        var projectId = _selectedCommunityProjectId;
        var title = string.IsNullOrWhiteSpace(_selectedCommunityProjectTitleHint)
            ? T("resource_detail.selection.project_fallback_title", ("project_id", projectId))
            : _selectedCommunityProjectTitleHint;
        _communityProjectState = new FrontendCommunityProjectState(
            projectId,
            title,
            title,
            string.Empty,
            T("resource_detail.loading.source"),
            null,
            null,
            string.Empty,
            T("resource_detail.loading.status"),
            T("resource_detail.selection.not_loaded"),
            0,
            0,
            T("resource_detail.selection.no_compatibility"),
            T("resource_detail.selection.no_tags"),
            [],
            [],
            string.Empty,
            false);
        ReplaceItems(CommunityProjectActionButtons, []);
        ReplaceItems(CommunityProjectVersionFilterButtons, []);
        ReplaceItems(CommunityProjectLoaderFilterButtons, []);
        ReplaceItems(CommunityProjectReleaseGroups, []);
        ReplaceItems(CommunityProjectDependencySections, []);
        ReplaceItems(CommunityProjectSections, []);
        SetCommunityProjectLoading(true);
        ApplyCommunityProjectIcon();
        RaiseCommunityProjectProperties();

        var refreshVersion = ++_communityProjectRefreshVersion;
        var preferredMinecraftVersion = _instanceComposition.Selection.VanillaVersion;
        var communitySourcePreference = _selectedCommunityDownloadSourceIndex;
        _ = LoadCommunityProjectStateAsync(projectId, preferredMinecraftVersion, communitySourcePreference, refreshVersion);
    }

}
