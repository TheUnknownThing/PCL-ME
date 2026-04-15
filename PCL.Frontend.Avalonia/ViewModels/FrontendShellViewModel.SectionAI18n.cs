using System;
using System.Collections.Generic;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string T(string key)
    {
        return _i18n.T(key);
    }

    private string T(string key, params (string Name, object? Value)[] args)
    {
        if (args.Length == 0)
        {
            return _i18n.T(key);
        }

        var values = new Dictionary<string, object?>(args.Length, StringComparer.Ordinal);
        foreach (var (name, value) in args)
        {
            values[name] = value;
        }

        return _i18n.T(key, values);
    }

    public string LaunchHomepageRefreshButtonText => T("launch.homepage.actions.refresh");

    public string LaunchProfileAddTooltip => T("launch.profile.actions.add_or_login");

    public string LaunchProfileSelectTooltip => T("launch.profile.actions.select");

    public string LaunchProfileChooserMicrosoftText => T("launch.profile.chooser.microsoft");

    public string LaunchProfileChooserOfflineText => T("launch.profile.chooser.offline");

    public string LaunchProfileChooserAuthlibText => T("launch.profile.chooser.authlib");

    public string LaunchProfileBackText => T("common.actions.back");

    public string LaunchProfileNoEntriesText => T("launch.profile.selection.empty");

    public string LaunchOfflineEditorTitle => T("launch.profile.offline.title");

    public string LaunchOfflineUserNameWatermark => T("launch.profile.offline.user_name_watermark");

    public string LaunchOfflineCustomUuidWatermark => T("launch.profile.offline.custom_uuid_watermark");

    public string LaunchOfflineCreateText => T("launch.profile.offline.actions.create");

    public string LaunchMicrosoftEditorTitle => T("launch.profile.microsoft.title");

    public string LaunchMicrosoftDeviceCodeLabel => T("launch.profile.microsoft.device_code");

    public string LaunchMicrosoftReopenBrowserText => T("launch.profile.microsoft.actions.reopen_browser");

    public string LaunchAuthlibEditorTitle => T("launch.profile.authlib.title");

    public string LaunchAuthlibServerWatermark => T("launch.profile.authlib.server_watermark");

    public string LaunchAuthlibUserNameWatermark => T("launch.profile.authlib.user_name_watermark");

    public string LaunchAuthlibPasswordWatermark => T("launch.profile.authlib.password_watermark");

    public string LaunchAuthlibLittleSkinText => T("launch.profile.authlib.actions.use_littleskin");

    public string LaunchAuthlibLoginText => T("launch.profile.authlib.actions.login");

    public string LaunchVersionSelectButtonText => T("launch.actions.version_select");

    public string LaunchVersionSetupButtonText => T("launch.actions.version_setup");

    public string LaunchDialogStepLabel => T("launch.dialog.labels.step");

    public string LaunchDialogMethodLabel => T("launch.dialog.labels.method");

    public string LaunchDialogProgressLabel => T("launch.dialog.labels.progress");

    public string LaunchDialogDownloadSpeedLabel => T("launch.dialog.labels.download_speed");

    public string LaunchDialogHintTitle => T("launch.dialog.labels.did_you_know");

    public string CommonSearchButtonText => T("common.actions.search");

    public string DownloadInstallClearSelectionText => T("download.install.actions.clear_selection");

    public string DownloadInstallStartText => T("download.install.actions.start");

    public string DownloadResourceCurrentInstanceTitleText => T("download.resource.current_instance.title");

    public string DownloadResourceSwitchInstanceText => T("download.resource.current_instance.actions.switch");

    public string DownloadResourceFilterSourceText => T("download.resource.filters.source");

    public string DownloadResourceFilterTagText => T("download.resource.filters.tag");

    public string DownloadResourceFilterSortText => T("download.resource.filters.sort");

    public string DownloadResourceFilterVersionText => T("download.resource.filters.version");

    public string DownloadResourceFilterLoaderText => T("download.resource.filters.loader");

    public string DownloadFavoriteSearchWatermark => T("download.favorites.search.watermark");

    public string DownloadFavoriteManageTargetsTooltip => T("download.favorites.actions.manage_targets");

    public string DownloadFavoriteEmptyHeader => T("download.favorites.empty.title");

    public string DownloadFavoriteEmptyBody => T("download.favorites.empty.body");

    public string DownloadFavoriteBatchInstallText => T("download.favorites.actions.batch_install");

    public string DownloadFavoriteShareSelectedText => T("download.favorites.actions.share_selected");

    public string DownloadFavoriteFavoriteToText => T("download.favorites.actions.favorite_to");

    public string DownloadFavoriteRemoveText => T("download.favorites.actions.remove");

    public string DownloadFavoriteSelectAllText => T("common.actions.select_all");

    public string DownloadFavoriteCancelSelectionText => T("download.favorites.actions.clear_selection");

    public string CommunityProjectVersionFilterLabel => T("resource_detail.filters.version");

    public string CommunityProjectLoaderFilterLabel => T("resource_detail.filters.loader");

    public string CommunityProjectSuggestedReleaseTitle => T("resource_detail.suggested_release.title");

    public string CommunityProjectNoReleasesTitle => T("resource_detail.releases.empty.title");

    public string CommunityProjectNoReleasesBody => T("resource_detail.releases.empty.body");

    private void RaiseSectionAI18nProperties()
    {
        RaisePropertyChanged(nameof(LaunchHomepageRefreshButtonText));
        RaisePropertyChanged(nameof(LaunchHomepageStatusTitle));
        RaisePropertyChanged(nameof(LaunchProfileAddTooltip));
        RaisePropertyChanged(nameof(LaunchProfileSelectTooltip));
        RaisePropertyChanged(nameof(LaunchProfileChooserMicrosoftText));
        RaisePropertyChanged(nameof(LaunchProfileChooserOfflineText));
        RaisePropertyChanged(nameof(LaunchProfileChooserAuthlibText));
        RaisePropertyChanged(nameof(LaunchProfileBackText));
        RaisePropertyChanged(nameof(LaunchProfileNoEntriesText));
        RaisePropertyChanged(nameof(LaunchOfflineEditorTitle));
        RaisePropertyChanged(nameof(LaunchOfflineUserNameWatermark));
        RaisePropertyChanged(nameof(LaunchOfflineCustomUuidWatermark));
        RaisePropertyChanged(nameof(LaunchOfflineCreateText));
        RaisePropertyChanged(nameof(LaunchMicrosoftEditorTitle));
        RaisePropertyChanged(nameof(LaunchMicrosoftDeviceCodeLabel));
        RaisePropertyChanged(nameof(LaunchMicrosoftReopenBrowserText));
        RaisePropertyChanged(nameof(LaunchAuthlibEditorTitle));
        RaisePropertyChanged(nameof(LaunchAuthlibServerWatermark));
        RaisePropertyChanged(nameof(LaunchAuthlibUserNameWatermark));
        RaisePropertyChanged(nameof(LaunchAuthlibPasswordWatermark));
        RaisePropertyChanged(nameof(LaunchAuthlibLittleSkinText));
        RaisePropertyChanged(nameof(LaunchAuthlibLoginText));
        RaisePropertyChanged(nameof(LaunchVersionSelectButtonText));
        RaisePropertyChanged(nameof(LaunchVersionSetupButtonText));
        RaisePropertyChanged(nameof(LaunchDialogStepLabel));
        RaisePropertyChanged(nameof(LaunchDialogMethodLabel));
        RaisePropertyChanged(nameof(LaunchDialogProgressLabel));
        RaisePropertyChanged(nameof(LaunchDialogDownloadSpeedLabel));
        RaisePropertyChanged(nameof(LaunchDialogHintTitle));
        RaisePropertyChanged(nameof(CommonSearchButtonText));
        RaisePropertyChanged(nameof(DownloadInstallClearSelectionText));
        RaisePropertyChanged(nameof(DownloadInstallStartText));
        RaisePropertyChanged(nameof(DownloadResourceCurrentInstanceTitleText));
        RaisePropertyChanged(nameof(DownloadResourceSwitchInstanceText));
        RaisePropertyChanged(nameof(DownloadResourceFilterSourceText));
        RaisePropertyChanged(nameof(DownloadResourceFilterTagText));
        RaisePropertyChanged(nameof(DownloadResourceFilterSortText));
        RaisePropertyChanged(nameof(DownloadResourceFilterVersionText));
        RaisePropertyChanged(nameof(DownloadResourceFilterLoaderText));
        RaisePropertyChanged(nameof(DownloadFavoriteSearchWatermark));
        RaisePropertyChanged(nameof(DownloadFavoriteManageTargetsTooltip));
        RaisePropertyChanged(nameof(DownloadFavoriteEmptyHeader));
        RaisePropertyChanged(nameof(DownloadFavoriteEmptyBody));
        RaisePropertyChanged(nameof(DownloadFavoriteBatchInstallText));
        RaisePropertyChanged(nameof(DownloadFavoriteShareSelectedText));
        RaisePropertyChanged(nameof(DownloadFavoriteFavoriteToText));
        RaisePropertyChanged(nameof(DownloadFavoriteRemoveText));
        RaisePropertyChanged(nameof(DownloadFavoriteSelectAllText));
        RaisePropertyChanged(nameof(DownloadFavoriteCancelSelectionText));
        RaisePropertyChanged(nameof(CommunityProjectVersionFilterLabel));
        RaisePropertyChanged(nameof(CommunityProjectLoaderFilterLabel));
        RaisePropertyChanged(nameof(CommunityProjectSuggestedReleaseTitle));
        RaisePropertyChanged(nameof(CommunityProjectInstallModeOptions));
        RaisePropertyChanged(nameof(CommunityProjectNoReleasesTitle));
        RaisePropertyChanged(nameof(CommunityProjectNoReleasesBody));
    }
}
