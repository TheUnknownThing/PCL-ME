using PCL.Frontend.Spike.Desktop.Controls;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public ActionCommand BackCommand => _backCommand;

    public ActionCommand TogglePromptOverlayCommand => _togglePromptOverlayCommand;

    public ActionCommand DismissPromptOverlayCommand => _dismissPromptOverlayCommand;

    public ActionCommand LaunchCommand => _launchCommand;

    public ActionCommand VersionSelectCommand => _versionSelectCommand;

    public ActionCommand VersionSetupCommand => _versionSetupCommand;

    public ActionCommand ToggleLaunchMigrationCommand => _toggleLaunchMigrationCommand;

    public ActionCommand ToggleLaunchNewsCommand => _toggleLaunchNewsCommand;

    public ActionCommand DismissLaunchCommunityHintCommand => _dismissLaunchCommunityHintCommand;

    public ActionCommand OpenFeedbackCommand => _openFeedbackCommand;

    public ActionCommand ExportLogCommand => _exportLogCommand;

    public ActionCommand ExportAllLogsCommand => _exportAllLogsCommand;

    public ActionCommand OpenLogDirectoryCommand => _openLogDirectoryCommand;

    public ActionCommand CleanLogsCommand => _cleanLogsCommand;

    public ActionCommand GetMirrorCdkCommand => _getMirrorCdkCommand;

    public ActionCommand DownloadUpdateCommand => _downloadUpdateCommand;

    public ActionCommand ShowUpdateDetailCommand => _showUpdateDetailCommand;

    public ActionCommand CheckUpdateAgainCommand => _checkUpdateAgainCommand;

    public ActionCommand OpenFullChangelogCommand => _openFullChangelogCommand;

    public ActionCommand ResetGameLinkSettingsCommand => _resetGameLinkSettingsCommand;

    public ActionCommand ResetGameManageSettingsCommand => _resetGameManageSettingsCommand;

    public ActionCommand ResetLauncherMiscSettingsCommand => _resetLauncherMiscSettingsCommand;

    public ActionCommand ExportSettingsCommand => _exportSettingsCommand;

    public ActionCommand ImportSettingsCommand => _importSettingsCommand;

    public ActionCommand ApplyProxySettingsCommand => _applyProxySettingsCommand;

    public ActionCommand AddJavaRuntimeCommand => _addJavaRuntimeCommand;

    public ActionCommand SelectAutoJavaCommand => _selectAutoJavaCommand;

    public ActionCommand ResetUiSettingsCommand => _resetUiSettingsCommand;

    public ActionCommand OpenSnapshotBuildCommand => _openSnapshotBuildCommand;

    public ActionCommand BackgroundOpenFolderCommand => _backgroundOpenFolderCommand;

    public ActionCommand BackgroundRefreshCommand => _backgroundRefreshCommand;

    public ActionCommand BackgroundClearCommand => _backgroundClearCommand;

    public ActionCommand MusicOpenFolderCommand => _musicOpenFolderCommand;

    public ActionCommand MusicRefreshCommand => _musicRefreshCommand;

    public ActionCommand MusicClearCommand => _musicClearCommand;

    public ActionCommand ChangeLogoImageCommand => _changeLogoImageCommand;

    public ActionCommand DeleteLogoImageCommand => _deleteLogoImageCommand;

    public ActionCommand RefreshHomepageCommand => _refreshHomepageCommand;

    public ActionCommand GenerateHomepageTutorialFileCommand => _generateHomepageTutorialFileCommand;

    public ActionCommand ViewHomepageTutorialCommand => _viewHomepageTutorialCommand;

    public ActionCommand OpenHomepageMarketCommand => _openHomepageMarketCommand;

    public ActionCommand ToggleLaunchAdvancedOptionsCommand => _toggleLaunchAdvancedOptionsCommand;

    public ActionCommand AcceptGameLinkTermsCommand => _acceptGameLinkTermsCommand;

    public ActionCommand TestLobbyNatCommand => _testLobbyNatCommand;

    public ActionCommand LoginNatayarkAccountCommand => _loginNatayarkAccountCommand;

    public ActionCommand JoinLobbyCommand => _joinLobbyCommand;

    public ActionCommand PasteLobbyIdCommand => _pasteLobbyIdCommand;

    public ActionCommand ClearLobbyIdCommand => _clearLobbyIdCommand;

    public ActionCommand CreateLobbyCommand => _createLobbyCommand;

    public ActionCommand RefreshLobbyWorldsCommand => _refreshLobbyWorldsCommand;

    public ActionCommand InputLobbyPortCommand => _inputLobbyPortCommand;

    public ActionCommand CopyLobbyVirtualIpCommand => _copyLobbyVirtualIpCommand;

    public ActionCommand CopyActiveLobbyIdCommand => _copyActiveLobbyIdCommand;

    public ActionCommand ExitLobbyCommand => _exitLobbyCommand;

    public ActionCommand OpenLobbyReportCommand => _openLobbyReportCommand;

    public ActionCommand OpenNatayarkPolicyCommand => _openNatayarkPolicyCommand;

    public ActionCommand OpenLobbyPrivacyPolicyCommand => _openLobbyPrivacyPolicyCommand;

    public ActionCommand DisableGameLinkFeatureCommand => _disableGameLinkFeatureCommand;

    public ActionCommand OpenGameLinkFaqCommand => _openGameLinkFaqCommand;

    public ActionCommand OpenEasyTierWebsiteCommand => _openEasyTierWebsiteCommand;

    public ActionCommand OpenPysioWebsiteCommand => _openPysioWebsiteCommand;

    public ActionCommand SelectDownloadFolderCommand => _selectDownloadFolderCommand;

    public ActionCommand StartCustomDownloadCommand => _startCustomDownloadCommand;

    public ActionCommand OpenCustomDownloadFolderCommand => _openCustomDownloadFolderCommand;

    public ActionCommand SaveOfficialSkinCommand => _saveOfficialSkinCommand;

    public ActionCommand PreviewAchievementCommand => _previewAchievementCommand;

    public ActionCommand SaveAchievementCommand => _saveAchievementCommand;

    public ActionCommand SelectHeadSkinCommand => _selectHeadSkinCommand;

    public ActionCommand SaveHeadCommand => _saveHeadCommand;

    public ActionCommand ManageDownloadFavoriteTargetCommand => _manageDownloadFavoriteTargetCommand;

    public ActionCommand ResetInstanceExportOptionsCommand => _resetInstanceExportOptionsCommand;

    public ActionCommand ImportInstanceExportConfigCommand => _importInstanceExportConfigCommand;

    public ActionCommand SaveInstanceExportConfigCommand => _saveInstanceExportConfigCommand;

    public ActionCommand OpenInstanceExportGuideCommand => _openInstanceExportGuideCommand;

    public ActionCommand StartInstanceExportCommand => _startInstanceExportCommand;

    public ActionCommand SetLittleSkinCommand => _setLittleSkinCommand;

    public ActionCommand LockInstanceLoginCommand => _lockInstanceLoginCommand;

    public ActionCommand CreateInstanceProfileCommand => _createInstanceProfileCommand;

    public ActionCommand OpenGlobalLaunchSettingsCommand => _openGlobalLaunchSettingsCommand;
}
