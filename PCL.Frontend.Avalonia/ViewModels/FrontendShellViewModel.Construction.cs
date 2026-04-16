using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.I18n;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string LauncherRootDirectory = FrontendLauncherAssetLocator.RootDirectory;
    private static readonly string LaunchAvatarImageFilePath = GetLauncherAssetPath("Images", "Heads", "PCL-Community.png");
    private static readonly string LaunchNewsImageFilePath = GetLauncherAssetPath("Images", "Backgrounds", "server_bg.png");
    private static readonly string UpdateAvailableIconFilePath = GetLauncherAssetPath("Images", "Heads", "Logo-CE.png");
    private static readonly string UpdateCurrentIconFilePath = GetLauncherAssetPath("Images", "icon.png");
    private readonly AvaloniaCommandOptions _options;
    private readonly II18nService _i18n;
    private readonly IReadOnlyList<string> _launcherLocaleKeys;
    private readonly IReadOnlyList<string> _launcherLocaleOptions;
    private readonly FrontendShellActionService _shellActionService;
    private FrontendShellComposition _shellComposition;
    private FrontendSetupComposition _setupComposition;
    private SetupLocalizationCatalog _setupText;
    private FrontendInstanceComposition _instanceComposition;
    private FrontendInstanceCompositionService.LoadMode _instanceCompositionLoadMode = FrontendInstanceCompositionService.LoadMode.Full;
    private FrontendToolsComposition _toolsComposition = new(
        new FrontendToolsHelpState([]),
        new FrontendToolsTestState([], string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, 0, string.Empty));
    private FrontendSetupUpdateStatus _updateStatus = null!;
    private FrontendSetupFeedbackSnapshot? _feedbackSnapshot;
    private StartupAvaloniaPlan _startupPlan;
    private FrontendLaunchComposition _launchComposition;
    private readonly CrashAvaloniaPlan _crashPlan;
    private CrashAvaloniaPlan _activeCrashPlan;
    private readonly Dictionary<AvaloniaPromptLaneKind, List<PromptCardViewModel>> _promptCatalog;
    private readonly List<LauncherFrontendRoute> _routeAncestors = [];
    private readonly ActionCommand _backCommand;
    private readonly ActionCommand _homeCommand;
    private readonly ActionCommand _togglePromptOverlayCommand;
    private readonly ActionCommand _dismissPromptOverlayCommand;
    private readonly ActionCommand _openTaskManagerShortcutCommand;
    private readonly ActionCommand _launchCommand;
    private readonly ActionCommand _cancelLaunchCommand;
    private readonly ActionCommand _versionSelectCommand;
    private readonly ActionCommand _versionSetupCommand;
    private readonly ActionCommand _toggleLaunchMigrationCommand;
    private readonly ActionCommand _toggleLaunchNewsCommand;
    private readonly ActionCommand _dismissLaunchCommunityHintCommand;
    private readonly ActionCommand _selectLaunchProfileCommand;
    private readonly ActionCommand _addLaunchProfileCommand;
    private readonly ActionCommand _createOfflineLaunchProfileCommand;
    private readonly ActionCommand _loginMicrosoftLaunchProfileCommand;
    private readonly ActionCommand _loginAuthlibLaunchProfileCommand;
    private readonly ActionCommand _refreshLaunchProfileCommand;
    private readonly ActionCommand _backLaunchProfileCommand;
    private readonly ActionCommand _submitOfflineLaunchProfileCommand;
    private readonly ActionCommand _submitMicrosoftLaunchProfileCommand;
    private readonly ActionCommand _openMicrosoftDeviceLinkCommand;
    private readonly ActionCommand _submitAuthlibLaunchProfileCommand;
    private readonly ActionCommand _useLittleSkinLaunchProfileCommand;
    private readonly ActionCommand _openFeedbackCommand;
    private readonly ActionCommand _exportLogCommand;
    private readonly ActionCommand _exportAllLogsCommand;
    private readonly ActionCommand _openLogDirectoryCommand;
    private readonly ActionCommand _cleanLogsCommand;
    private readonly ActionCommand _downloadUpdateCommand;
    private readonly ActionCommand _showUpdateDetailCommand;
    private readonly ActionCommand _checkUpdateAgainCommand;
    private readonly ActionCommand _openFullChangelogCommand;
    private readonly ActionCommand _resetGameManageSettingsCommand;
    private readonly ActionCommand _resetLauncherMiscSettingsCommand;
    private readonly ActionCommand _exportSettingsCommand;
    private readonly ActionCommand _importSettingsCommand;
    private readonly ActionCommand _applyProxySettingsCommand;
    private readonly ActionCommand _addJavaRuntimeCommand;
    private readonly ActionCommand _selectAutoJavaCommand;
    private readonly ActionCommand _resetUiSettingsCommand;
    private readonly ActionCommand _openSnapshotBuildCommand;
    private readonly ActionCommand _backgroundOpenFolderCommand;
    private readonly ActionCommand _backgroundRefreshCommand;
    private readonly ActionCommand _backgroundClearCommand;
    private readonly ActionCommand _musicOpenFolderCommand;
    private readonly ActionCommand _musicRefreshCommand;
    private readonly ActionCommand _musicClearCommand;
    private readonly ActionCommand _changeLogoImageCommand;
    private readonly ActionCommand _deleteLogoImageCommand;
    private readonly ActionCommand _refreshHomepageCommand;
    private readonly ActionCommand _generateHomepageTutorialFileCommand;
    private readonly ActionCommand _viewHomepageTutorialCommand;
    private readonly ActionCommand _toggleLaunchAdvancedOptionsCommand;
    private readonly ActionCommand _openPysioWebsiteCommand;
    private readonly ActionCommand _selectDownloadFolderCommand;
    private readonly ActionCommand _startCustomDownloadCommand;
    private readonly ActionCommand _openCustomDownloadFolderCommand;
    private readonly ActionCommand _saveOfficialSkinCommand;
    private readonly ActionCommand _previewAchievementCommand;
    private readonly ActionCommand _saveAchievementCommand;
    private readonly ActionCommand _queryMinecraftServerCommand;
    private readonly ActionCommand _selectHeadSkinCommand;
    private readonly ActionCommand _saveHeadCommand;
    private readonly ActionCommand _resetDownloadInstallSurfaceCommand;
    private readonly ActionCommand _manageDownloadFavoriteTargetCommand;
    private readonly ActionCommand _resetInstanceExportOptionsCommand;
    private readonly ActionCommand _importInstanceExportConfigCommand;
    private readonly ActionCommand _saveInstanceExportConfigCommand;
    private readonly ActionCommand _openInstanceExportGuideCommand;
    private readonly ActionCommand _startInstanceExportCommand;
    private readonly ActionCommand _setLittleSkinCommand;
    private readonly ActionCommand _lockInstanceLoginCommand;
    private readonly ActionCommand _createInstanceProfileCommand;
    private readonly ActionCommand _openGlobalLaunchSettingsCommand;
    private readonly ActionCommand _refreshInstanceSelectionCommand;
    private readonly ActionCommand _clearInstanceSelectionSearchCommand;
    private readonly ActionCommand _addInstanceSelectionFolderCommand;
    private readonly ActionCommand _importInstanceSelectionPackCommand;
    private readonly ActionCommand _openInstanceSelectionDownloadCommand;
    private readonly ActionCommand _refreshTaskManagerCommand;
    private readonly ActionCommand _clearFinishedTasksCommand;
    private readonly ActionCommand _refreshGameLogCommand;
    private readonly ActionCommand _clearGameLogCommand;
    private LauncherFrontendRoute _currentRoute;
    private LauncherFrontendNavigationView? _currentNavigation;
    private AvaloniaPromptLaneKind _selectedPromptLane;
    private string _eyebrow = string.Empty;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _status = string.Empty;
    private string _breadcrumbTrail = string.Empty;
    private string _surfaceMeta = string.Empty;
    private string _promptInboxTitle = string.Empty;
    private string _promptInboxSummary = string.Empty;
    private string _promptEmptyState = string.Empty;
    private double _standardSidebarAutoWidth = 152;
    private bool _canGoBack;
    private bool _canGoHome;
    private bool _isPromptOverlayOpen;
    private bool _isLaunchMigrationExpanded = true;
    private bool _isLaunchNewsExpanded = true;
    private LauncherAnnouncement? _currentLaunchAnnouncement;
    private bool _isLaunchInProgress;
    private bool _isLaunchProfileActionInProgress;
    private bool _isLaunchProfileRefreshInProgress;
    private LaunchProfileSurfaceKind _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
    private int _launchProfileCompositionRefreshVersion;
    private bool _pendingLaunchAfterPrompt;
    private bool _showLaunchLog;
    private readonly List<string> _launchLogLines = [];
    private string _launchLogVisibleText = string.Empty;
    private int _launchLogVisibleStartIndex;
    private bool _isLaunchLogViewportPinned = true;
    private readonly HashSet<string> _dismissedLaunchPromptIds = new(StringComparer.Ordinal);
    private string _launchPromptContextKey = string.Empty;
    private string _helpSearchQuery = string.Empty;
    private int _selectedUpdateChannelIndex;
    private int _selectedUpdateModeIndex;
    private bool _isCheckingUpdate;
    private string _lastUpdateCheckSignature = string.Empty;
    private bool _isRefreshingFeedback;
    private DateTimeOffset _lastFeedbackRefreshUtc;
    private string _toolDownloadUrl = "https://example.invalid/files/demo-pack.zip";
    private string _toolDownloadUserAgent = "PCL-ME-Avalonia/1.0";
    private string _toolDownloadFolder = "/Users/demo/Downloads/PCL";
    private string _toolDownloadName = "demo-pack.zip";
    private string _officialSkinPlayerName = "Steve";
    private string _launchOfflineUserName = string.Empty;
    private int _selectedLaunchOfflineUuidModeIndex;
    private string _launchOfflineCustomUuid = string.Empty;
    private string _launchOfflineStatusText = string.Empty;
    private string _launchMicrosoftStatusText = string.Empty;
    private string _launchMicrosoftDeviceCode = string.Empty;
    private string _launchMicrosoftVerificationUrl = string.Empty;
    private string _launchAuthlibServer = "https://littleskin.cn/api/yggdrasil";
    private string _launchAuthlibLoginName = string.Empty;
    private string _launchAuthlibPassword = string.Empty;
    private string _launchAuthlibStatusText = string.Empty;
    private string _achievementBlockId = "diamond_sword";
    private string _achievementTitle = "Achievement Get!";
    private string _achievementFirstLine = "Time to Strike!";
    private string _achievementSecondLine = "PCL Frontend Avalonia";
    private bool _showAchievementPreview;
    private int _selectedHeadSizeIndex;
    private string _selectedHeadSkinPath = string.Empty;
    private string _downloadInstallName = string.Empty;
    private string _downloadCatalogIntroTitle = string.Empty;
    private string _downloadCatalogIntroBody = string.Empty;
    private string _downloadCatalogLoadingText = string.Empty;
    private string _downloadFavoriteSearchQuery = string.Empty;
    private int _selectedDownloadFavoriteTargetIndex;
    private string _downloadFavoriteWarningText = string.Empty;
    private bool _showDownloadFavoriteWarning;
    private readonly HashSet<string> _downloadFavoriteSelectedProjectIds = new(StringComparer.OrdinalIgnoreCase);
    private string _downloadFavoriteSelectionTargetId = string.Empty;
    private bool _suppressDownloadFavoriteSelectionChanged;
    private CancellationTokenSource? _downloadCatalogRefreshCts;
    private int _downloadCatalogRefreshVersion;
    private CancellationTokenSource? _downloadFavoriteRefreshCts;
    private int _downloadFavoriteRefreshVersion;
    private bool _isDownloadCatalogLoading;
    private bool _isDownloadFavoriteLoading;
    private bool _isDownloadResourceLoading;
    private readonly Dictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState> _downloadResourceRuntimeStates = new();
    private int _communityProjectRefreshVersion;
    private bool _isCommunityProjectLoading;
    private string _communityProjectLoadingText = string.Empty;
    private string _selectedCommunityProjectTitleHint = string.Empty;
    private int _selectedLaunchIsolationIndex = 1;
    private string _launchWindowTitle = "{}{name} | Player: {user} | Signed in via {login}";
    private string _launchCustomInfo = "PCL";
    private int _selectedLaunchVisibilityIndex = 4;
    private int _selectedLaunchPriorityIndex = 1;
    private int _selectedLaunchWindowTypeIndex = 1;
    private string _launchWindowWidth = "854";
    private string _launchWindowHeight = "480";
    private bool _useAutomaticRamAllocation = true;
    private double _customRamAllocation = 3;
    private double _launchUsedRamGb;
    private double _launchTotalRamGb;
    private double _launchAutomaticAllocatedRamGb;
    private bool _optimizeMemoryBeforeLaunch = true;
    private bool _isLaunchAdvancedOptionsExpanded;
    private int _selectedLaunchRendererIndex;
    private string _launchJvmArguments = "-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions";
    private string _launchGameArguments = string.Empty;
    private string _launchBeforeCommand = string.Empty;
    private string _launchEnvironmentVariables = string.Empty;
    private bool _waitForLaunchBeforeCommand;
    private bool _forceX11OnWaylandForLaunch;
    private bool _disableJavaLaunchWrapper;
    private bool _disableRetroWrapper;
    private bool _requireDedicatedGpu = true;
    private bool _useJavaExecutable;
    private int _selectedLaunchMicrosoftAuthIndex;
    private int _selectedLaunchPreferredIpStackIndex;
    private int _selectedDownloadSourceIndex;
    private int _selectedVersionSourceIndex;
    private double _downloadThreadLimit = 63;
    private double _downloadSpeedLimit = 42;
    private double _downloadTimeoutSeconds = 8;
    private bool _autoSelectNewInstance = true;
    private bool _upgradePartialAuthlib = true;
    private int _selectedCommunityDownloadSourceIndex = 1;
    private int _selectedFileNameFormatIndex = 1;
    private int _selectedModLocalNameStyleIndex;
    private bool _ignoreQuiltLoader;
    private bool _notifyReleaseUpdates = true;
    private bool _notifySnapshotUpdates;
    private bool _autoSwitchGameLanguageToChinese = true;
    private bool _detectClipboardResourceLinks = true;
    private int _selectedLauncherLocaleIndex;
    private int _selectedSystemActivityIndex;
    private double _animationFpsLimit = 59;
    private double _maxRealTimeLogValue = 13;
    private bool _disableHardwareAcceleration;
    private bool _isLaunchBlockedByPrompt;
    private bool _ignoreJavaCompatibilityWarningOnce;
    private bool _enableDoH = true;
    private int _selectedHttpProxyTypeIndex;
    private string _httpProxyAddress = string.Empty;
    private string _httpProxyUsername = string.Empty;
    private string _httpProxyPassword = string.Empty;
    private double _debugAnimationSpeed = 30;
    private bool _skipCopyDuringDownload;
    private bool _debugModeEnabled;
    private bool _debugDelayEnabled;
    private int _selectedDarkModeIndex = 2;
    private int _selectedLightColorIndex;
    private int _selectedDarkColorIndex;
    private string _customLightThemeColorHex = string.Empty;
    private string _customDarkThemeColorHex = string.Empty;
    private double _launcherOpacity = 360;
    private bool _showLauncherLogo = true;
    private bool _lockWindowSize;
    private bool _showLaunchingHint = true;
    private int _selectedGlobalFontIndex;
    private int _selectedMotdFontIndex = 1;
    private bool _backgroundColorful = true;
    private double _backgroundOpacity = 1000;
    private double _backgroundBlur;
    private int _selectedBackgroundSuitIndex;
    private double _musicVolume = 680;
    private bool _musicRandomPlay = true;
    private bool _musicAutoStart;
    private bool _musicStartOnGameLaunch = true;
    private bool _musicStopOnGameLaunch;
    private bool _musicEnableSmtc = true;
    private int _selectedLogoTypeIndex = 1;
    private bool _logoAlignLeft = true;
    private string _logoText = "Plain Craft Launcher";
    private int _selectedHomepageTypeIndex = 1;
    private string _homepageUrl = "https://example.invalid/homepage.json";
    private int _selectedHomepagePresetIndex = 11;
    private bool _showHiddenItemsOverride;
    private Bitmap? _titleBarLogoImage;
    private string _selectedJavaRuntimeKey = "auto";
    private bool _suppressSetupPersistence;
    private bool _suppressInstancePersistence;
    private bool _suppressToolsPersistence;
    private Task _selectedInstanceRefreshTask = Task.CompletedTask;
    private bool _hasOptimisticLaunchInstanceName;
    private string _optimisticLaunchInstanceName = string.Empty;

    public static FrontendShellViewModel CreateBootstrap(
        AvaloniaCommandOptions options,
        FrontendShellActionService shellActionService,
        II18nService i18nService)
    {
        return new FrontendShellViewModel(options, shellActionService, i18nService);
    }

    private FrontendShellViewModel(
        AvaloniaCommandOptions options,
        FrontendShellActionService shellActionService,
        II18nService i18nService)
    {
        _options = options;
        _i18n = i18nService;
        _setupText = CreateSetupLocalizationCatalog();
        _launcherLocaleKeys = _i18n.AvailableLocales;
        _launcherLocaleOptions = _launcherLocaleKeys.Select(FormatLauncherLocaleOption).ToArray();
        _shellActionService = shellActionService;
        _updateStatus = FrontendSetupUpdateStatusService.CreateDefault(_i18n);
        _selectedHeadSkinPath = _i18n.T("shell.tools.test.head.no_skin_selected");
        _shellActionService.ConfirmPresenter = ShowInAppConfirmationAsync;
        _shellActionService.TextInputPresenter = ShowInAppTextInputAsync;
        _shellActionService.ChoicePresenter = ShowInAppChoiceAsync;
        _shellComposition = FrontendShellCompositionService.Compose(options);
        _setupComposition = FrontendSetupCompositionService.Compose(shellActionService.RuntimePaths, i18nService);
        _currentRoute = NormalizeRoute(_shellComposition.NavigationRequest.CurrentRoute);
        var initialInstanceLoadMode = _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
            ? ResolveInstanceCompositionLoadMode(_currentRoute)
            : FrontendInstanceCompositionService.LoadMode.Lightweight;
        _instanceComposition = FrontendInstanceCompositionService.Compose(shellActionService.RuntimePaths, initialInstanceLoadMode, _i18n);
        _instanceCompositionLoadMode = initialInstanceLoadMode;
        _toolsComposition = FrontendToolsCompositionService.Compose(shellActionService.RuntimePaths, _i18n.Locale);
        ReloadVersionSavesComposition();
        ReloadDownloadComposition();
        _startupPlan = new StartupAvaloniaPlan(
            LauncherStartupWorkflowService.BuildPlan(_shellComposition.StartupWorkflowRequest),
            _shellComposition.StartupConsentResult);
        _launchComposition = FrontendLaunchCompositionService.Compose(options, shellActionService.RuntimePaths);
        _launchPromptContextKey = BuildLaunchPromptContextKey(_launchComposition, _instanceComposition.Selection.InstanceDirectory);
        _crashPlan = FrontendCrashCompositionService.Compose(shellActionService.RuntimePaths, _i18n);
        _activeCrashPlan = _crashPlan;
        _selectedPromptLane = AvaloniaPromptLaneKind.Startup;
        _backCommand = new ActionCommand(NavigateBack, () => CanGoBack);
        _homeCommand = new ActionCommand(NavigateHome, () => CanGoHome);
        _togglePromptOverlayCommand = new ActionCommand(TogglePromptOverlay);
        _dismissPromptOverlayCommand = new ActionCommand(() => SetPromptOverlayOpen(false));
        _openTaskManagerShortcutCommand = new ActionCommand(() => NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
            "Opened Task Manager from the bottom-right shortcut.",
            RouteNavigationBehavior.Child));
        _launchCommand = new ActionCommand(() => _ = HandleLaunchRequestedAsync(), () => !_isLaunchInProgress);
        _cancelLaunchCommand = new ActionCommand(HandleCancelLaunchRequested, () => IsLaunchDialogVisible);
        _versionSelectCommand = new ActionCommand(() => NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSelect), "Opened instance selection from the launch pane."));
        _versionSetupCommand = new ActionCommand(() => NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup),
            "Opened instance settings from the launch pane."));
        _toggleLaunchMigrationCommand = new ActionCommand(ToggleLaunchMigrationCard);
        _toggleLaunchNewsCommand = new ActionCommand(ToggleLaunchNewsCard);
        _dismissLaunchCommunityHintCommand = new ActionCommand(DismissCurrentLaunchAnnouncement);
        _selectLaunchProfileCommand = new ActionCommand(() => _ = SelectLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress);
        _addLaunchProfileCommand = new ActionCommand(() => _ = AddLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress);
        _createOfflineLaunchProfileCommand = new ActionCommand(() => _ = CreateOfflineLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress);
        _loginMicrosoftLaunchProfileCommand = new ActionCommand(() => _ = LoginMicrosoftLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress);
        _loginAuthlibLaunchProfileCommand = new ActionCommand(() => _ = LoginAuthlibLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress);
        _refreshLaunchProfileCommand = new ActionCommand(() => _ = RefreshSelectedLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress && !_isLaunchInProgress && CanRefreshLaunchProfile);
        _backLaunchProfileCommand = new ActionCommand(BackLaunchProfileSurface, () => !_isLaunchProfileActionInProgress);
        _submitOfflineLaunchProfileCommand = new ActionCommand(() => _ = SubmitOfflineLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress);
        _submitMicrosoftLaunchProfileCommand = new ActionCommand(() => _ = SubmitMicrosoftLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress);
        _openMicrosoftDeviceLinkCommand = new ActionCommand(OpenMicrosoftDeviceLink, () => !_isLaunchProfileActionInProgress);
        _submitAuthlibLaunchProfileCommand = new ActionCommand(() => _ = SubmitAuthlibLaunchProfileAsync(), () => !_isLaunchProfileActionInProgress);
        _useLittleSkinLaunchProfileCommand = new ActionCommand(ApplyLittleSkinLaunchProfilePreset, () => !_isLaunchProfileActionInProgress);
        _openFeedbackCommand = CreateLinkCommand("Open feedback", "https://github.com/TheUnknownThing/PCL-ME/issues");
        _exportLogCommand = new ActionCommand(() => ExportLauncherLogs(includeAllLogs: false));
        _exportAllLogsCommand = new ActionCommand(() => ExportLauncherLogs(includeAllLogs: true));
        _openLogDirectoryCommand = new ActionCommand(OpenLauncherLogDirectory);
        _cleanLogsCommand = new ActionCommand(CleanLauncherLogs);
        _downloadUpdateCommand = new ActionCommand(DownloadAvailableUpdate);
        _showUpdateDetailCommand = new ActionCommand(ShowAvailableUpdateDetail);
        _checkUpdateAgainCommand = new ActionCommand(() => _ = CheckForLauncherUpdatesAsync(forceRefresh: true));
        _openFullChangelogCommand = CreateLinkCommand("View changelog", "https://github.com/TheUnknownThing/PCL-ME/releases");
        _resetGameManageSettingsCommand = new ActionCommand(ResetGameManageSurface);
        _resetLauncherMiscSettingsCommand = new ActionCommand(ResetLauncherMiscSurface);
        _exportSettingsCommand = new ActionCommand(ExportSettingsSnapshot);
        _importSettingsCommand = new ActionCommand(() => _ = ImportSettingsAsync());
        _applyProxySettingsCommand = new ActionCommand(ApplyProxySettings);
        _addJavaRuntimeCommand = new ActionCommand(() => _ = AddJavaRuntimeAsync());
        _selectAutoJavaCommand = new ActionCommand(() => SelectJavaRuntime("auto"));
        _resetUiSettingsCommand = new ActionCommand(ResetUiSurface);
        _openSnapshotBuildCommand = CreateLinkCommand("Get official snapshot build", "https://github.com/TheUnknownThing/PCL-ME");
        _backgroundOpenFolderCommand = new ActionCommand(OpenBackgroundFolder);
        _backgroundRefreshCommand = new ActionCommand(RefreshBackgroundAssets);
        _backgroundClearCommand = new ActionCommand(ClearBackgroundAssets);
        _musicOpenFolderCommand = new ActionCommand(OpenMusicFolder);
        _musicRefreshCommand = new ActionCommand(RefreshMusicAssets);
        _musicClearCommand = new ActionCommand(ClearMusicAssets);
        _changeLogoImageCommand = new ActionCommand(() => _ = ChangeLogoImageAsync());
        _deleteLogoImageCommand = new ActionCommand(DeleteLogoImage);
        _refreshHomepageCommand = new ActionCommand(RefreshHomepageContent);
        _generateHomepageTutorialFileCommand = new ActionCommand(GenerateHomepageTutorialFile);
        _viewHomepageTutorialCommand = new ActionCommand(ViewHomepageTutorial);
        _toggleLaunchAdvancedOptionsCommand = new ActionCommand(() => IsLaunchAdvancedOptionsExpanded = !IsLaunchAdvancedOptionsExpanded);
        _openPysioWebsiteCommand = CreateLinkCommand("Pysio's Home", "https://pysio.online/");
        _selectDownloadFolderCommand = new ActionCommand(() => _ = SelectDownloadFolderAsync());
        _startCustomDownloadCommand = new ActionCommand(() => _ = StartCustomDownloadAsync());
        _openCustomDownloadFolderCommand = new ActionCommand(OpenCustomDownloadFolder);
        _saveOfficialSkinCommand = new ActionCommand(SaveOfficialSkin);
        _previewAchievementCommand = new ActionCommand(PreviewAchievement);
        _saveAchievementCommand = new ActionCommand(() => _ = SaveAchievementAsync());
        _queryMinecraftServerCommand = new ActionCommand(() => _ = QueryMinecraftServerAsync());
        _selectHeadSkinCommand = new ActionCommand(() => _ = SelectHeadSkinAsync());
        _saveHeadCommand = new ActionCommand(() => _ = SaveHeadAsync());
        _resetDownloadInstallSurfaceCommand = new ActionCommand(ResetDownloadInstallSurface);
        _resetDownloadResourceFiltersCommand = new ActionCommand(ResetDownloadResourceFilters);
        _searchDownloadResourceCommand = new ActionCommand(SearchDownloadResource);
        _installDownloadResourceModPackCommand = new ActionCommand(InstallDownloadResourceModPack);
        _firstDownloadResourcePageCommand = new ActionCommand(GoToFirstDownloadResourcePage, () => _downloadResourcePageIndex > 0);
        _previousDownloadResourcePageCommand = new ActionCommand(GoToPreviousDownloadResourcePage, () => _downloadResourcePageIndex > 0);
        _nextDownloadResourcePageCommand = new ActionCommand(GoToNextDownloadResourcePage, () => _downloadResourcePageIndex < _downloadResourceTotalPages - 1 || _downloadResourceHasMoreEntries);
        _manageDownloadFavoriteTargetCommand = new ActionCommand(() => _ = ManageDownloadFavoriteTargetsAsync());
        _resetInstanceExportOptionsCommand = new ActionCommand(ResetInstanceExportOptions);
        _importInstanceExportConfigCommand = new ActionCommand(() => _ = ImportInstanceExportConfigAsync());
        _saveInstanceExportConfigCommand = new ActionCommand(() => _ = SaveInstanceExportConfigAsync());
        _openInstanceExportGuideCommand = new ActionCommand(() => _ = OpenInstanceExportGuideAsync());
        _startInstanceExportCommand = new ActionCommand(StartInstanceExport);
        _setLittleSkinCommand = new ActionCommand(() =>
        {
            SelectedInstanceServerLoginRequireIndex = 2;
            InstanceServerAuthServer = "https://littleskin.cn/api/yggdrasil";
            InstanceServerAuthRegister = "https://littleskin.cn/auth/register";
            InstanceServerAuthName = "LittleSkin";
            AddActivity(T("launch.profile.authlib.activities.use_littleskin"), InstanceServerAuthServer);
        });
        _lockInstanceLoginCommand = new ActionCommand(LockInstanceLogin);
        _createInstanceProfileCommand = new ActionCommand(() => _ = CreateInstanceProfileAsync());
        _openGlobalLaunchSettingsCommand = new ActionCommand(() => NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch), "Opened the shared launch settings from instance settings."));
        _refreshInstanceSelectionCommand = new ActionCommand(RefreshInstanceSelectionSurface);
        _clearInstanceSelectionSearchCommand = new ActionCommand(() => InstanceSelectionSearchQuery = string.Empty);
        _addInstanceSelectionFolderCommand = new ActionCommand(() => _ = AddInstanceSelectionFolderAsync());
        _importInstanceSelectionPackCommand = new ActionCommand(() => _ = ImportInstanceSelectionPackAsync());
        _openInstanceSelectionDownloadCommand = new ActionCommand(() => NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadInstall),
            LT("shell.instance_select.empty.open_download_activity")));
        _refreshTaskManagerCommand = new ActionCommand(RefreshTaskManagerSurface);
        _clearFinishedTasksCommand = new ActionCommand(() =>
        {
            TaskCenter.RemoveFinished();
            RefreshTaskManagerSurface();
            RefreshShell(LT("shell.task_manager.actions.clear_finished"));
        });
        _refreshGameLogCommand = new ActionCommand(RefreshGameLogSurface);
        _clearGameLogCommand = new ActionCommand(ClearGameLogSurface);

        ScenarioLabel = $"Scenario: {options.Scenario}";
        EnvironmentLabel = _shellComposition.EnvironmentLabel;
        InputLabel = _shellComposition.InputLabel;

        _promptCatalog = BuildPromptCatalog(options.Scenario);
        _i18n.Changed += HandleI18nChanged;
        PropertyChanged += (_, args) => PersistSetupSetting(args.PropertyName);
        PropertyChanged += (_, args) => PersistInstanceSetting(args.PropertyName);
        PropertyChanged += (_, args) => PersistToolsSetting(args.PropertyName);
        PropertyChanged += (_, args) => HandleReactiveSettingChange(args.PropertyName);
        InitializeAboutEntries();
        InitializeFeedbackSections();
        ApplyToolsComposition(_toolsComposition);
        InitializeDownloadInstallSurface();
        ApplySetupComposition(_setupComposition);
        ApplyInstanceComposition(_instanceComposition);
        InitializeStepOneSurfaces();
        InitializePromptLanes();
        RefreshHelpTopics();
        RefreshShell("Shell initialized from portable frontend contracts.");
        _ = WarmPortableJavaSearchAsync();
        if (_currentRoute.Page == LauncherFrontendPageKey.Setup && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUpdate)
        {
            _ = CheckForLauncherUpdatesAsync(forceRefresh: false);
        }
    }

    private async Task WarmPortableJavaSearchAsync()
    {
        try
        {
            await FrontendJavaInventoryService.WarmPortableJavaScanCacheAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_currentRoute.Page == LauncherFrontendPageKey.Setup
                    && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupJava)
                {
                    ReloadSetupComposition(initializeAllSurfaces: false);
                }

                RefreshLaunchState();
                RefreshShell("Portable Java search cache refreshed.");
            });
        }
        catch
        {
            // Java search warm-up is best-effort and should never block shell startup.
        }
    }

}
