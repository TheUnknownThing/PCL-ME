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

internal sealed partial class FrontendShellViewModel
{

    private async Task LoadCommunityProjectStateAsync(
        string projectId,
        string preferredMinecraftVersion,
        int communitySourcePreference,
        int refreshVersion)
    {
        var state = await Task.Run(() => FrontendCommunityProjectService.GetProjectState(
            projectId,
            preferredMinecraftVersion,
            communitySourcePreference));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _communityProjectRefreshVersion
                || !string.Equals(projectId, _selectedCommunityProjectId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _communityProjectState = state;
            SetCommunityProjectLoading(false);
            ApplyCommunityProjectIcon();
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
        });
    }

    private void RebuildCommunityProjectSurfaceCollections()
    {
        ReplaceItems(CommunityProjectActionButtons, BuildCommunityProjectActionButtons());

        var versionGrouping = DetermineCommunityProjectVersionGrouping(_communityProjectState.Releases);
        var versionOptions = BuildCommunityProjectVersionOptions(versionGrouping);
        var preferredVersion = NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
        _selectedCommunityProjectVersionFilter = ResolveCommunityProjectVersionFilter(versionOptions, versionGrouping, preferredVersion);

        var loaderOptions = BuildCommunityProjectLoaderOptions();
        _selectedCommunityProjectLoaderFilter = ResolveCommunityProjectLoaderFilter(loaderOptions);

        ReplaceItems(
            CommunityProjectVersionFilterButtons,
            BuildCommunityProjectFilterButtons(
                versionOptions,
                _selectedCommunityProjectVersionFilter,
                T("common.filters.all"),
                SelectCommunityProjectVersionFilter));
        ReplaceItems(
            CommunityProjectLoaderFilterButtons,
            BuildCommunityProjectFilterButtons(
                loaderOptions,
                _selectedCommunityProjectLoaderFilter,
                T("common.filters.all"),
                SelectCommunityProjectLoaderFilter));
        ReplaceItems(CommunityProjectReleaseGroups, BuildCommunityProjectReleaseGroups(versionGrouping));
        RebuildCommunityProjectDependencySections(versionGrouping);
        ReplaceItems(
            CommunityProjectSections,
            _communityProjectState.Links.Count == 0
                ? []
                :
                [
                    new DownloadCatalogSectionViewModel(
                        T("resource_detail.links.title"),
                        _communityProjectState.Links
                            .Select(LocalizeCommunityProjectLinkEntry)
                            .Select(entry => new DownloadCatalogEntryViewModel(
                                entry.Title,
                                entry.Info,
                                entry.Meta,
                                entry.ActionText,
                                CreateProjectSectionCommand(entry)))
                            .ToArray())
                ]);
    }

    private void RaiseCommunityProjectProperties()
    {
        RaisePropertyChanged(nameof(CommunityProjectTitle));
        RaisePropertyChanged(nameof(CommunityProjectSummary));
        RaisePropertyChanged(nameof(CommunityProjectDescription));
        RaisePropertyChanged(nameof(CommunityProjectSource));
        RaisePropertyChanged(nameof(CommunityProjectIcon));
        RaisePropertyChanged(nameof(HasCommunityProjectIcon));
        RaisePropertyChanged(nameof(CommunityProjectWebsite));
        RaisePropertyChanged(nameof(CommunityProjectStatus));
        RaisePropertyChanged(nameof(CommunityProjectUpdatedLabel));
        RaisePropertyChanged(nameof(CommunityProjectCompatibilitySummary));
        RaisePropertyChanged(nameof(CommunityProjectCategorySummary));
        RaisePropertyChanged(nameof(CommunityProjectDownloadCountLabel));
        RaisePropertyChanged(nameof(CommunityProjectFollowCountLabel));
        RaisePropertyChanged(nameof(HasCommunityProjectDescription));
        RaisePropertyChanged(nameof(CommunityProjectIntroDescription));
        RaisePropertyChanged(nameof(CommunityProjectCategoryTags));
        RaisePropertyChanged(nameof(HasCommunityProjectCategoryTags));
        RaisePropertyChanged(nameof(CommunityProjectSourceBadgeText));
        RaisePropertyChanged(nameof(CommunityProjectCurrentInstanceName));
        RaisePropertyChanged(nameof(CommunityProjectCurrentInstanceSummary));
        RaisePropertyChanged(nameof(ShowCommunityProjectInstallSuggestionCard));
        RaisePropertyChanged(nameof(CommunityProjectInstallSuggestionTitle));
        RaisePropertyChanged(nameof(CommunityProjectInstallSuggestionSummary));
        RaisePropertyChanged(nameof(CommunityProjectInstallModeOptions));
        RaisePropertyChanged(nameof(SelectedCommunityProjectInstallModeOption));
        RaisePropertyChanged(nameof(HasCommunityProjectActionButtons));
        RaisePropertyChanged(nameof(ShowCommunityProjectFilterCard));
        RaisePropertyChanged(nameof(ShowCommunityProjectVersionFilters));
        RaisePropertyChanged(nameof(ShowCommunityProjectLoaderFilters));
        RaisePropertyChanged(nameof(HasCommunityProjectReleaseGroups));
        RaisePropertyChanged(nameof(HasNoCommunityProjectReleaseGroups));
        RaisePropertyChanged(nameof(HasCommunityProjectDependencySections));
        RaisePropertyChanged(nameof(CommunityProjectDependencyCardTitle));
        RaisePropertyChanged(nameof(ShowCommunityProjectDependencyCard));
        RaisePropertyChanged(nameof(HasCommunityProjectSections));
        RaisePropertyChanged(nameof(HasNoCommunityProjectSections));
        RaisePropertyChanged(nameof(ShowCommunityProjectWarning));
        RaisePropertyChanged(nameof(CommunityProjectWarningText));
        RaisePropertyChanged(nameof(ShowCommunityProjectLoadingCard));
        RaisePropertyChanged(nameof(ShowCommunityProjectContent));
        RaisePropertyChanged(nameof(CommunityProjectLoadingText));
    }

    private void SetCommunityProjectLoading(bool isLoading)
    {
        if (_isCommunityProjectLoading == isLoading)
        {
            return;
        }

        _isCommunityProjectLoading = isLoading;
        RaisePropertyChanged(nameof(ShowCommunityProjectLoadingCard));
        RaisePropertyChanged(nameof(ShowCommunityProjectContent));
    }

}
