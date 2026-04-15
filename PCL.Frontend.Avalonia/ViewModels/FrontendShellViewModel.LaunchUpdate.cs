using System.IO;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public string MemorySummaryUsageHeaderText => T("common.memory.summary.usage_header");

    public string MemorySummaryAllocationPrefixText => T("common.memory.summary.allocation_prefix");

    public IReadOnlyList<string> LaunchIsolationOptions => SetupText.Launch.IsolationOptions;

    public IReadOnlyList<string> LaunchVisibilityOptions => SetupText.Launch.VisibilityOptions;

    public IReadOnlyList<string> LaunchPriorityOptions => SetupText.Launch.PriorityOptions;

    public IReadOnlyList<string> LaunchWindowTypeOptions => SetupText.Launch.WindowTypeOptions;

    public IReadOnlyList<string> LaunchMicrosoftAuthOptions => SetupText.Launch.MicrosoftAuthOptions;

    public IReadOnlyList<string> LaunchPreferredIpStackOptions => SetupText.Launch.PreferredIpStackOptions;

    public IReadOnlyList<string> LaunchRendererOptions => SetupText.Launch.RendererOptions;

    public IReadOnlyList<string> UpdateChannelOptions => SetupText.Update.ChannelOptions;

    public IReadOnlyList<string> UpdateModeOptions => SetupText.Update.ModeOptions;

    public int SelectedUpdateChannelIndex
    {
        get => _selectedUpdateChannelIndex;
        set
        {
            var clampedValue = Math.Clamp(value, 0, UpdateChannelOptions.Count - 1);
            if (SetProperty(ref _selectedUpdateChannelIndex, clampedValue))
            {
                AddActivity(_i18n.T("setup.update.activities.change_channel"), UpdateChannelOptions[clampedValue]);
                if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupUpdate))
                {
                    _ = CheckForLauncherUpdatesAsync(forceRefresh: true);
                }
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
                AddActivity(_i18n.T("setup.update.activities.change_mode"), UpdateModeOptions[clampedValue]);
            }
        }
    }

    public Bitmap? UpdateAvailableIcon => File.Exists(UpdateAvailableIconFilePath)
        ? new Bitmap(UpdateAvailableIconFilePath)
        : null;

    public Bitmap? UpdateCurrentIcon => File.Exists(UpdateCurrentIconFilePath)
        ? new Bitmap(UpdateCurrentIconFilePath)
        : null;

    public bool ShowAvailableUpdateCard => _updateStatus.SurfaceState == UpdateSurfaceState.Available;

    public bool ShowCurrentVersionCard => _updateStatus.SurfaceState != UpdateSurfaceState.Available;

    public string AvailableUpdateName => _updateStatus.AvailableUpdateName;

    public string AvailableUpdatePublisher => _updateStatus.AvailableUpdatePublisher;

    public string AvailableUpdateSummary => _updateStatus.AvailableUpdateSummary;

    public string CurrentVersionName => _updateStatus.CurrentVersionName;

    public string CurrentVersionDescription => _updateStatus.CurrentVersionDescription;

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
                RaisePropertyChanged(nameof(AllocatedRamBarWidth));
                RaisePropertyChanged(nameof(FreeRamBarWidth));
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
                RaisePropertyChanged(nameof(AllocatedRamBarWidth));
                RaisePropertyChanged(nameof(FreeRamBarWidth));
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
                RaisePropertyChanged(nameof(UsedRamBarWidth));
                RaisePropertyChanged(nameof(AllocatedRamBarWidth));
                RaisePropertyChanged(nameof(FreeRamBarWidth));
                RaisePropertyChanged(nameof(ShowRamAllocationWarning));
            }
        }
    }

    public string CustomRamAllocationLabel => FormatMemorySummarySize(Math.Round(CustomRamAllocation));

    public string UsedRamLabel => FormatMemorySummarySize(_launchUsedRamGb);

    public string TotalRamLabel => FormatMemorySummarySize(_launchTotalRamGb);

    public string AllocatedRamLabel => UseAutomaticRamAllocation
        ? FormatMemorySummarySize(_launchAutomaticAllocatedRamGb)
        : FormatMemorySummarySize(Math.Round(CustomRamAllocation, 1));

    public GridLength UsedRamBarWidth => CreateMemoryBarWidth(_launchUsedRamGb);

    public GridLength AllocatedRamBarWidth => CreateMemoryBarWidth(UseAutomaticRamAllocation
        ? _launchAutomaticAllocatedRamGb
        : Math.Round(CustomRamAllocation, 1));

    public GridLength FreeRamBarWidth => CreateMemoryBarWidth(Math.Max(
        _launchTotalRamGb - _launchUsedRamGb - (UseAutomaticRamAllocation ? _launchAutomaticAllocatedRamGb : Math.Round(CustomRamAllocation, 1)),
        0));

    public bool ShowRamAllocationWarning => UseCustomRamAllocation &&
                                            (_launchTotalRamGb > 0
                                                ? CustomRamAllocation / _launchTotalRamGb > 0.75
                                                : CustomRamAllocation >= 8);

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

    private string FormatMemorySummarySize(double sizeGb)
    {
        return T("common.memory.summary.size_gb", ("value", sizeGb.ToString("0.0", ResolveCurrentLocaleCulture())));
    }

    private CultureInfo ResolveCurrentLocaleCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(_i18n.Locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
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

    public string LaunchEnvironmentVariables
    {
        get => _launchEnvironmentVariables;
        set => SetProperty(ref _launchEnvironmentVariables, value);
    }

    public bool WaitForLaunchBeforeCommand
    {
        get => _waitForLaunchBeforeCommand;
        set => SetProperty(ref _waitForLaunchBeforeCommand, value);
    }

    public bool ForceX11OnWaylandForLaunch
    {
        get => _forceX11OnWaylandForLaunch;
        set => SetProperty(ref _forceX11OnWaylandForLaunch, value);
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

    public string LaunchUserName => _launchComposition.SelectedProfile.UserName;

    public string LaunchAuthLabel => _launchComposition.SelectedProfile.AuthLabel;

    public bool HasSelectedLaunchProfile => _launchComposition.SelectedProfile.Kind != MinecraftLaunchProfileKind.None;

    public bool ShowLaunchProfileSetupActions => !HasSelectedLaunchProfile;

    public string LaunchProfileHint => HasSelectedLaunchProfile
        ? T("launch.profile.hint.selected")
        : T("launch.profile.selection.hint");

    public string LaunchProfileDescription => _launchComposition.SelectedProfile.Kind switch
    {
        MinecraftLaunchProfileKind.Auth when !string.IsNullOrWhiteSpace(_launchComposition.SelectedProfile.AuthServerName)
            => T("launch.profile.kinds.authlib_with_server", ("server_name", _launchComposition.SelectedProfile.AuthServerName)),
        MinecraftLaunchProfileKind.Auth when !string.IsNullOrWhiteSpace(_launchComposition.SelectedProfile.AuthServer)
            => T("launch.profile.kinds.authlib_with_server", ("server_name", GetLaunchAuthServerDisplayName(_launchComposition.SelectedProfile.AuthServer!))),
        MinecraftLaunchProfileKind.Microsoft => T("launch.profile.kinds.microsoft"),
        MinecraftLaunchProfileKind.Legacy => T("launch.profile.kinds.offline"),
        _ => T("launch.profile.selection.hint_empty")
    };

    public string LaunchButtonTitle => _isLaunchInProgress
        ? T("launch.actions.launching")
        : T("launch.actions.launch");

    public string LaunchVersionSubtitle => GetDisplayedLaunchInstanceName();

    public string LaunchWelcomeBanner => T("launch.status.current_instance", ("instance_name", LaunchVersionSubtitle));

    public string LaunchMigrationHeadline => T("launch.status.title");

    public string LaunchNewsTitle => T("launch.status.overview_title", ("instance_name", LaunchVersionSubtitle));

    public string LaunchNewsBadgeText => LaunchVersionSubtitle;

    public string LaunchNewsSectionTitle => T("launch.status.title");

    public string LaunchAnnouncementHeader => ResolveLaunchAnnouncementTitle(_currentLaunchAnnouncement);

    public string LaunchAnnouncementPrimaryText => ResolveLaunchAnnouncementMessage(_currentLaunchAnnouncement);

    public string LaunchAnnouncementSecondaryText => ResolveLaunchAnnouncementDetail(_currentLaunchAnnouncement);

    public bool ShowLaunchAnnouncement => _currentLaunchAnnouncement is not null;

    public bool ShowLaunchLog => _showLaunchLog;

    public string LaunchLogText => _launchLogVisibleText;

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

    public IReadOnlyList<string> LaunchMigrationLines
    {
        get
        {
            var lines = new List<string>
            {
                T("launch.status.lines.profile", ("profile_label", _launchComposition.SelectedProfile.IdentityLabel)),
                T("launch.status.lines.java", ("java_label", GetLaunchJavaRuntimeLabel())),
                T("launch.status.lines.precheck", ("result", _launchComposition.PrecheckResult.IsSuccess ? T("launch.status.values.passed") : GetLaunchPrecheckFailureMessage())),
                T("launch.status.lines.prompts", ("prompt_count", _launchComposition.PrecheckResult.Prompts.Count), ("support_prompt", _launchComposition.SupportPrompt is null ? T("launch.status.values.not_matched") : T("launch.status.values.matched"))),
                T("launch.status.lines.session", ("session_state", _isLaunchInProgress ? T("launch.status.values.launching") : T("launch.status.values.idle")))
            };
            if (!string.IsNullOrWhiteSpace(_launchComposition.JavaWarningMessage))
            {
                lines.Insert(2, T("launch.status.lines.java_warning", ("message", _launchComposition.JavaWarningMessage)));
            }

            return lines;
        }
    }

    public Bitmap? LaunchAvatarImage => File.Exists(_launchAvatarImagePath)
        ? new Bitmap(_launchAvatarImagePath)
        : null;

    public Bitmap? LaunchNewsImage => File.Exists(LaunchNewsImageFilePath)
        ? new Bitmap(LaunchNewsImageFilePath)
        : null;

    private void RefreshLaunchAnnouncements()
    {
        var sharedConfig = _shellActionService.RuntimePaths.OpenSharedConfigProvider();
        var localConfig = _shellActionService.RuntimePaths.OpenLocalConfigProvider();
        _currentLaunchAnnouncement = FrontendLaunchAnnouncementService.Compose(sharedConfig, localConfig).FirstOrDefault();
        RaisePropertyChanged(nameof(LaunchAnnouncementHeader));
        RaisePropertyChanged(nameof(LaunchAnnouncementPrimaryText));
        RaisePropertyChanged(nameof(LaunchAnnouncementSecondaryText));
        RaisePropertyChanged(nameof(ShowLaunchAnnouncement));
    }

    private void DismissCurrentLaunchAnnouncement()
    {
        if (_currentLaunchAnnouncement is null)
        {
            return;
        }

        var announcement = _currentLaunchAnnouncement;
        var sharedConfig = _shellActionService.RuntimePaths.OpenSharedConfigProvider();
        var shownState = sharedConfig.Exists("SystemSystemAnnouncement")
            ? sharedConfig.Get<string>("SystemSystemAnnouncement")
            : string.Empty;
        _shellActionService.PersistSharedValue(
            "SystemSystemAnnouncement",
            FrontendLaunchAnnouncementService.MarkAnnouncementAsShown(shownState, announcement.Id));
        RefreshLaunchAnnouncements();
        AddActivity(
            T("launch.status.activities.dismiss_announcement"),
            T("launch.status.messages.announcement_dismissed", ("title", ResolveLaunchAnnouncementTitle(announcement))));
    }

    private string ResolveLaunchAnnouncementTitle(LauncherAnnouncement? announcement)
    {
        if (announcement is null)
        {
            return string.Empty;
        }

        return announcement.Id switch
        {
            "launch-community-edition-intro" => T("launch.announcements.community_edition_intro.title"),
            _ => announcement.Title
        };
    }

    private string ResolveLaunchAnnouncementMessage(LauncherAnnouncement? announcement)
    {
        if (announcement is null)
        {
            return string.Empty;
        }

        return announcement.Id switch
        {
            "launch-community-edition-intro" => T("launch.announcements.community_edition_intro.message"),
            _ => announcement.Message
        };
    }

    private string ResolveLaunchAnnouncementDetail(LauncherAnnouncement? announcement)
    {
        if (announcement is null)
        {
            return string.Empty;
        }

        return announcement.Id switch
        {
            "launch-community-edition-intro" => T("launch.announcements.community_edition_intro.detail"),
            _ => announcement.Detail ?? string.Empty
        };
    }

    private static string GetLaunchAuthServerDisplayName(string authServer)
    {
        if (Uri.TryCreate(authServer, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return authServer;
    }
}
