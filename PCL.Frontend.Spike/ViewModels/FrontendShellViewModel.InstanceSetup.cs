namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private int _selectedInstanceServerLoginRequireIndex;
    private string _instanceServerAuthServer = "https://littleskin.cn/api/yggdrasil";
    private string _instanceServerAuthRegister = "https://littleskin.cn/auth/register";
    private string _instanceServerAuthName = "LittleSkin";
    private string _instanceServerAutoJoin = "play.example.invalid:25565";
    private int _selectedInstanceJavaIndex = 1;
    private string _instanceClasspathHead = string.Empty;
    private bool _ignoreJavaCompatibilityWarning;
    private bool _disableFileValidation;
    private bool _followLauncherProxy = true;
    private bool _useDebugLog4jConfig;

    public IReadOnlyList<string> InstanceServerLoginRequireOptions { get; } =
    [
        "无限制",
        "仅正版验证",
        "仅第三方验证（Authlib-Injector）",
        "正版验证与第三方验证（Authlib-Injector）"
    ];

    public IReadOnlyList<string> InstanceJavaOptions { get; } =
    [
        "自动选择",
        "Temurin 21.0.4 64 位",
        "Zulu 17.0.12 64 位",
        "Java 8u401 64 位"
    ];

    public int SelectedInstanceServerLoginRequireIndex
    {
        get => _selectedInstanceServerLoginRequireIndex;
        set
        {
            var nextValue = Math.Clamp(value, 0, InstanceServerLoginRequireOptions.Count - 1);
            if (SetProperty(ref _selectedInstanceServerLoginRequireIndex, nextValue))
            {
                RaisePropertyChanged(nameof(ShowInstanceServerAuthFields));
            }
        }
    }

    public bool ShowInstanceServerAuthFields => SelectedInstanceServerLoginRequireIndex >= 2;

    public string InstanceServerAuthServer
    {
        get => _instanceServerAuthServer;
        set => SetProperty(ref _instanceServerAuthServer, value);
    }

    public string InstanceServerAuthRegister
    {
        get => _instanceServerAuthRegister;
        set => SetProperty(ref _instanceServerAuthRegister, value);
    }

    public string InstanceServerAuthName
    {
        get => _instanceServerAuthName;
        set => SetProperty(ref _instanceServerAuthName, value);
    }

    public string InstanceServerAutoJoin
    {
        get => _instanceServerAutoJoin;
        set => SetProperty(ref _instanceServerAutoJoin, value);
    }

    public int SelectedInstanceJavaIndex
    {
        get => _selectedInstanceJavaIndex;
        set => SetProperty(ref _selectedInstanceJavaIndex, Math.Clamp(value, 0, InstanceJavaOptions.Count - 1));
    }

    public string InstanceClasspathHead
    {
        get => _instanceClasspathHead;
        set => SetProperty(ref _instanceClasspathHead, value);
    }

    public bool IgnoreJavaCompatibilityWarning
    {
        get => _ignoreJavaCompatibilityWarning;
        set => SetProperty(ref _ignoreJavaCompatibilityWarning, value);
    }

    public bool DisableFileValidation
    {
        get => _disableFileValidation;
        set => SetProperty(ref _disableFileValidation, value);
    }

    public bool FollowLauncherProxy
    {
        get => _followLauncherProxy;
        set => SetProperty(ref _followLauncherProxy, value);
    }

    public bool UseDebugLog4jConfig
    {
        get => _useDebugLog4jConfig;
        set => SetProperty(ref _useDebugLog4jConfig, value);
    }

    private void InitializeInstanceSetupSurface()
    {
        _selectedInstanceServerLoginRequireIndex = 2;
        _instanceServerAuthServer = "https://littleskin.cn/api/yggdrasil";
        _instanceServerAuthRegister = "https://littleskin.cn/auth/register";
        _instanceServerAuthName = "LittleSkin";
        _instanceServerAutoJoin = "play.example.invalid:25565";
        _selectedInstanceJavaIndex = 1;
        _instanceClasspathHead = string.Empty;
        _ignoreJavaCompatibilityWarning = false;
        _disableFileValidation = false;
        _followLauncherProxy = true;
        _useDebugLog4jConfig = false;
    }

    private void RefreshInstanceSetupSurface()
    {
        if (!IsInstanceSetupSurface)
        {
            return;
        }

        RaisePropertyChanged(nameof(SelectedLaunchIsolationIndex));
        RaisePropertyChanged(nameof(LaunchWindowTitleSetting));
        RaisePropertyChanged(nameof(LaunchCustomInfoSetting));
        RaisePropertyChanged(nameof(SelectedInstanceJavaIndex));
        RaisePropertyChanged(nameof(UseAutomaticRamAllocation));
        RaisePropertyChanged(nameof(UseCustomRamAllocation));
        RaisePropertyChanged(nameof(CustomRamAllocation));
        RaisePropertyChanged(nameof(CustomRamAllocationLabel));
        RaisePropertyChanged(nameof(AllocatedRamLabel));
        RaisePropertyChanged(nameof(ShowRamAllocationWarning));
        RaisePropertyChanged(nameof(OptimizeMemoryBeforeLaunch));
        RaisePropertyChanged(nameof(SelectedInstanceServerLoginRequireIndex));
        RaisePropertyChanged(nameof(ShowInstanceServerAuthFields));
        RaisePropertyChanged(nameof(InstanceServerAuthServer));
        RaisePropertyChanged(nameof(InstanceServerAuthRegister));
        RaisePropertyChanged(nameof(InstanceServerAuthName));
        RaisePropertyChanged(nameof(InstanceServerAutoJoin));
        RaisePropertyChanged(nameof(SelectedLaunchRendererIndex));
        RaisePropertyChanged(nameof(LaunchJvmArguments));
        RaisePropertyChanged(nameof(LaunchGameArguments));
        RaisePropertyChanged(nameof(InstanceClasspathHead));
        RaisePropertyChanged(nameof(LaunchBeforeCommand));
        RaisePropertyChanged(nameof(WaitForLaunchBeforeCommand));
        RaisePropertyChanged(nameof(IgnoreJavaCompatibilityWarning));
        RaisePropertyChanged(nameof(DisableFileValidation));
        RaisePropertyChanged(nameof(FollowLauncherProxy));
        RaisePropertyChanged(nameof(DisableJavaLaunchWrapper));
        RaisePropertyChanged(nameof(DisableRetroWrapper));
        RaisePropertyChanged(nameof(UseDebugLog4jConfig));
    }
}
