using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
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

    public string DownloadThreadLimitLabel => FrontendDownloadSettingsService.FormatThreadLimitLabel(DownloadThreadLimit);

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

    public string DownloadSpeedLimitLabel => FrontendDownloadSettingsService.FormatSpeedLimitLabel(DownloadSpeedLimit);

    public double DownloadTimeoutSeconds
    {
        get => _downloadTimeoutSeconds;
        set
        {
            if (SetProperty(ref _downloadTimeoutSeconds, value))
            {
                RaisePropertyChanged(nameof(DownloadTimeoutLabel));
            }
        }
    }

    public string DownloadTimeoutLabel => $"{Math.Round(DownloadTimeoutSeconds)} s";

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
}
