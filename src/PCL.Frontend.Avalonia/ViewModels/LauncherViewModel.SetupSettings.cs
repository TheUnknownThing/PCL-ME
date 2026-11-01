using System.Globalization;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    public IReadOnlyList<string> DownloadSourceOptions => SetupText.GameManage.DownloadSourceOptions;

    public IReadOnlyList<string> FileNameFormatOptions => SetupText.GameManage.FileNameFormatOptions;

    public IReadOnlyList<string> ModLocalNameStyleOptions => SetupText.GameManage.ModLocalNameStyleOptions;

    public int SelectedDownloadSourceIndex
    {
        get => _selectedDownloadSourceIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, DownloadSourceOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedDownloadSourceIndex, normalizedValue);
            }
        }
    }

    public int SelectedVersionSourceIndex
    {
        get => _selectedVersionSourceIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, DownloadSourceOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedVersionSourceIndex, normalizedValue);
            }
        }
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

    public string DownloadSpeedLimitLabel => FrontendDownloadSettingsService.FormatSpeedLimitLabel(
        DownloadSpeedLimit,
        _i18n.T("setup.game_manage.labels.download_speed_unlimited"));

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

    public string DownloadTimeoutLabel => _i18n.T(
        "setup.game_manage.labels.download_timeout_value",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["value"] = Math.Round(DownloadTimeoutSeconds)
        });

    public bool AutoSelectNewInstance
    {
        get => _autoSelectNewInstance;
        set => SetProperty(ref _autoSelectNewInstance, value);
    }

    public int SelectedCommunityDownloadSourceIndex
    {
        get => _selectedCommunityDownloadSourceIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, DownloadSourceOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedCommunityDownloadSourceIndex, normalizedValue);
            }
        }
    }

    public int SelectedFileNameFormatIndex
    {
        get => _selectedFileNameFormatIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, FileNameFormatOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedFileNameFormatIndex, normalizedValue);
            }
        }
    }

    public int SelectedModLocalNameStyleIndex
    {
        get => _selectedModLocalNameStyleIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, ModLocalNameStyleOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedModLocalNameStyleIndex, normalizedValue);
            }
        }
    }

    public bool IgnoreQuiltLoader
    {
        get => _ignoreQuiltLoader;
        set => SetProperty(ref _ignoreQuiltLoader, value);
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

    public IReadOnlyList<string> LauncherLocaleOptions => _launcherLocaleOptions;

    public int SelectedLauncherLocaleIndex
    {
        get => _selectedLauncherLocaleIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, LauncherLocaleOptions.Count, out var clamped))
            {
                return;
            }

            if (!SetProperty(ref _selectedLauncherLocaleIndex, clamped))
            {
                return;
            }

            if (clamped >= 0 &&
                clamped < _launcherLocaleKeys.Count &&
                !string.Equals(_i18n.Locale, _launcherLocaleKeys[clamped], StringComparison.Ordinal))
            {
                _i18n.SetLocale(_launcherLocaleKeys[clamped]);
            }
        }
    }

    public IReadOnlyList<string> SystemActivityOptions => SetupText.LauncherMisc.SystemActivityOptions;

    public int SelectedSystemActivityIndex
    {
        get => _selectedSystemActivityIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, SystemActivityOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedSystemActivityIndex, normalizedValue);
            }
        }
    }

    public double MaxRealTimeLogValue
    {
        get => _maxRealTimeLogValue;
        set
        {
            if (SetProperty(ref _maxRealTimeLogValue, value))
            {
                ApplyLaunchLogRetentionPreference();
                RaisePropertyChanged(nameof(MaxRealTimeLogLabel));
            }
        }
    }

    public string MaxRealTimeLogLabel => FrontendRealTimeLogSettingsService.FormatLineLimitLabel(MaxRealTimeLogValue);

    public bool IsHardwareAccelerationToggleVisible => _setupComposition.LauncherMisc.IsHardwareAccelerationToggleAvailable;

    public bool DisableHardwareAcceleration
    {
        get => _disableHardwareAcceleration;
        set => SetProperty(ref _disableHardwareAcceleration, value);
    }

    public IReadOnlyList<string> SecureDnsModeOptions => SetupText.LauncherMisc.SecureDnsModeOptions;

    public IReadOnlyList<string> SecureDnsProviderOptions => SetupText.LauncherMisc.SecureDnsProviderOptions;

    public int SelectedSecureDnsModeIndex
    {
        get => _selectedSecureDnsModeIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, SecureDnsModeOptions.Count, out var clamped))
            {
                return;
            }

            if (SetProperty(ref _selectedSecureDnsModeIndex, clamped))
            {
                RaisePropertyChanged(nameof(IsSecureDnsProviderSelectionEnabled));
            }
        }
    }

    public int SelectedSecureDnsProviderIndex
    {
        get => _selectedSecureDnsProviderIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, SecureDnsProviderOptions.Count, out var clamped))
            {
                SetProperty(ref _selectedSecureDnsProviderIndex, clamped);
            }
        }
    }

    public bool IsSecureDnsProviderSelectionEnabled => SelectedSecureDnsModeIndex != (int)FrontendSecureDnsMode.System;

    public int SelectedHttpProxyTypeIndex
    {
        get => _selectedHttpProxyTypeIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, 3, out var clamped))
            {
                return;
            }

            if (SetProperty(ref _selectedHttpProxyTypeIndex, clamped))
            {
                RaisePropertyChanged(nameof(IsCustomHttpProxyEnabled));
                RaisePropertyChanged(nameof(IsNoHttpProxySelected));
                RaisePropertyChanged(nameof(IsSystemHttpProxySelected));
                RaisePropertyChanged(nameof(IsCustomHttpProxySelected));
                ClearProxyTestFeedback();
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
        set
        {
            if (SetProperty(ref _httpProxyAddress, value))
            {
                ClearProxyTestFeedback();
            }
        }
    }

    public string HttpProxyUsername
    {
        get => _httpProxyUsername;
        set
        {
            if (SetProperty(ref _httpProxyUsername, value))
            {
                ClearProxyTestFeedback();
            }
        }
    }

    public string HttpProxyPassword
    {
        get => _httpProxyPassword;
        set
        {
            if (SetProperty(ref _httpProxyPassword, value))
            {
                ClearProxyTestFeedback();
            }
        }
    }

    public string ProxyTestFeedbackText
    {
        get => _proxyTestFeedbackText;
        private set
        {
            if (SetProperty(ref _proxyTestFeedbackText, value))
            {
                RaisePropertyChanged(nameof(IsProxyTestFeedbackVisible));
                RaisePropertyChanged(nameof(IsProxyTestSuccessVisible));
                RaisePropertyChanged(nameof(IsProxyTestFailureVisible));
            }
        }
    }

    public bool IsProxyTestFeedbackVisible => !string.IsNullOrWhiteSpace(ProxyTestFeedbackText);

    public bool IsProxyTestSuccessVisible => IsProxyTestFeedbackVisible && _isProxyTestFeedbackSuccess;

    public bool IsProxyTestFailureVisible => IsProxyTestFeedbackVisible && !_isProxyTestFeedbackSuccess;

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
        ? _i18n.T("setup.launcher_misc.labels.debug_animation_speed_off")
        : $"{Math.Round(DebugAnimationSpeed / 10 + 0.1, 1):0.0}x";

    public bool DebugModeEnabled
    {
        get => _debugModeEnabled;
        set => SetProperty(ref _debugModeEnabled, value);
    }

    private int ResolveLauncherLocaleIndex(string locale)
    {
        for (var i = 0; i < _launcherLocaleKeys.Count; i++)
        {
            if (string.Equals(_launcherLocaleKeys[i], locale, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    private static string FormatLauncherLocaleOption(string locale)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);
            var nativeName = culture.NativeName;
            return string.IsNullOrWhiteSpace(nativeName)
                ? locale
                : $"{nativeName} ({locale})";
        }
        catch (CultureNotFoundException)
        {
            return locale;
        }
    }
}
