using PCL.Frontend.Avalonia.Desktop.Controls;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public ActionCommand BackCommand => _backCommand;

    public ActionCommand HomeCommand => _homeCommand;

    public ActionCommand TogglePromptOverlayCommand => _togglePromptOverlayCommand;

    public ActionCommand DismissPromptOverlayCommand => _dismissPromptOverlayCommand;

    public ActionCommand OpenTaskManagerShortcutCommand => _openTaskManagerShortcutCommand;

    public ActionCommand LaunchCommand => _launchCommand;

    public ActionCommand CancelLaunchCommand => _cancelLaunchCommand;

    public ActionCommand VersionSelectCommand => _versionSelectCommand;

    public ActionCommand VersionSetupCommand => _versionSetupCommand;

    public ActionCommand ToggleLaunchMigrationCommand => _toggleLaunchMigrationCommand;

    public ActionCommand ToggleLaunchNewsCommand => _toggleLaunchNewsCommand;

    public ActionCommand DismissLaunchCommunityHintCommand => _dismissLaunchCommunityHintCommand;

    public ActionCommand SelectLaunchProfileCommand => _selectLaunchProfileCommand;

    public ActionCommand AddLaunchProfileCommand => _addLaunchProfileCommand;

    public ActionCommand CreateOfflineLaunchProfileCommand => _createOfflineLaunchProfileCommand;

    public ActionCommand LoginMicrosoftLaunchProfileCommand => _loginMicrosoftLaunchProfileCommand;

    public ActionCommand LoginAuthlibLaunchProfileCommand => _loginAuthlibLaunchProfileCommand;

    public ActionCommand RefreshLaunchProfileCommand => _refreshLaunchProfileCommand;

    public ActionCommand BackLaunchProfileCommand => _backLaunchProfileCommand;

    public ActionCommand SubmitOfflineLaunchProfileCommand => _submitOfflineLaunchProfileCommand;

    public ActionCommand SubmitMicrosoftLaunchProfileCommand => _submitMicrosoftLaunchProfileCommand;

    public ActionCommand OpenMicrosoftDeviceLinkCommand => _openMicrosoftDeviceLinkCommand;

    public ActionCommand SubmitAuthlibLaunchProfileCommand => _submitAuthlibLaunchProfileCommand;

    public ActionCommand UseLittleSkinLaunchProfileCommand => _useLittleSkinLaunchProfileCommand;

    public ActionCommand OpenFeedbackCommand => _openFeedbackCommand;

    public ActionCommand ExportLogCommand => _exportLogCommand;

    public ActionCommand ExportAllLogsCommand => _exportAllLogsCommand;

    public ActionCommand OpenLogDirectoryCommand => _openLogDirectoryCommand;

    public ActionCommand CleanLogsCommand => _cleanLogsCommand;

    public ActionCommand DownloadUpdateCommand => _downloadUpdateCommand;

    public ActionCommand ShowUpdateDetailCommand => _showUpdateDetailCommand;

    public ActionCommand CheckUpdateAgainCommand => _checkUpdateAgainCommand;

    public ActionCommand OpenFullChangelogCommand => _openFullChangelogCommand;

    public ActionCommand ResetGameManageSettingsCommand => _resetGameManageSettingsCommand;

    public ActionCommand ResetLauncherMiscSettingsCommand => _resetLauncherMiscSettingsCommand;

    public ActionCommand ExportSettingsCommand => _exportSettingsCommand;

    public ActionCommand ImportSettingsCommand => _importSettingsCommand;

    public ActionCommand ApplyProxySettingsCommand => _applyProxySettingsCommand;

    public ActionCommand TestProxyConnectionCommand => _testProxyConnectionCommand;

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

    public ActionCommand ToggleLaunchAdvancedOptionsCommand => _toggleLaunchAdvancedOptionsCommand;

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
