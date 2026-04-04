using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel : ViewModelBase
{
    public ObservableCollection<NavigationEntryViewModel> TopLevelEntries { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> SidebarEntries { get; } = [];

    public ObservableCollection<SidebarSectionViewModel> SidebarSections { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> UtilityEntries { get; } = [];

    public ObservableCollection<SurfaceFactViewModel> SurfaceFacts { get; } = [];

    public ObservableCollection<SurfaceSectionViewModel> SurfaceSections { get; } = [];

    public ObservableCollection<ActivityItemViewModel> ActivityEntries { get; } = [];

    public ObservableCollection<PromptLaneViewModel> PromptLanes { get; } = [];

    public ObservableCollection<PromptCardViewModel> ActivePrompts { get; } = [];

    public ObservableCollection<AboutEntryViewModel> AboutProjectEntries { get; } = [];

    public ObservableCollection<AboutEntryViewModel> AboutAcknowledgementEntries { get; } = [];

    public ObservableCollection<FeedbackSectionViewModel> FeedbackSections { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> LogEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> GameLinkPolicyEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> GameLinkPlayerEntries { get; } = [];

    public ObservableCollection<ToolboxActionViewModel> ToolboxActions { get; } = [];

    public ObservableCollection<SurfaceNoticeViewModel> DownloadInstallHints { get; } = [];

    public ObservableCollection<DownloadInstallOptionViewModel> DownloadInstallOptions { get; } = [];

    public ObservableCollection<DownloadCatalogActionViewModel> DownloadCatalogIntroActions { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> DownloadCatalogSections { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> DownloadFavoriteSections { get; } = [];

    public ObservableCollection<HelpTopicGroupViewModel> HelpTopicGroups { get; } = [];

    public ObservableCollection<JavaRuntimeEntryViewModel> JavaRuntimeEntries { get; } = [];

    public ObservableCollection<UiFeatureToggleGroupViewModel> UiFeatureToggleGroups { get; } = [];

    public string ScenarioLabel { get; }

    public string EnvironmentLabel { get; }

    public string InputLabel { get; }

    public string HelpSearchQuery
    {
        get => _helpSearchQuery;
        set
        {
            if (SetProperty(ref _helpSearchQuery, value))
            {
                RefreshHelpTopics();
            }
        }
    }

    public string Eyebrow
    {
        get => _eyebrow;
        private set => SetProperty(ref _eyebrow, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string BreadcrumbTrail
    {
        get => _breadcrumbTrail;
        private set => SetProperty(ref _breadcrumbTrail, value);
    }

    public string SurfaceMeta
    {
        get => _surfaceMeta;
        private set => SetProperty(ref _surfaceMeta, value);
    }

    public string PromptInboxTitle
    {
        get => _promptInboxTitle;
        private set => SetProperty(ref _promptInboxTitle, value);
    }

    public string PromptInboxSummary
    {
        get => _promptInboxSummary;
        private set => SetProperty(ref _promptInboxSummary, value);
    }

    public string PromptEmptyState
    {
        get => _promptEmptyState;
        private set => SetProperty(ref _promptEmptyState, value);
    }

    public bool IsLaunchRoute => _currentRoute.Page == LauncherFrontendPageKey.Launch;

    public bool IsStandardShellRoute => !IsLaunchRoute;

    public bool ShowTopLevelNavigation => !CanGoBack;

    public bool ShowInnerNavigation => CanGoBack;

    public bool HasActivePrompts => ActivePrompts.Count > 0;

    public bool HasNoActivePrompts => !HasActivePrompts;

    public bool IsPromptOverlayVisible => HasActivePrompts && _isPromptOverlayOpen;

    public bool HasSidebarEntries => SidebarEntries.Count > 0;

    public bool HasSidebarSections => SidebarSections.Count > 0;

    public bool HasNoSidebarSections => !HasSidebarSections;

    public bool HasSurfaceFacts => SurfaceFacts.Count > 0;

    public bool HasSurfaceSections => SurfaceSections.Count > 0;

    public bool HasActivityEntries => ActivityEntries.Count > 0;

    public bool HasUtilityEntries => UtilityEntries.Count > 0;

    public bool IsSetupAboutSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupAbout;

    public bool IsSetupLaunchSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupLaunch;

    public bool IsSetupFeedbackSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupFeedback;

    public bool IsSetupLogSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupLog;

    public bool IsSetupUpdateSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUpdate;

    public bool IsSetupGameLinkSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupGameLink;

    public bool IsSetupGameManageSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupGameManage;

    public bool IsSetupLauncherMiscSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupLauncherMisc;

    public bool IsSetupJavaSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupJava;

    public bool IsSetupUiSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUI;

    public bool IsDownloadInstallSurface => _currentRoute.Page == LauncherFrontendPageKey.Download
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadInstall;

    public bool IsDownloadCatalogSurface => _currentRoute.Page == LauncherFrontendPageKey.Download
        && _currentRoute.Subpage is LauncherFrontendSubpageKey.DownloadClient
            or LauncherFrontendSubpageKey.DownloadOptiFine
            or LauncherFrontendSubpageKey.DownloadForge
            or LauncherFrontendSubpageKey.DownloadNeoForge
            or LauncherFrontendSubpageKey.DownloadCleanroom
            or LauncherFrontendSubpageKey.DownloadFabric
            or LauncherFrontendSubpageKey.DownloadQuilt
            or LauncherFrontendSubpageKey.DownloadLiteLoader
            or LauncherFrontendSubpageKey.DownloadLabyMod
            or LauncherFrontendSubpageKey.DownloadLegacyFabric;

    public bool IsDownloadFavoritesSurface => _currentRoute.Page == LauncherFrontendPageKey.Download
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadCompFavorites;

    public bool IsToolsGameLinkSurface => _currentRoute.Page == LauncherFrontendPageKey.Tools
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.ToolsGameLink;

    public bool IsToolsHelpSurface => _currentRoute.Page == LauncherFrontendPageKey.Tools
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.ToolsLauncherHelp;

    public bool IsToolsTestSurface => _currentRoute.Page == LauncherFrontendPageKey.Tools
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.ToolsTest;

    public bool IsGenericShellSurface => IsStandardShellRoute
        && !IsSetupLaunchSurface
        && !IsSetupAboutSurface
        && !IsSetupFeedbackSurface
        && !IsSetupLogSurface
        && !IsSetupUpdateSurface
        && !IsSetupGameLinkSurface
        && !IsSetupGameManageSurface
        && !IsSetupLauncherMiscSurface
        && !IsSetupJavaSurface
        && !IsSetupUiSurface
        && !IsDownloadInstallSurface
        && !IsDownloadCatalogSurface
        && !IsDownloadFavoritesSurface
        && !IsToolsGameLinkSurface
        && !IsToolsHelpSurface
        && !IsToolsTestSurface;

    public bool HasAboutProjectEntries => AboutProjectEntries.Count > 0;

    public bool HasAboutAcknowledgementEntries => AboutAcknowledgementEntries.Count > 0;

    public bool HasFeedbackSections => FeedbackSections.Count > 0;

    public bool HasHelpTopicGroups => HelpTopicGroups.Count > 0;

    public bool HasNoHelpTopicGroups => !HasHelpTopicGroups;

    public string TitleBarLabel => _currentNavigation?.CurrentPage.SidebarItemTitle
        ?? _currentNavigation?.CurrentPage.Title
        ?? Title;

    public IReadOnlyList<string> LaunchIsolationOptions { get; } =
    [
        "关闭",
        "隔离可安装 Mod 的实例",
        "隔离非正式版",
        "隔离可安装 Mod 的实例与非正式版",
        "隔离所有实例"
    ];

    public IReadOnlyList<string> LaunchVisibilityOptions { get; } =
    [
        "游戏启动后立即关闭",
        "游戏启动后隐藏，游戏退出后自动关闭",
        "游戏启动后隐藏，游戏退出后重新打开",
        "游戏启动后最小化",
        "游戏启动后仍保持不变"
    ];

    public IReadOnlyList<string> LaunchPriorityOptions { get; } =
    [
        "高（优先保证游戏运行）",
        "中（平衡）",
        "低（优先保证其他程序运行）"
    ];

    public IReadOnlyList<string> LaunchWindowTypeOptions { get; } =
    [
        "全屏",
        "默认",
        "与启动器尺寸一致",
        "自定义尺寸",
        "最大化"
    ];

    public IReadOnlyList<string> LaunchMicrosoftAuthOptions { get; } =
    [
        "Web 账户管理器（暂时强制设备代码流）",
        "设备代码流"
    ];

    public IReadOnlyList<string> LaunchPreferredIpStackOptions { get; } =
    [
        "IPv4 优先",
        "Java 默认",
        "IPv6 优先"
    ];

    public IReadOnlyList<string> LaunchRendererOptions { get; } =
    [
        "游戏默认",
        "软渲染（llvmpipe）",
        "DirectX12（d3d12）",
        "Vulkan（zink）"
    ];

    public IReadOnlyList<string> UpdateChannelOptions { get; } =
    [
        "正式版 / Release",
        "测试版 / Beta",
        "开发版 / Dev"
    ];

    public IReadOnlyList<string> UpdateModeOptions { get; } =
    [
        "自动下载并安装更新",
        "自动下载并提示更新",
        "提示更新",
        "不自动检查更新（不推荐）"
    ];

    public int SelectedUpdateChannelIndex
    {
        get => _selectedUpdateChannelIndex;
        set
        {
            var clampedValue = Math.Clamp(value, 0, UpdateChannelOptions.Count - 1);
            if (SetProperty(ref _selectedUpdateChannelIndex, clampedValue))
            {
                AddActivity("切换更新通道", UpdateChannelOptions[clampedValue]);
            }
        }
    }

    public int SelectedUpdateModeIndex
    {
        get => _selectedUpdateModeIndex;
        set
        {
            var clampedValue = Math.Clamp(value, 0, UpdateModeOptions.Count - 1);
            if (SetProperty(ref _selectedUpdateModeIndex, clampedValue))
            {
                AddActivity("切换自动更新设置", UpdateModeOptions[clampedValue]);
            }
        }
    }

    public string MirrorCdk
    {
        get => _mirrorCdk;
        set => SetProperty(ref _mirrorCdk, value);
    }

    public Bitmap? UpdateAvailableIcon => File.Exists(UpdateAvailableIconFilePath)
        ? new Bitmap(UpdateAvailableIconFilePath)
        : null;

    public Bitmap? UpdateCurrentIcon => File.Exists(UpdateCurrentIconFilePath)
        ? new Bitmap(UpdateCurrentIconFilePath)
        : null;

    public Bitmap? UpdateOptionalIcon => File.Exists(UpdateOptionalIconFilePath)
        ? new Bitmap(UpdateOptionalIconFilePath)
        : null;

    public bool ShowAvailableUpdateCard => _updateSurfaceState == UpdateSurfaceState.Available;

    public bool ShowCurrentVersionCard => _updateSurfaceState == UpdateSurfaceState.Latest;

    public bool ShowOptionalUpdateCard => false;

    public string AvailableUpdateName => "PCL CE 2.14.0";

    public string AvailableUpdatePublisher => "by PCL-Community";

    public string AvailableUpdateSummary => "PCL CE 2.14.0 带来了让人眼前一亮的新设计和 Pigeon 智能的多项功能，同时还带来了令人愉快的跨实例工作方式，助你提高工作效率。";

    public string CurrentVersionName => "PCL CE 2.13.4";

    public string CurrentVersionDescription => ShowCurrentVersionCard ? "已是最新版本" : "正在检查更新...";

    public string OptionalUpdateName => "AquaCL 3.0.0";

    public string OptionalUpdateDescription => "20.0 MB";

    public string OptionalUpdateSummary => "AquaCL 3 是 PCL CE 的第一个主要更新，带来了让人眼前一亮的新设计，使用了最新开发技术，完全重构了基础体验，并更支持 macOS、Linux 等其他平台。";

    public int SelectedLaunchIsolationIndex
    {
        get => _selectedLaunchIsolationIndex;
        set => SetProperty(ref _selectedLaunchIsolationIndex, Math.Clamp(value, 0, LaunchIsolationOptions.Count - 1));
    }

    public string LaunchWindowTitleSetting
    {
        get => _launchWindowTitle;
        set => SetProperty(ref _launchWindowTitle, value);
    }

    public string LaunchCustomInfoSetting
    {
        get => _launchCustomInfo;
        set => SetProperty(ref _launchCustomInfo, value);
    }

    public int SelectedLaunchVisibilityIndex
    {
        get => _selectedLaunchVisibilityIndex;
        set => SetProperty(ref _selectedLaunchVisibilityIndex, Math.Clamp(value, 0, LaunchVisibilityOptions.Count - 1));
    }

    public int SelectedLaunchPriorityIndex
    {
        get => _selectedLaunchPriorityIndex;
        set => SetProperty(ref _selectedLaunchPriorityIndex, Math.Clamp(value, 0, LaunchPriorityOptions.Count - 1));
    }

    public int SelectedLaunchWindowTypeIndex
    {
        get => _selectedLaunchWindowTypeIndex;
        set
        {
            if (SetProperty(ref _selectedLaunchWindowTypeIndex, Math.Clamp(value, 0, LaunchWindowTypeOptions.Count - 1)))
            {
                RaisePropertyChanged(nameof(IsCustomLaunchWindowSizeVisible));
            }
        }
    }

    public bool IsCustomLaunchWindowSizeVisible => SelectedLaunchWindowTypeIndex == 3;

    public string LaunchWindowWidth
    {
        get => _launchWindowWidth;
        set => SetProperty(ref _launchWindowWidth, value);
    }

    public string LaunchWindowHeight
    {
        get => _launchWindowHeight;
        set => SetProperty(ref _launchWindowHeight, value);
    }

    public int SelectedLaunchMicrosoftAuthIndex
    {
        get => _selectedLaunchMicrosoftAuthIndex;
        set => SetProperty(ref _selectedLaunchMicrosoftAuthIndex, Math.Clamp(value, 0, LaunchMicrosoftAuthOptions.Count - 1));
    }

    public int SelectedLaunchPreferredIpStackIndex
    {
        get => _selectedLaunchPreferredIpStackIndex;
        set => SetProperty(ref _selectedLaunchPreferredIpStackIndex, Math.Clamp(value, 0, LaunchPreferredIpStackOptions.Count - 1));
    }

    public bool UseAutomaticRamAllocation
    {
        get => _useAutomaticRamAllocation;
        set
        {
            if (!value)
            {
                return;
            }

            if (SetProperty(ref _useAutomaticRamAllocation, true))
            {
                RaisePropertyChanged(nameof(UseCustomRamAllocation));
                RaisePropertyChanged(nameof(IsCustomRamAllocationEnabled));
                RaisePropertyChanged(nameof(AllocatedRamLabel));
                RaisePropertyChanged(nameof(ShowRamAllocationWarning));
            }
        }
    }

    public bool UseCustomRamAllocation
    {
        get => !_useAutomaticRamAllocation;
        set
        {
            if (!value)
            {
                return;
            }

            if (SetProperty(ref _useAutomaticRamAllocation, false))
            {
                RaisePropertyChanged(nameof(UseAutomaticRamAllocation));
                RaisePropertyChanged(nameof(IsCustomRamAllocationEnabled));
                RaisePropertyChanged(nameof(AllocatedRamLabel));
                RaisePropertyChanged(nameof(ShowRamAllocationWarning));
            }
        }
    }

    public bool IsCustomRamAllocationEnabled => UseCustomRamAllocation;

    public double CustomRamAllocation
    {
        get => _customRamAllocation;
        set
        {
            if (SetProperty(ref _customRamAllocation, value))
            {
                RaisePropertyChanged(nameof(CustomRamAllocationLabel));
                RaisePropertyChanged(nameof(AllocatedRamLabel));
                RaisePropertyChanged(nameof(ShowRamAllocationWarning));
            }
        }
    }

    public string CustomRamAllocationLabel => $"{Math.Round(CustomRamAllocation):0} GB";

    public string UsedRamLabel => "4.7 GB";

    public string TotalRamLabel => "7.9 GB";

    public string AllocatedRamLabel => UseAutomaticRamAllocation
        ? "2.5 GB"
        : $"{Math.Round(CustomRamAllocation):0.0} GB";

    public bool ShowRamAllocationWarning => UseCustomRamAllocation && CustomRamAllocation >= 8;

    public bool OptimizeMemoryBeforeLaunch
    {
        get => _optimizeMemoryBeforeLaunch;
        set => SetProperty(ref _optimizeMemoryBeforeLaunch, value);
    }

    public bool IsLaunchAdvancedOptionsExpanded
    {
        get => _isLaunchAdvancedOptionsExpanded;
        private set => SetProperty(ref _isLaunchAdvancedOptionsExpanded, value);
    }

    public int SelectedLaunchRendererIndex
    {
        get => _selectedLaunchRendererIndex;
        set => SetProperty(ref _selectedLaunchRendererIndex, Math.Clamp(value, 0, LaunchRendererOptions.Count - 1));
    }

    public string LaunchJvmArguments
    {
        get => _launchJvmArguments;
        set => SetProperty(ref _launchJvmArguments, value);
    }

    public string LaunchGameArguments
    {
        get => _launchGameArguments;
        set => SetProperty(ref _launchGameArguments, value);
    }

    public string LaunchBeforeCommand
    {
        get => _launchBeforeCommand;
        set => SetProperty(ref _launchBeforeCommand, value);
    }

    public bool WaitForLaunchBeforeCommand
    {
        get => _waitForLaunchBeforeCommand;
        set => SetProperty(ref _waitForLaunchBeforeCommand, value);
    }

    public bool DisableJavaLaunchWrapper
    {
        get => _disableJavaLaunchWrapper;
        set => SetProperty(ref _disableJavaLaunchWrapper, value);
    }

    public bool DisableRetroWrapper
    {
        get => _disableRetroWrapper;
        set => SetProperty(ref _disableRetroWrapper, value);
    }

    public bool RequireDedicatedGpu
    {
        get => _requireDedicatedGpu;
        set => SetProperty(ref _requireDedicatedGpu, value);
    }

    public bool UseJavaExecutable
    {
        get => _useJavaExecutable;
        set => SetProperty(ref _useJavaExecutable, value);
    }

    public string GameLinkAnnouncement
    {
        get => _gameLinkAnnouncement;
        set => SetProperty(ref _gameLinkAnnouncement, value);
    }

    public string GameLinkNatStatus
    {
        get => _gameLinkNatStatus;
        set => SetProperty(ref _gameLinkNatStatus, value);
    }

    public string GameLinkAccountStatus
    {
        get => _gameLinkAccountStatus;
        set => SetProperty(ref _gameLinkAccountStatus, value);
    }

    public string GameLinkLobbyId
    {
        get => _gameLinkLobbyId;
        set => SetProperty(ref _gameLinkLobbyId, value);
    }

    public string GameLinkSessionPing
    {
        get => _gameLinkSessionPing;
        set => SetProperty(ref _gameLinkSessionPing, value);
    }

    public string GameLinkSessionId
    {
        get => _gameLinkSessionId;
        set => SetProperty(ref _gameLinkSessionId, value);
    }

    public string GameLinkConnectionType
    {
        get => _gameLinkConnectionType;
        set => SetProperty(ref _gameLinkConnectionType, value);
    }

    public string GameLinkConnectedUserName
    {
        get => _gameLinkConnectedUserName;
        set => SetProperty(ref _gameLinkConnectedUserName, value);
    }

    public string GameLinkConnectedUserType
    {
        get => _gameLinkConnectedUserType;
        set => SetProperty(ref _gameLinkConnectedUserType, value);
    }

    public string GameLinkPlayerListTitle => GameLinkPlayerEntries.Count > 0
        ? $"大厅成员列表（{GameLinkPlayerEntries.Count} 人）"
        : "大厅成员列表（正在获取信息）";

    public IReadOnlyList<string> GameLinkWorldOptions { get; } =
    [
        "Modern Fabric Demo - 25565",
        "Legacy Forge Demo - 25566",
        "Quilt Snapshot Demo - 25567"
    ];

    public int SelectedGameLinkWorldIndex
    {
        get => _selectedGameLinkWorldIndex;
        set => SetProperty(ref _selectedGameLinkWorldIndex, Math.Clamp(value, 0, GameLinkWorldOptions.Count - 1));
    }

    public string ToolDownloadUrl
    {
        get => _toolDownloadUrl;
        set => SetProperty(ref _toolDownloadUrl, value);
    }

    public string ToolDownloadUserAgent
    {
        get => _toolDownloadUserAgent;
        set => SetProperty(ref _toolDownloadUserAgent, value);
    }

    public string ToolDownloadFolder
    {
        get => _toolDownloadFolder;
        set => SetProperty(ref _toolDownloadFolder, value);
    }

    public string ToolDownloadName
    {
        get => _toolDownloadName;
        set => SetProperty(ref _toolDownloadName, value);
    }

    public string OfficialSkinPlayerName
    {
        get => _officialSkinPlayerName;
        set => SetProperty(ref _officialSkinPlayerName, value);
    }

    public string AchievementBlockId
    {
        get => _achievementBlockId;
        set => SetProperty(ref _achievementBlockId, value);
    }

    public string AchievementTitle
    {
        get => _achievementTitle;
        set => SetProperty(ref _achievementTitle, value);
    }

    public string AchievementFirstLine
    {
        get => _achievementFirstLine;
        set => SetProperty(ref _achievementFirstLine, value);
    }

    public string AchievementSecondLine
    {
        get => _achievementSecondLine;
        set => SetProperty(ref _achievementSecondLine, value);
    }

    public bool ShowAchievementPreview
    {
        get => _showAchievementPreview;
        private set => SetProperty(ref _showAchievementPreview, value);
    }

    public IReadOnlyList<string> HeadSizeOptions { get; } =
    [
        "64x64",
        "96x96",
        "128x128"
    ];

    public int SelectedHeadSizeIndex
    {
        get => _selectedHeadSizeIndex;
        set
        {
            var nextValue = Math.Clamp(value, 0, HeadSizeOptions.Count - 1);
            if (SetProperty(ref _selectedHeadSizeIndex, nextValue))
            {
                RaisePropertyChanged(nameof(HeadPreviewSize));
            }
        }
    }

    public string SelectedHeadSkinPath
    {
        get => _selectedHeadSkinPath;
        set
        {
            if (SetProperty(ref _selectedHeadSkinPath, value))
            {
                RaisePropertyChanged(nameof(HasSelectedHeadSkin));
            }
        }
    }

    public bool HasSelectedHeadSkin => !string.Equals(SelectedHeadSkinPath, "尚未选择皮肤", StringComparison.Ordinal);

    public double HeadPreviewSize => SelectedHeadSizeIndex switch
    {
        0 => 80,
        1 => 96,
        _ => 112
    };

    public string DownloadInstallName
    {
        get => _downloadInstallName;
        set => SetProperty(ref _downloadInstallName, value);
    }

    public string DownloadCatalogIntroTitle
    {
        get => _downloadCatalogIntroTitle;
        private set
        {
            if (SetProperty(ref _downloadCatalogIntroTitle, value))
            {
                RaisePropertyChanged(nameof(HasDownloadCatalogIntro));
            }
        }
    }

    public string DownloadCatalogIntroBody
    {
        get => _downloadCatalogIntroBody;
        private set => SetProperty(ref _downloadCatalogIntroBody, value);
    }

    public bool HasDownloadCatalogIntro => !string.IsNullOrWhiteSpace(DownloadCatalogIntroTitle);

    public string DownloadFavoriteSearchQuery
    {
        get => _downloadFavoriteSearchQuery;
        set
        {
            if (SetProperty(ref _downloadFavoriteSearchQuery, value) && IsDownloadFavoritesSurface)
            {
                RefreshDownloadFavoriteSurface();
            }
        }
    }

    public IReadOnlyList<string> DownloadFavoriteTargetOptions { get; } =
    [
        "默认收藏夹",
        "整合包收藏",
        "建筑与材质"
    ];

    public int SelectedDownloadFavoriteTargetIndex
    {
        get => _selectedDownloadFavoriteTargetIndex;
        set
        {
            var nextValue = Math.Clamp(value, 0, DownloadFavoriteTargetOptions.Count - 1);
            if (SetProperty(ref _selectedDownloadFavoriteTargetIndex, nextValue) && IsDownloadFavoritesSurface)
            {
                RefreshDownloadFavoriteSurface();
            }
        }
    }

    public string DownloadFavoriteWarningText
    {
        get => _downloadFavoriteWarningText;
        private set => SetProperty(ref _downloadFavoriteWarningText, value);
    }

    public bool ShowDownloadFavoriteWarning
    {
        get => _showDownloadFavoriteWarning;
        private set => SetProperty(ref _showDownloadFavoriteWarning, value);
    }

    public bool HasDownloadFavoriteSections => DownloadFavoriteSections.Count > 0;

    public bool HasNoDownloadFavoriteSections => !HasDownloadFavoriteSections;

    public IReadOnlyList<string> LinkProtocolPreferenceOptions { get; } =
    [
        "TCP",
        "UDP"
    ];

    public string LinkUsername
    {
        get => _linkUsername;
        set => SetProperty(ref _linkUsername, value);
    }

    public int SelectedProtocolPreferenceIndex
    {
        get => _selectedProtocolPreferenceIndex;
        set => SetProperty(ref _selectedProtocolPreferenceIndex, Math.Clamp(value, 0, LinkProtocolPreferenceOptions.Count - 1));
    }

    public bool PreferLowestLatencyPath
    {
        get => _preferLowestLatencyPath;
        set => SetProperty(ref _preferLowestLatencyPath, value);
    }

    public bool TryPunchSymmetricNat
    {
        get => _tryPunchSymmetricNat;
        set => SetProperty(ref _tryPunchSymmetricNat, value);
    }

    public bool AllowIpv6Communication
    {
        get => _allowIpv6Communication;
        set => SetProperty(ref _allowIpv6Communication, value);
    }

    public bool EnableLinkCliOutput
    {
        get => _enableLinkCliOutput;
        set => SetProperty(ref _enableLinkCliOutput, value);
    }

    public IReadOnlyList<string> DownloadSourceOptions { get; } =
    [
        "尽量使用镜像源",
        "优先使用官方源，在加载缓慢时换用镜像源",
        "尽量使用官方源"
    ];

    public IReadOnlyList<string> FileNameFormatOptions { get; } =
    [
        "【机械动力】create-1.21.1-6.0.4",
        "[机械动力] create-1.21.1-6.0.4",
        "机械动力-create-1.21.1-6.0.4",
        "create-1.21.1-6.0.4-机械动力",
        "create-1.21.1-6.0.4"
    ];

    public IReadOnlyList<string> ModLocalNameStyleOptions { get; } =
    [
        "标题显示译名，详情显示文件名",
        "标题显示文件名，详情显示译名"
    ];

    public int SelectedDownloadSourceIndex
    {
        get => _selectedDownloadSourceIndex;
        set => SetProperty(ref _selectedDownloadSourceIndex, Math.Clamp(value, 0, DownloadSourceOptions.Count - 1));
    }

    public int SelectedVersionSourceIndex
    {
        get => _selectedVersionSourceIndex;
        set => SetProperty(ref _selectedVersionSourceIndex, Math.Clamp(value, 0, DownloadSourceOptions.Count - 1));
    }

    public double DownloadThreadLimit
    {
        get => _downloadThreadLimit;
        set
        {
            if (SetProperty(ref _downloadThreadLimit, value))
            {
                RaisePropertyChanged(nameof(DownloadThreadLimitLabel));
            }
        }
    }

    public string DownloadThreadLimitLabel => $"{Math.Round(DownloadThreadLimit)}";

    public double DownloadSpeedLimit
    {
        get => _downloadSpeedLimit;
        set
        {
            if (SetProperty(ref _downloadSpeedLimit, value))
            {
                RaisePropertyChanged(nameof(DownloadSpeedLimitLabel));
            }
        }
    }

    public string DownloadSpeedLimitLabel => $"{Math.Round(DownloadSpeedLimit)}";

    public bool AutoSelectNewInstance
    {
        get => _autoSelectNewInstance;
        set => SetProperty(ref _autoSelectNewInstance, value);
    }

    public bool UpgradePartialAuthlib
    {
        get => _upgradePartialAuthlib;
        set => SetProperty(ref _upgradePartialAuthlib, value);
    }

    public int SelectedCommunityDownloadSourceIndex
    {
        get => _selectedCommunityDownloadSourceIndex;
        set => SetProperty(ref _selectedCommunityDownloadSourceIndex, Math.Clamp(value, 0, DownloadSourceOptions.Count - 1));
    }

    public int SelectedFileNameFormatIndex
    {
        get => _selectedFileNameFormatIndex;
        set => SetProperty(ref _selectedFileNameFormatIndex, Math.Clamp(value, 0, FileNameFormatOptions.Count - 1));
    }

    public int SelectedModLocalNameStyleIndex
    {
        get => _selectedModLocalNameStyleIndex;
        set => SetProperty(ref _selectedModLocalNameStyleIndex, Math.Clamp(value, 0, ModLocalNameStyleOptions.Count - 1));
    }

    public bool IgnoreQuiltLoader
    {
        get => _ignoreQuiltLoader;
        set => SetProperty(ref _ignoreQuiltLoader, value);
    }

    public bool NotifyReleaseUpdates
    {
        get => _notifyReleaseUpdates;
        set => SetProperty(ref _notifyReleaseUpdates, value);
    }

    public bool NotifySnapshotUpdates
    {
        get => _notifySnapshotUpdates;
        set => SetProperty(ref _notifySnapshotUpdates, value);
    }

    public bool AutoSwitchGameLanguageToChinese
    {
        get => _autoSwitchGameLanguageToChinese;
        set => SetProperty(ref _autoSwitchGameLanguageToChinese, value);
    }

    public bool DetectClipboardResourceLinks
    {
        get => _detectClipboardResourceLinks;
        set => SetProperty(ref _detectClipboardResourceLinks, value);
    }

    public IReadOnlyList<string> SystemActivityOptions { get; } =
    [
        "显示所有公告",
        "仅在有重要通知时显示公告",
        "关闭所有公告"
    ];

    public int SelectedSystemActivityIndex
    {
        get => _selectedSystemActivityIndex;
        set => SetProperty(ref _selectedSystemActivityIndex, Math.Clamp(value, 0, SystemActivityOptions.Count - 1));
    }

    public double AnimationFpsLimit
    {
        get => _animationFpsLimit;
        set
        {
            if (SetProperty(ref _animationFpsLimit, value))
            {
                RaisePropertyChanged(nameof(AnimationFpsLabel));
            }
        }
    }

    public string AnimationFpsLabel => $"{Math.Round(AnimationFpsLimit) + 1} FPS";

    public double MaxRealTimeLogValue
    {
        get => _maxRealTimeLogValue;
        set
        {
            if (SetProperty(ref _maxRealTimeLogValue, value))
            {
                RaisePropertyChanged(nameof(MaxRealTimeLogLabel));
            }
        }
    }

    public string MaxRealTimeLogLabel => FormatMaxRealTimeLog(MaxRealTimeLogValue);

    public bool DisableHardwareAcceleration
    {
        get => _disableHardwareAcceleration;
        set => SetProperty(ref _disableHardwareAcceleration, value);
    }

    public bool EnableTelemetry
    {
        get => _enableTelemetry;
        set => SetProperty(ref _enableTelemetry, value);
    }

    public bool EnableDoH
    {
        get => _enableDoH;
        set => SetProperty(ref _enableDoH, value);
    }

    public int SelectedHttpProxyTypeIndex
    {
        get => _selectedHttpProxyTypeIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, 2);
            if (SetProperty(ref _selectedHttpProxyTypeIndex, clamped))
            {
                RaisePropertyChanged(nameof(IsCustomHttpProxyEnabled));
                RaisePropertyChanged(nameof(IsNoHttpProxySelected));
                RaisePropertyChanged(nameof(IsSystemHttpProxySelected));
                RaisePropertyChanged(nameof(IsCustomHttpProxySelected));
            }
        }
    }

    public bool IsCustomHttpProxyEnabled => SelectedHttpProxyTypeIndex == 2;

    public bool IsNoHttpProxySelected
    {
        get => SelectedHttpProxyTypeIndex == 0;
        set
        {
            if (value)
            {
                SelectedHttpProxyTypeIndex = 0;
            }
        }
    }

    public bool IsSystemHttpProxySelected
    {
        get => SelectedHttpProxyTypeIndex == 1;
        set
        {
            if (value)
            {
                SelectedHttpProxyTypeIndex = 1;
            }
        }
    }

    public bool IsCustomHttpProxySelected
    {
        get => SelectedHttpProxyTypeIndex == 2;
        set
        {
            if (value)
            {
                SelectedHttpProxyTypeIndex = 2;
            }
        }
    }

    public string HttpProxyAddress
    {
        get => _httpProxyAddress;
        set => SetProperty(ref _httpProxyAddress, value);
    }

    public string HttpProxyUsername
    {
        get => _httpProxyUsername;
        set => SetProperty(ref _httpProxyUsername, value);
    }

    public string HttpProxyPassword
    {
        get => _httpProxyPassword;
        set => SetProperty(ref _httpProxyPassword, value);
    }

    public double DebugAnimationSpeed
    {
        get => _debugAnimationSpeed;
        set
        {
            if (SetProperty(ref _debugAnimationSpeed, value))
            {
                RaisePropertyChanged(nameof(DebugAnimationSpeedLabel));
            }
        }
    }

    public string DebugAnimationSpeedLabel => Math.Round(DebugAnimationSpeed) > 29
        ? "关闭"
        : $"{Math.Round(DebugAnimationSpeed / 10 + 0.1, 1):0.0}x";

    public bool SkipCopyDuringDownload
    {
        get => _skipCopyDuringDownload;
        set => SetProperty(ref _skipCopyDuringDownload, value);
    }

    public bool DebugModeEnabled
    {
        get => _debugModeEnabled;
        set => SetProperty(ref _debugModeEnabled, value);
    }

    public bool DebugDelayEnabled
    {
        get => _debugDelayEnabled;
        set => SetProperty(ref _debugDelayEnabled, value);
    }

    public IReadOnlyList<string> DarkModeOptions { get; } =
    [
        "浅色",
        "深色",
        "跟随系统"
    ];

    public IReadOnlyList<string> ThemeColorOptions { get; } =
    [
        "龙猫蓝",
        "甜柠青",
        "小草绿",
        "菠萝黄",
        "橡木棕"
    ];

    public IReadOnlyList<string> BlurTypeOptions { get; } =
    [
        "高斯模糊",
        "方框模糊"
    ];

    public IReadOnlyList<string> FontOptions { get; } =
    [
        "默认字体",
        "思源黑体",
        "霞鹜文楷",
        "JetBrains Mono"
    ];

    public IReadOnlyList<string> HomepagePresetOptions { get; } =
    [
        "你知道吗？",
        "Minecraft 新闻（作者：最亮的信标）",
        "简单主页（作者：MFn233）",
        "每日整合包推荐（作者：wkea）",
        "Minecraft 皮肤推荐（作者：wkea）",
        "OpenBMCLAPI 仪表盘 Lite（作者：Silverteal、Mxmilu666）",
        "PCL 主页市场（作者：凌云）",
        "PCL 新闻速报（作者：Joker2184）",
        "PCL 新功能说明书（作者：WForst-Breeze）",
        "杂志主页（作者：CreeperIsASpy）",
        "PCL GitHub 仪表盘（作者：Deep-Dark-Forest）",
        "Minecraft 更新摘要（作者：pynickle，部分由 AI 生成）",
        "PCL CE 公告栏",
        "Minecraft 官方信息流"
    ];

    public int SelectedDarkModeIndex
    {
        get => _selectedDarkModeIndex;
        set => SetProperty(ref _selectedDarkModeIndex, Math.Clamp(value, 0, DarkModeOptions.Count - 1));
    }

    public int SelectedLightColorIndex
    {
        get => _selectedLightColorIndex;
        set => SetProperty(ref _selectedLightColorIndex, Math.Clamp(value, 0, ThemeColorOptions.Count - 1));
    }

    public int SelectedDarkColorIndex
    {
        get => _selectedDarkColorIndex;
        set => SetProperty(ref _selectedDarkColorIndex, Math.Clamp(value, 0, ThemeColorOptions.Count - 1));
    }

    public double LauncherOpacity
    {
        get => _launcherOpacity;
        set
        {
            if (SetProperty(ref _launcherOpacity, value))
            {
                RaisePropertyChanged(nameof(LauncherOpacityLabel));
            }
        }
    }

    public string LauncherOpacityLabel => $"{Math.Round(LauncherOpacity / 10)}%";

    public bool ShowLauncherLogoSetting
    {
        get => _showLauncherLogo;
        set => SetProperty(ref _showLauncherLogo, value);
    }

    public bool LockWindowSizeSetting
    {
        get => _lockWindowSize;
        set => SetProperty(ref _lockWindowSize, value);
    }

    public bool ShowLaunchingHintSetting
    {
        get => _showLaunchingHint;
        set => SetProperty(ref _showLaunchingHint, value);
    }

    public bool EnableAdvancedMaterial
    {
        get => _enableAdvancedMaterial;
        set => SetProperty(ref _enableAdvancedMaterial, value);
    }

    public double BlurRadius
    {
        get => _blurRadius;
        set
        {
            if (SetProperty(ref _blurRadius, value))
            {
                RaisePropertyChanged(nameof(BlurRadiusLabel));
            }
        }
    }

    public string BlurRadiusLabel => $"{Math.Round(BlurRadius)}";

    public double BlurSamplingRate
    {
        get => _blurSamplingRate;
        set
        {
            if (SetProperty(ref _blurSamplingRate, value))
            {
                RaisePropertyChanged(nameof(BlurSamplingRateLabel));
            }
        }
    }

    public string BlurSamplingRateLabel => $"{Math.Round(BlurSamplingRate)}%";

    public int SelectedBlurTypeIndex
    {
        get => _selectedBlurTypeIndex;
        set => SetProperty(ref _selectedBlurTypeIndex, Math.Clamp(value, 0, BlurTypeOptions.Count - 1));
    }

    public int SelectedGlobalFontIndex
    {
        get => _selectedGlobalFontIndex;
        set => SetProperty(ref _selectedGlobalFontIndex, Math.Clamp(value, 0, FontOptions.Count - 1));
    }

    public int SelectedMotdFontIndex
    {
        get => _selectedMotdFontIndex;
        set => SetProperty(ref _selectedMotdFontIndex, Math.Clamp(value, 0, FontOptions.Count - 1));
    }

    public bool AutoPauseVideo
    {
        get => _autoPauseVideo;
        set => SetProperty(ref _autoPauseVideo, value);
    }

    public bool BackgroundColorful
    {
        get => _backgroundColorful;
        set => SetProperty(ref _backgroundColorful, value);
    }

    public double MusicVolume
    {
        get => _musicVolume;
        set
        {
            if (SetProperty(ref _musicVolume, value))
            {
                RaisePropertyChanged(nameof(MusicVolumeLabel));
            }
        }
    }

    public string MusicVolumeLabel => $"{Math.Round(MusicVolume / 10)}%";

    public bool MusicRandomPlay
    {
        get => _musicRandomPlay;
        set => SetProperty(ref _musicRandomPlay, value);
    }

    public bool MusicAutoStart
    {
        get => _musicAutoStart;
        set => SetProperty(ref _musicAutoStart, value);
    }

    public bool MusicStartOnGameLaunch
    {
        get => _musicStartOnGameLaunch;
        set => SetProperty(ref _musicStartOnGameLaunch, value);
    }

    public bool MusicStopOnGameLaunch
    {
        get => _musicStopOnGameLaunch;
        set => SetProperty(ref _musicStopOnGameLaunch, value);
    }

    public bool MusicEnableSmtc
    {
        get => _musicEnableSmtc;
        set => SetProperty(ref _musicEnableSmtc, value);
    }

    public int SelectedLogoTypeIndex
    {
        get => _selectedLogoTypeIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, 3);
            if (SetProperty(ref _selectedLogoTypeIndex, clamped))
            {
                RaisePropertyChanged(nameof(IsLogoTypeNoneSelected));
                RaisePropertyChanged(nameof(IsLogoTypeDefaultSelected));
                RaisePropertyChanged(nameof(IsLogoTypeTextSelected));
                RaisePropertyChanged(nameof(IsLogoTypeImageSelected));
                RaisePropertyChanged(nameof(IsLogoLeftVisible));
                RaisePropertyChanged(nameof(IsLogoTextVisible));
                RaisePropertyChanged(nameof(IsLogoImageActionsVisible));
            }
        }
    }

    public bool IsLogoTypeNoneSelected
    {
        get => SelectedLogoTypeIndex == 0;
        set { if (value) SelectedLogoTypeIndex = 0; }
    }

    public bool IsLogoTypeDefaultSelected
    {
        get => SelectedLogoTypeIndex == 1;
        set { if (value) SelectedLogoTypeIndex = 1; }
    }

    public bool IsLogoTypeTextSelected
    {
        get => SelectedLogoTypeIndex == 2;
        set { if (value) SelectedLogoTypeIndex = 2; }
    }

    public bool IsLogoTypeImageSelected
    {
        get => SelectedLogoTypeIndex == 3;
        set { if (value) SelectedLogoTypeIndex = 3; }
    }

    public bool IsLogoLeftVisible => SelectedLogoTypeIndex != 0;

    public bool LogoAlignLeft
    {
        get => _logoAlignLeft;
        set => SetProperty(ref _logoAlignLeft, value);
    }

    public bool IsLogoTextVisible => SelectedLogoTypeIndex == 2;

    public string LogoTextValue
    {
        get => _logoText;
        set => SetProperty(ref _logoText, value);
    }

    public bool IsLogoImageActionsVisible => SelectedLogoTypeIndex == 3;

    public int SelectedHomepageTypeIndex
    {
        get => _selectedHomepageTypeIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, 3);
            if (SetProperty(ref _selectedHomepageTypeIndex, clamped))
            {
                RaisePropertyChanged(nameof(IsHomepageBlankSelected));
                RaisePropertyChanged(nameof(IsHomepagePresetSelected));
                RaisePropertyChanged(nameof(IsHomepageLocalSelected));
                RaisePropertyChanged(nameof(IsHomepageNetSelected));
                RaisePropertyChanged(nameof(IsHomepageLocalActionsVisible));
                RaisePropertyChanged(nameof(IsHomepageNetVisible));
                RaisePropertyChanged(nameof(IsHomepagePresetVisible));
            }
        }
    }

    public bool IsHomepageBlankSelected
    {
        get => SelectedHomepageTypeIndex == 0;
        set { if (value) SelectedHomepageTypeIndex = 0; }
    }

    public bool IsHomepagePresetSelected
    {
        get => SelectedHomepageTypeIndex == 1;
        set { if (value) SelectedHomepageTypeIndex = 1; }
    }

    public bool IsHomepageLocalSelected
    {
        get => SelectedHomepageTypeIndex == 2;
        set { if (value) SelectedHomepageTypeIndex = 2; }
    }

    public bool IsHomepageNetSelected
    {
        get => SelectedHomepageTypeIndex == 3;
        set { if (value) SelectedHomepageTypeIndex = 3; }
    }

    public bool IsHomepageLocalActionsVisible => SelectedHomepageTypeIndex == 2;

    public bool IsHomepageNetVisible => SelectedHomepageTypeIndex == 3;

    public string HomepageUrl
    {
        get => _homepageUrl;
        set => SetProperty(ref _homepageUrl, value);
    }

    public bool IsHomepagePresetVisible => SelectedHomepageTypeIndex == 1;

    public int SelectedHomepagePresetIndex
    {
        get => _selectedHomepagePresetIndex;
        set => SetProperty(ref _selectedHomepagePresetIndex, Math.Clamp(value, 0, HomepagePresetOptions.Count - 1));
    }

    public bool HasJavaRuntimeEntries => JavaRuntimeEntries.Count > 0;

    public bool IsAutoJavaSelected => _selectedJavaRuntimeKey == "auto";

    public string LaunchUserName => _launchPlan.ReplacementPlan.Values.TryGetValue("${auth_player_name}", out var authPlayerName)
        ? authPlayerName
        : "DemoPlayer";

    public string LaunchAuthLabel => _launchPlan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft
        ? "正版验证"
        : "外置验证";

    public string LaunchButtonTitle => "启动游戏";

    public string LaunchVersionSubtitle => _launchPlan.ReplacementPlan.Values.TryGetValue("${version_name}", out var versionName)
        ? versionName
        : "Demo Instance";

    public string LaunchWelcomeBanner => "欢迎使用新闻主页";

    public string LaunchMigrationHeadline => "新特性与迁移";

    public string LaunchNewsTitle => "最新快照版 - 25w20a";

    public string LaunchCommunityHintPrimaryText => "你正在使用 PCL 社区版！此版本为独立开发和维护，与官方版本维护路线不同，体验有所出入。";

    public string LaunchCommunityHintSecondaryText => "若要永久隐藏此提示，请输入正确的 PCL CE 开发组织名称。";

    public bool ShowLaunchCommunityHint
    {
        get => _showLaunchCommunityHint;
        private set => SetProperty(ref _showLaunchCommunityHint, value);
    }

    public bool ShowLaunchLog => false;

    public string LaunchLogText => "正在等待启动日志输出。";

    public bool IsLaunchMigrationExpanded
    {
        get => _isLaunchMigrationExpanded;
        private set => SetProperty(ref _isLaunchMigrationExpanded, value);
    }

    public bool IsLaunchNewsExpanded
    {
        get => _isLaunchNewsExpanded;
        private set => SetProperty(ref _isLaunchNewsExpanded, value);
    }

    public IReadOnlyList<string> LaunchMigrationLines =>
    [
        "新的主页内容区会优先展示信息卡片，并逐步替换旧的调试式布局。",
        "后续将继续接入原始主页渲染路径，而不是在 MainWindow 里手写所有内容。"
    ];

    public Bitmap? LaunchAvatarImage => File.Exists(LaunchAvatarImageFilePath)
        ? new Bitmap(LaunchAvatarImageFilePath)
        : null;

    public Bitmap? LaunchNewsImage => File.Exists(LaunchNewsImageFilePath)
        ? new Bitmap(LaunchNewsImageFilePath)
        : null;

    public bool CanGoBack
    {
        get => _canGoBack;
        private set
        {
            if (SetProperty(ref _canGoBack, value))
            {
                _backCommand.NotifyCanExecuteChanged();
                RaisePropertyChanged(nameof(ShowTopLevelNavigation));
                RaisePropertyChanged(nameof(ShowInnerNavigation));
            }
        }
    }

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

    public ActionCommand DownloadOptionalUpdateCommand => _downloadOptionalUpdateCommand;

    public ActionCommand ShowOptionalUpdateDetailCommand => _showOptionalUpdateDetailCommand;

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

    public static FrontendShellViewModel CreateBootstrap(SpikeCommandOptions options)
    {
        return new FrontendShellViewModel(options);
    }

    private Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>> BuildPromptCatalog(string scenario)
    {
        var startupPrompts = _startupPlan.StartupPlan.EnvironmentWarningPrompt is null
            ? LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent)
            : LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent);

        var launchPrompts = LauncherFrontendPromptService.BuildLaunchPromptQueue(
            BuildLaunchPrecheckResult(scenario),
            MinecraftLaunchShellService.GetSupportPrompt(10),
            _launchPlan.JavaWorkflow.MissingJavaPrompt);
        var crashPrompts = LauncherFrontendPromptService.BuildCrashPromptQueue(_crashPlan.OutputPrompt);

        return new Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>>
        {
            [SpikePromptLaneKind.Startup] = startupPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Startup, prompt)).ToList(),
            [SpikePromptLaneKind.Launch] = launchPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Launch, prompt)).ToList(),
            [SpikePromptLaneKind.Crash] = crashPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Crash, prompt)).ToList()
        };
    }

    private void InitializePromptLanes()
    {
        PromptLanes.Clear();
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Startup,
            "启动前",
            "许可、环境与首次启动提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Startup))));
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Launch,
            "启动中",
            "启动前检查、赞助与 Java 下载提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Launch))));
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Crash,
            "崩溃恢复",
            "崩溃输出与导出恢复提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Crash))));

        SyncPromptLaneState();
        SelectPromptLane(_selectedPromptLane);
    }

    private void RefreshShell(string activityMessage)
    {
        var shellPlan = BuildShellPlan();
        var pageContent = BuildPageContent(shellPlan);
        _currentNavigation = shellPlan.Navigation;

        Eyebrow = pageContent.Eyebrow;
        Title = shellPlan.Navigation.CurrentPage.Title;
        Description = pageContent.Summary;
        Status = $"Immediate command: {shellPlan.StartupPlan.ImmediateCommand.Kind} | Splash: {(shellPlan.StartupPlan.Visual.ShouldShowSplashScreen ? "on" : "off")} | Backstack depth: {_routeHistory.Count}";
        BreadcrumbTrail = string.Join(" / ", shellPlan.Navigation.Breadcrumbs.Select(crumb => crumb.Title));
        SurfaceMeta = $"{shellPlan.Navigation.CurrentPage.Kind} surface • {(shellPlan.Navigation.CurrentPage.SidebarGroupTitle ?? "No sidebar group")} • {(shellPlan.Navigation.ShowsBackButton ? shellPlan.Navigation.BackTarget?.Label ?? "Back available" : "Top-level route")}";
        CanGoBack = shellPlan.Navigation.ShowsBackButton;

        ReplaceItems(TopLevelEntries, shellPlan.Navigation.TopLevelEntries.Select(entry => CreateNavigationEntry(entry, NavigationVisualStyle.TopLevel)));
        ReplaceItems(SidebarEntries, shellPlan.Navigation.SidebarEntries.Select(entry => CreateNavigationEntry(entry, NavigationVisualStyle.Sidebar)));
        ReplaceItems(SidebarSections, BuildSidebarSections(shellPlan.Navigation));
        ReplaceItems(UtilityEntries, shellPlan.Navigation.UtilityEntries.Where(entry => entry.IsVisible).Select(CreateUtilityEntry));
        RefreshDownloadCatalogSurface();
        RefreshDownloadFavoriteSurface();
        ReplaceItems(SurfaceFacts, pageContent.Facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
        ReplaceItems(SurfaceSections, pageContent.Sections.Select((section, index) => CreateSurfaceSection(section, index)));
        RaiseCollectionStateProperties();

        SelectPromptLane(_selectedPromptLane, updateActivity: false);
        AddActivity(activityMessage, $"{shellPlan.Navigation.CurrentPage.Title} • {shellPlan.Navigation.CurrentPage.Route.Page}/{shellPlan.Navigation.CurrentPage.Route.Subpage}");
        RaiseShellStateProperties();
    }

    private NavigationEntryViewModel CreateNavigationEntry(LauncherFrontendNavigationEntry entry, NavigationVisualStyle style)
    {
        var (iconPath, iconScale) = GetNavigationIcon(entry.Title);
        return new NavigationEntryViewModel(
            entry.Title,
            entry.Summary,
            style == NavigationVisualStyle.Sidebar ? entry.Route.Subpage.ToString() : entry.Route.Page.ToString(),
            entry.IsSelected,
            iconPath,
            iconScale,
            GetNavigationPalette(entry.IsSelected, style),
            new ActionCommand(() => NavigateTo(entry.Route, $"Navigated to {entry.Title} from the {(style == NavigationVisualStyle.Sidebar ? "sidebar" : "top bar")}."))
        );
    }

    private NavigationEntryViewModel CreateUtilityEntry(LauncherFrontendUtilityEntry entry)
    {
        var meta = entry.Id switch
        {
            "back" => "返",
            "task-manager" => "任",
            "game-log" => "志",
            _ => entry.Route.Page.ToString()
        };

        return new NavigationEntryViewModel(
            entry.Title,
            entry.IsSelected ? "Utility surface is active in the shell." : "Pinned shell utility surface.",
            meta,
            entry.IsSelected,
            GetUtilityIcon(entry.Id),
            1.0,
            GetNavigationPalette(entry.IsSelected, NavigationVisualStyle.Utility),
            new ActionCommand(() => NavigateTo(entry.Route, $"Opened utility surface {entry.Title}.")));
    }

    private IEnumerable<SidebarSectionViewModel> BuildSidebarSections(LauncherFrontendNavigationView navigation)
    {
        if (navigation.SidebarEntries.Count == 0)
        {
            return [];
        }

        return navigation.SidebarEntries
            .GroupBy(entry => GetSidebarSectionTitle(navigation.CurrentRoute.Page, entry.Route.Subpage))
            .Select(group => new SidebarSectionViewModel(
                group.Key,
                string.IsNullOrWhiteSpace(group.Key)
                    ? false
                    : true,
                group.Select(entry =>
                {
                    var (iconPath, iconScale) = GetSidebarIcon(entry.Route.Page, entry.Route.Subpage, entry.Title);
                    var accessory = GetSidebarAccessory(entry.Route.Page, entry.Route.Subpage, entry.Title);
                    return new SidebarListItemViewModel(
                        entry.Title,
                        entry.Summary,
                        entry.IsSelected,
                        iconPath,
                        iconScale,
                        new ActionCommand(() => NavigateTo(entry.Route, $"Navigated to {entry.Title} from the launcher-style left pane.")),
                        accessory.ToolTip,
                        accessory.IconPath,
                        accessory.Command is null
                            ? null
                            : new ActionCommand(() => ApplySidebarAccessory(entry.Title, accessory.ActionLabel, accessory.Command)));
                }).ToArray()))
            .ToArray();
    }

    private LauncherFrontendShellPlan BuildShellPlan()
    {
        var request = _shellInputs.NavigationRequest with
        {
            CurrentRoute = _currentRoute,
            BackstackDepth = _routeHistory.Count
        };
        return LauncherFrontendShellService.BuildPlan(new LauncherFrontendShellRequest(
            _shellInputs.StartupInputs.StartupWorkflowRequest,
            _shellInputs.StartupInputs.StartupConsentRequest,
            request));
    }

    private void NavigateTo(LauncherFrontendRoute route, string activityMessage)
    {
        if (route == _currentRoute)
        {
            AddActivity("Stayed on the current route.", $"{route.Page}/{route.Subpage}");
            return;
        }

        _routeHistory.Add(_currentRoute);
        _currentRoute = route;
        RefreshShell(activityMessage);
    }

    private void NavigateBack()
    {
        if (_currentNavigation is null)
        {
            return;
        }

        if (_routeHistory.Count > 0)
        {
            var previousRoute = _routeHistory[^1];
            _routeHistory.RemoveAt(_routeHistory.Count - 1);
            _currentRoute = previousRoute;
            RefreshShell("Returned to the previous shell route.");
            return;
        }

        if (_currentNavigation.BackTarget?.Route is { } backRoute)
        {
            _currentRoute = backRoute;
            RefreshShell($"Followed shell back target to {backRoute.Page}.");
        }
    }

    private void SelectPromptLane(SpikePromptLaneKind lane, bool updateActivity = true)
    {
        _selectedPromptLane = lane;
        SyncPromptLaneState();
        ReplaceItems(ActivePrompts, _promptCatalog[lane]);
        RaisePropertyChanged(nameof(HasActivePrompts));
        RaisePropertyChanged(nameof(HasNoActivePrompts));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));

        var selectedLane = PromptLanes.First(item => item.Kind == lane);
        PromptInboxTitle = $"{selectedLane.Title}提示";
        PromptInboxSummary = selectedLane.Summary;
        PromptEmptyState = $"当前没有待处理的{selectedLane.Title}提示。";
        var pageContent = BuildPageContent(BuildShellPlan());
        ReplaceItems(SurfaceFacts, pageContent.Facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
        ReplaceItems(SurfaceSections, pageContent.Sections.Select((section, index) => CreateSurfaceSection(section, index)));
        RaiseCollectionStateProperties();

        if (updateActivity)
        {
            AddActivity("Switched prompt lane.", $"{selectedLane.Title} now has {selectedLane.Count} queued prompt(s).");
        }
    }

    private void SyncPromptLaneState()
    {
        foreach (var lane in PromptLanes)
        {
            lane.Count = _promptCatalog[lane.Kind].Count;
            lane.IsSelected = lane.Kind == _selectedPromptLane;
        }
    }

    private PromptCardViewModel CreatePromptCard(SpikePromptLaneKind lane, LauncherFrontendPrompt prompt)
    {
        return new PromptCardViewModel(
            lane,
            prompt.Id,
            prompt.Title,
            prompt.Message,
            prompt.Source.ToString(),
            prompt.Severity.ToString(),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning ? Brush.Parse("#A94F2B") : Brush.Parse("#256A61"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning ? Brush.Parse("#FFF1EA") : Brush.Parse("#EAF7F5"),
            prompt.Options.Select(option => new PromptOptionViewModel(
                option.Label,
                DescribePromptOption(option),
                new ActionCommand(() => ApplyPromptOption(lane, prompt.Id, option)))).ToList());
    }

    private void ApplyPromptOption(SpikePromptLaneKind lane, string promptId, LauncherFrontendPromptOption option)
    {
        var commandSummary = option.Commands.Count == 0
            ? "No commands attached."
            : string.Join(" • ", option.Commands.Select(DescribePromptCommand));
        AddActivity($"Prompt action: {option.Label}", commandSummary);

        foreach (var command in option.Commands)
        {
            ExecutePromptCommand(command);
        }

        if (option.ClosesPrompt)
        {
            _promptCatalog[lane].RemoveAll(prompt => prompt.Id == promptId);
            SyncPromptLaneState();
            SelectPromptLane(_selectedPromptLane, updateActivity: false);
            if (!HasActivePrompts)
            {
                SetPromptOverlayOpen(false);
            }
            AddActivity("Prompt closed.", $"{promptId} was dismissed from the {lane} lane.");
        }
    }

    private void ExecutePromptCommand(LauncherFrontendPromptCommand command)
    {
        switch (command.Kind)
        {
            case LauncherFrontendPromptCommandKind.ViewGameLog:
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog), "Prompt routed the shell to the live game log surface.");
                break;
            case LauncherFrontendPromptCommandKind.OpenInstanceSettings:
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup), "Prompt routed the shell to instance settings.");
                break;
            case LauncherFrontendPromptCommandKind.ExportCrashReport:
                AddActivity("Crash export intent issued.", _crashPlan.ExportPlan.SuggestedArchiveName);
                break;
            case LauncherFrontendPromptCommandKind.DownloadJavaRuntime:
                AddActivity("Java download intent issued.", command.Value ?? _launchPlan.JavaWorkflow.MissingJavaPrompt.DownloadTarget ?? "No download target");
                break;
            case LauncherFrontendPromptCommandKind.OpenUrl:
                AddActivity("External URL intent issued.", command.Value ?? "No URL supplied");
                break;
            case LauncherFrontendPromptCommandKind.AppendLaunchArgument:
                AddActivity("Launch argument intent issued.", command.Value ?? "No argument supplied");
                break;
            case LauncherFrontendPromptCommandKind.SetTelemetryEnabled:
            case LauncherFrontendPromptCommandKind.AcceptConsent:
            case LauncherFrontendPromptCommandKind.RejectConsent:
            case LauncherFrontendPromptCommandKind.ContinueFlow:
            case LauncherFrontendPromptCommandKind.AbortLaunch:
            case LauncherFrontendPromptCommandKind.PersistSetting:
            case LauncherFrontendPromptCommandKind.ClosePrompt:
            case LauncherFrontendPromptCommandKind.ExitLauncher:
                AddActivity("Shell intent recorded.", DescribePromptCommand(command));
                break;
            default:
                AddActivity("Unhandled prompt command encountered.", command.Kind.ToString());
                break;
        }
    }

    private void AddActivity(string title, string body)
    {
        ActivityEntries.Insert(0, new ActivityItemViewModel(DateTime.Now.ToString("HH:mm:ss"), title, body));
        while (ActivityEntries.Count > 12)
        {
            ActivityEntries.RemoveAt(ActivityEntries.Count - 1);
        }

        RaisePropertyChanged(nameof(HasActivityEntries));
    }

    private void TogglePromptOverlay()
    {
        SetPromptOverlayOpen(!IsPromptOverlayVisible);
    }

    private void ToggleLaunchMigrationCard()
    {
        IsLaunchMigrationExpanded = !IsLaunchMigrationExpanded;
    }

    private void ToggleLaunchNewsCard()
    {
        IsLaunchNewsExpanded = !IsLaunchNewsExpanded;
    }

    private void SetPromptOverlayOpen(bool isOpen)
    {
        if (_isPromptOverlayOpen == isOpen)
        {
            RaisePropertyChanged(nameof(IsPromptOverlayVisible));
            return;
        }

        _isPromptOverlayOpen = isOpen;
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
    }

    private void RaiseShellStateProperties()
    {
        RaisePropertyChanged(nameof(IsLaunchRoute));
        RaisePropertyChanged(nameof(IsStandardShellRoute));
        RaisePropertyChanged(nameof(IsSetupLaunchSurface));
        RaisePropertyChanged(nameof(IsSetupAboutSurface));
        RaisePropertyChanged(nameof(IsSetupFeedbackSurface));
        RaisePropertyChanged(nameof(IsSetupLogSurface));
        RaisePropertyChanged(nameof(IsSetupUpdateSurface));
        RaisePropertyChanged(nameof(IsSetupGameLinkSurface));
        RaisePropertyChanged(nameof(IsSetupGameManageSurface));
        RaisePropertyChanged(nameof(IsSetupLauncherMiscSurface));
        RaisePropertyChanged(nameof(IsSetupJavaSurface));
        RaisePropertyChanged(nameof(IsSetupUiSurface));
        RaisePropertyChanged(nameof(IsDownloadInstallSurface));
        RaisePropertyChanged(nameof(IsDownloadCatalogSurface));
        RaisePropertyChanged(nameof(IsDownloadFavoritesSurface));
        RaisePropertyChanged(nameof(IsToolsGameLinkSurface));
        RaisePropertyChanged(nameof(IsToolsHelpSurface));
        RaisePropertyChanged(nameof(IsToolsTestSurface));
        RaisePropertyChanged(nameof(IsGenericShellSurface));
        RaisePropertyChanged(nameof(ShowTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowInnerNavigation));
        RaisePropertyChanged(nameof(TitleBarLabel));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
        RaiseUpdateSurfaceProperties();
    }

    private void RaiseCollectionStateProperties()
    {
        RaisePropertyChanged(nameof(HasSidebarEntries));
        RaisePropertyChanged(nameof(HasSidebarSections));
        RaisePropertyChanged(nameof(HasNoSidebarSections));
        RaisePropertyChanged(nameof(HasSurfaceFacts));
        RaisePropertyChanged(nameof(HasSurfaceSections));
        RaisePropertyChanged(nameof(HasUtilityEntries));
        RaisePropertyChanged(nameof(HasActivityEntries));
        RaisePropertyChanged(nameof(HasAboutProjectEntries));
        RaisePropertyChanged(nameof(HasAboutAcknowledgementEntries));
        RaisePropertyChanged(nameof(HasFeedbackSections));
        RaisePropertyChanged(nameof(HasDownloadFavoriteSections));
        RaisePropertyChanged(nameof(HasNoDownloadFavoriteSections));
        RaisePropertyChanged(nameof(HasHelpTopicGroups));
        RaisePropertyChanged(nameof(HasNoHelpTopicGroups));
        RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
    }

    private void RaiseUpdateSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowAvailableUpdateCard));
        RaisePropertyChanged(nameof(ShowCurrentVersionCard));
        RaisePropertyChanged(nameof(ShowOptionalUpdateCard));
        RaisePropertyChanged(nameof(CurrentVersionDescription));
    }

    private static string DescribePromptOption(LauncherFrontendPromptOption option)
    {
        return option.Commands.Count == 0
            ? "No shell commands."
            : string.Join(", ", option.Commands.Select(DescribePromptCommand));
    }

    private static string DescribePromptCommand(LauncherFrontendPromptCommand command)
    {
        return command.Kind switch
        {
            LauncherFrontendPromptCommandKind.ContinueFlow => "Continue flow",
            LauncherFrontendPromptCommandKind.AcceptConsent => "Accept consent",
            LauncherFrontendPromptCommandKind.RejectConsent => "Reject consent",
            LauncherFrontendPromptCommandKind.OpenUrl => $"Open URL ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.ExitLauncher => "Exit launcher",
            LauncherFrontendPromptCommandKind.SetTelemetryEnabled => $"Set telemetry = {command.Value ?? "n/a"}",
            LauncherFrontendPromptCommandKind.AbortLaunch => "Abort launch",
            LauncherFrontendPromptCommandKind.AppendLaunchArgument => $"Append launch arg ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.PersistSetting => $"Persist setting ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.DownloadJavaRuntime => $"Download Java ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.ClosePrompt => "Close prompt",
            LauncherFrontendPromptCommandKind.ViewGameLog => "Open game log",
            LauncherFrontendPromptCommandKind.OpenInstanceSettings => "Open instance settings",
            LauncherFrontendPromptCommandKind.ExportCrashReport => "Export crash report",
            _ => command.Kind.ToString()
        };
    }

    private LauncherFrontendPageContent BuildPageContent(LauncherFrontendShellPlan shellPlan)
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            shellPlan.Navigation,
            shellPlan.StartupPlan,
            shellPlan.Consent,
            BuildPromptLaneSummaries(),
            BuildLaunchSurfaceData(),
            BuildCrashSurfaceData()));

        if (shellPlan.Navigation.CurrentPage.Route.Page != LauncherFrontendPageKey.Launch)
        {
            return content;
        }

        return content with
        {
            Eyebrow = "启动主页",
            Summary = "基于原始启动页结构重建的 Avalonia 主窗口原型。",
            Facts =
            [
                new LauncherFrontendPageFact("账号", LaunchUserName),
                new LauncherFrontendPageFact("验证方式", LaunchAuthLabel),
                new LauncherFrontendPageFact("版本", LaunchVersionSubtitle),
                new LauncherFrontendPageFact("主页", "新闻主页")
            ],
            Sections =
            [
                new LauncherFrontendPageSection(
                    "快照版",
                    "25w20a",
                    [
                        "增加了由 Amos Roddy 创作的新音乐唱片《Tears》。",
                        "鞍具现在可以合成，并且能够用剪刀拆下。",
                        "刷怪蛋与部分实体的视觉表现获得了进一步统一。"
                    ]),
                new LauncherFrontendPageSection(
                    "迁移",
                    "新版主页结构",
                    [
                        "顶部入口、启动区和右侧内容区按原始比例重新收紧。",
                        "卡片标题、箭头、阴影和留白改回接近 PCL 的层级关系。"
                    ])
            ]
        };
    }

    private LauncherFrontendPromptLaneSummary[] BuildPromptLaneSummaries()
    {
        return
        [
            new LauncherFrontendPromptLaneSummary(
                "startup",
                "启动前",
                "许可、环境与首次启动提示。",
                _promptCatalog[SpikePromptLaneKind.Startup].Count,
                _selectedPromptLane == SpikePromptLaneKind.Startup),
            new LauncherFrontendPromptLaneSummary(
                "launch",
                "启动中",
                "启动前检查、赞助与 Java 下载提示。",
                _promptCatalog[SpikePromptLaneKind.Launch].Count,
                _selectedPromptLane == SpikePromptLaneKind.Launch),
            new LauncherFrontendPromptLaneSummary(
                "crash",
                "崩溃恢复",
                "崩溃输出与导出恢复提示。",
                _promptCatalog[SpikePromptLaneKind.Crash].Count,
                _selectedPromptLane == SpikePromptLaneKind.Crash)
        ];
    }

    private LauncherFrontendLaunchSurfaceData BuildLaunchSurfaceData()
    {
        var playerName = _launchPlan.ReplacementPlan.Values.TryGetValue("${auth_player_name}", out var authPlayerName)
            ? authPlayerName
            : "Unknown player";
        var provider = _launchPlan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft
            ? "Microsoft account"
            : "Authlib account";

        return new LauncherFrontendLaunchSurfaceData(
            _launchPlan.Scenario,
            provider,
            playerName,
            _launchPlan.LoginPlan.Steps.Count,
            _launchPlan.JavaWorkflow.RecommendedComponent is null
                ? $"Java {_launchPlan.JavaWorkflow.RecommendedMajorVersion}"
                : $"{_launchPlan.JavaWorkflow.RecommendedComponent} (Java {_launchPlan.JavaWorkflow.RecommendedMajorVersion})",
            _launchPlan.JavaWorkflow.MissingJavaPrompt.DownloadTarget,
            $"{_launchPlan.ResolutionPlan.Width} x {_launchPlan.ResolutionPlan.Height}",
            _launchPlan.ClasspathPlan.Entries.Count,
            _launchPlan.ReplacementPlan.Values.Count,
            _launchPlan.NativesDirectory,
            _launchPlan.PrerunPlan.Options.TargetFilePath,
            _launchPlan.PrerunPlan.LauncherProfiles.Workflow.ShouldWrite,
            _launchPlan.ScriptExportPlan is not null,
            _launchPlan.ScriptExportPlan?.TargetPath,
            _launchPlan.CompletionNotification.Message);
    }

    private LauncherFrontendCrashSurfaceData BuildCrashSurfaceData()
    {
        return new LauncherFrontendCrashSurfaceData(
            _crashPlan.ExportPlan.SuggestedArchiveName,
            _crashPlan.ExportPlan.ExportRequest.SourceFiles.Count,
            !string.IsNullOrWhiteSpace(_crashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath),
            _crashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath);
    }

}
