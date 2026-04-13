using System.IO;
using Avalonia.Media.Imaging;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
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
                if (IsSetupUpdateSurface)
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

    public bool ShowAvailableUpdateCard => _updateStatus.SurfaceState == UpdateSurfaceState.Available;

    public bool ShowCurrentVersionCard => _updateStatus.SurfaceState != UpdateSurfaceState.Available;

    public bool ShowOptionalUpdateCard => false;

    public string AvailableUpdateName => _updateStatus.AvailableUpdateName;

    public string AvailableUpdatePublisher => _updateStatus.AvailableUpdatePublisher;

    public string AvailableUpdateSummary => _updateStatus.AvailableUpdateSummary;

    public string CurrentVersionName => _updateStatus.CurrentVersionName;

    public string CurrentVersionDescription => _updateStatus.CurrentVersionDescription;

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

    public string LaunchUserName => _launchComposition.SelectedProfile.UserName;

    public string LaunchAuthLabel => _launchComposition.SelectedProfile.AuthLabel;

    public string LaunchButtonTitle => "启动游戏";

    public string LaunchVersionSubtitle => _launchComposition.InstanceName;

    public string LaunchWelcomeBanner => $"当前实例：{LaunchVersionSubtitle}";

    public string LaunchMigrationHeadline => "启动状态";

    public string LaunchNewsTitle => $"启动概览 - {LaunchVersionSubtitle}";

    public string LaunchNewsBadgeText => LaunchVersionSubtitle;

    public string LaunchNewsSectionTitle => "启动状态";

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
        $"档案：{_launchComposition.SelectedProfile.IdentityLabel}",
        $"Java：{GetLaunchJavaRuntimeLabel()}",
        $"预检查：{(_launchComposition.PrecheckResult.IsSuccess ? "已通过" : _launchComposition.PrecheckResult.FailureMessage ?? "未通过")}",
        $"启动提示：{_launchComposition.PrecheckResult.Prompts.Count} 项预检，支持提示 {(_launchComposition.SupportPrompt is null ? "未命中" : "已命中")}"
    ];

    public Bitmap? LaunchAvatarImage => File.Exists(LaunchAvatarImageFilePath)
        ? new Bitmap(LaunchAvatarImageFilePath)
        : null;

    public Bitmap? LaunchNewsImage => File.Exists(LaunchNewsImageFilePath)
        ? new Bitmap(LaunchNewsImageFilePath)
        : null;
}
