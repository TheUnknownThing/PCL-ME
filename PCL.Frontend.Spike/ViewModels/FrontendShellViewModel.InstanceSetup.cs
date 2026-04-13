using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private int _selectedInstanceIsolationIndex = 1;
    private string _instanceWindowTitle = string.Empty;
    private bool _useDefaultInstanceWindowTitle;
    private string _instanceCustomInfo = string.Empty;
    private IReadOnlyList<FrontendInstanceJavaOption> _instanceJavaOptionEntries = [];
    private int _instanceMemoryModeIndex = 2;
    private double _instanceCustomRamAllocation = 2.5;
    private int _selectedInstanceMemoryOptimizeIndex;
    private string _instanceUsedRamLabel = "0.0 GB";
    private string _instanceTotalRamLabel = " / 0.0 GB";
    private string _instanceAllocatedRamLabel = "0.0 GB";
    private bool _showInstanceRamAllocationWarning;
    private bool _showInstance32BitJavaWarning;
    private int _selectedInstanceServerLoginRequireIndex;
    private bool _isInstanceServerLoginLocked;
    private string _instanceServerAuthServer = "https://littleskin.cn/api/yggdrasil";
    private string _instanceServerAuthRegister = "https://littleskin.cn/auth/register";
    private string _instanceServerAuthName = "LittleSkin";
    private string _instanceServerAutoJoin = string.Empty;
    private int _selectedInstanceJavaIndex;
    private string _instanceClasspathHead = string.Empty;
    private int _selectedInstanceRendererIndex;
    private string _instanceLaunchJvmArguments = string.Empty;
    private string _instanceLaunchGameArguments = string.Empty;
    private string _instanceLaunchBeforeCommand = string.Empty;
    private bool _waitForInstanceLaunchBeforeCommand = true;
    private bool _ignoreInstanceJavaCompatibilityWarning;
    private bool _disableInstanceFileValidation;
    private bool _followInstanceLauncherProxy;
    private bool _disableInstanceJavaLaunchWrapper;
    private bool _disableInstanceRetroWrapper;
    private bool _useInstanceDebugLog4jConfig;

    public IReadOnlyList<string> InstanceIsolationOptions { get; } =
    [
        "开启",
        "关闭"
    ];

    public IReadOnlyList<string> InstanceServerLoginRequireOptions { get; } =
    [
        "无限制",
        "仅正版验证",
        "仅第三方验证（Authlib-Injector）",
        "正版验证与第三方验证（Authlib-Injector）"
    ];

    public IReadOnlyList<string> InstanceMemoryOptimizeOptions { get; } =
    [
        "跟随全局设置",
        "开启",
        "关闭"
    ];

    public IReadOnlyList<string> InstanceRendererOptions { get; } =
    [
        "跟随全局设置",
        "游戏默认",
        "软渲染（llvmpipe）",
        "DirectX12（d3d12）",
        "Vulkan（zink）"
    ];

    public IReadOnlyList<string> InstanceJavaOptions => _instanceJavaOptionEntries
        .Select(entry => entry.Label)
        .ToArray();

    public int SelectedInstanceIsolationIndex
    {
        get => _selectedInstanceIsolationIndex;
        set => SetProperty(ref _selectedInstanceIsolationIndex, Math.Clamp(value, 0, InstanceIsolationOptions.Count - 1));
    }

    public string InstanceWindowTitleSetting
    {
        get => _instanceWindowTitle;
        set => SetProperty(ref _instanceWindowTitle, value);
    }

    public bool UseDefaultInstanceWindowTitle
    {
        get => _useDefaultInstanceWindowTitle;
        set => SetProperty(ref _useDefaultInstanceWindowTitle, value);
    }

    public string InstanceCustomInfoSetting
    {
        get => _instanceCustomInfo;
        set => SetProperty(ref _instanceCustomInfo, value);
    }

    public int SelectedInstanceJavaIndex
    {
        get => _selectedInstanceJavaIndex;
        set => SetProperty(ref _selectedInstanceJavaIndex, Math.Clamp(value, 0, Math.Max(InstanceJavaOptions.Count - 1, 0)));
    }

    public bool UseGlobalInstanceRamAllocation
    {
        get => _instanceMemoryModeIndex == 2;
        set => SetInstanceMemoryMode(value, 2);
    }

    public bool UseAutomaticInstanceRamAllocation
    {
        get => _instanceMemoryModeIndex == 0;
        set => SetInstanceMemoryMode(value, 0);
    }

    public bool UseCustomInstanceRamAllocation
    {
        get => _instanceMemoryModeIndex == 1;
        set => SetInstanceMemoryMode(value, 1);
    }

    public bool IsInstanceCustomRamAllocationEnabled => UseCustomInstanceRamAllocation;

    public double InstanceCustomRamAllocation
    {
        get => _instanceCustomRamAllocation;
        set
        {
            if (SetProperty(ref _instanceCustomRamAllocation, value))
            {
                RaisePropertyChanged(nameof(InstanceCustomRamAllocationLabel));
                RaisePropertyChanged(nameof(InstanceAllocatedRamLabel));
                RaisePropertyChanged(nameof(ShowInstanceRamAllocationWarning));
            }
        }
    }

    public string InstanceCustomRamAllocationLabel => $"{Math.Round(InstanceCustomRamAllocation):0} GB";

    public int SelectedInstanceMemoryOptimizeIndex
    {
        get => _selectedInstanceMemoryOptimizeIndex;
        set => SetProperty(ref _selectedInstanceMemoryOptimizeIndex, Math.Clamp(value, 0, InstanceMemoryOptimizeOptions.Count - 1));
    }

    public string InstanceUsedRamLabel => _instanceUsedRamLabel;

    public string InstanceTotalRamLabel => _instanceTotalRamLabel;

    public string InstanceAllocatedRamLabel => UseCustomInstanceRamAllocation
        ? $"{Math.Round(InstanceCustomRamAllocation, 1):0.0} GB"
        : _instanceAllocatedRamLabel;

    public bool ShowInstanceRamAllocationWarning => UseCustomInstanceRamAllocation
        && (_showInstanceRamAllocationWarning || InstanceCustomRamAllocation >= 8);


    public bool ShowInstance32BitJavaWarning => _showInstance32BitJavaWarning;

    public int SelectedInstanceServerLoginRequireIndex
    {
        get => _selectedInstanceServerLoginRequireIndex;
        set
        {
            var nextValue = Math.Clamp(value, 0, InstanceServerLoginRequireOptions.Count - 1);
            if (SetProperty(ref _selectedInstanceServerLoginRequireIndex, nextValue))
            {
                RaisePropertyChanged(nameof(ShowInstanceServerAuthFields));
                RaisePropertyChanged(nameof(CanLockInstanceLogin));
            }
        }
    }

    public bool ShowInstanceServerAuthFields => SelectedInstanceServerLoginRequireIndex >= 2;

    public bool ShowInstanceServerLoginLockedWarning => IsInstanceServerLoginLocked;

    public bool CanLockInstanceLogin => !IsInstanceServerLoginLocked && SelectedInstanceServerLoginRequireIndex > 0;

    public bool IsInstanceServerLoginLocked
    {
        get => _isInstanceServerLoginLocked;
        set
        {
            if (SetProperty(ref _isInstanceServerLoginLocked, value))
            {
                RaisePropertyChanged(nameof(ShowInstanceServerLoginLockedWarning));
                RaisePropertyChanged(nameof(CanLockInstanceLogin));
            }
        }
    }

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

    public int SelectedInstanceRendererIndex
    {
        get => _selectedInstanceRendererIndex;
        set => SetProperty(ref _selectedInstanceRendererIndex, Math.Clamp(value, 0, InstanceRendererOptions.Count - 1));
    }

    public string InstanceLaunchJvmArguments
    {
        get => _instanceLaunchJvmArguments;
        set => SetProperty(ref _instanceLaunchJvmArguments, value);
    }

    public string InstanceLaunchGameArguments
    {
        get => _instanceLaunchGameArguments;
        set => SetProperty(ref _instanceLaunchGameArguments, value);
    }

    public string InstanceClasspathHead
    {
        get => _instanceClasspathHead;
        set => SetProperty(ref _instanceClasspathHead, value);
    }

    public string InstanceLaunchBeforeCommand
    {
        get => _instanceLaunchBeforeCommand;
        set => SetProperty(ref _instanceLaunchBeforeCommand, value);
    }

    public bool WaitForInstanceLaunchBeforeCommand
    {
        get => _waitForInstanceLaunchBeforeCommand;
        set => SetProperty(ref _waitForInstanceLaunchBeforeCommand, value);
    }

    public bool IgnoreInstanceJavaCompatibilityWarning
    {
        get => _ignoreInstanceJavaCompatibilityWarning;
        set => SetProperty(ref _ignoreInstanceJavaCompatibilityWarning, value);
    }

    public bool DisableInstanceFileValidation
    {
        get => _disableInstanceFileValidation;
        set => SetProperty(ref _disableInstanceFileValidation, value);
    }

    public bool FollowInstanceLauncherProxy
    {
        get => _followInstanceLauncherProxy;
        set => SetProperty(ref _followInstanceLauncherProxy, value);
    }

    public bool DisableInstanceJavaLaunchWrapper
    {
        get => _disableInstanceJavaLaunchWrapper;
        set => SetProperty(ref _disableInstanceJavaLaunchWrapper, value);
    }

    public bool DisableInstanceRetroWrapper
    {
        get => _disableInstanceRetroWrapper;
        set => SetProperty(ref _disableInstanceRetroWrapper, value);
    }

    public bool UseInstanceDebugLog4jConfig
    {
        get => _useInstanceDebugLog4jConfig;
        set => SetProperty(ref _useInstanceDebugLog4jConfig, value);
    }

    private void InitializeInstanceSetupSurface()
    {
        var setup = _instanceComposition.Setup;
        _selectedInstanceIsolationIndex = Math.Clamp(setup.IsolationIndex, 0, InstanceIsolationOptions.Count - 1);
        _instanceWindowTitle = setup.WindowTitle;
        _useDefaultInstanceWindowTitle = setup.UseDefaultWindowTitle;
        _instanceCustomInfo = setup.CustomInfo;
        _instanceJavaOptionEntries = setup.JavaOptions;
        _selectedInstanceJavaIndex = Math.Clamp(setup.SelectedJavaIndex, 0, Math.Max(setup.JavaOptions.Count - 1, 0));
        _instanceMemoryModeIndex = Math.Clamp(setup.MemoryModeIndex, 0, 2);
        _instanceCustomRamAllocation = setup.CustomMemoryAllocationGb;
        _selectedInstanceMemoryOptimizeIndex = Math.Clamp(setup.OptimizeMemoryIndex, 0, InstanceMemoryOptimizeOptions.Count - 1);
        _instanceUsedRamLabel = setup.UsedMemoryLabel;
        _instanceTotalRamLabel = setup.TotalMemoryLabel;
        _instanceAllocatedRamLabel = setup.AllocatedMemoryLabel;
        _showInstanceRamAllocationWarning = setup.ShowMemoryWarning;
        _showInstance32BitJavaWarning = setup.Show32BitJavaWarning;
        _selectedInstanceServerLoginRequireIndex = Math.Clamp(setup.ServerLoginRequirementIndex, 0, InstanceServerLoginRequireOptions.Count - 1);
        _isInstanceServerLoginLocked = setup.IsServerLoginLocked;
        _instanceServerAuthServer = setup.AuthServer;
        _instanceServerAuthRegister = setup.AuthRegister;
        _instanceServerAuthName = setup.AuthName;
        _instanceServerAutoJoin = setup.AutoJoinServer;
        _selectedInstanceRendererIndex = Math.Clamp(setup.RendererIndex, 0, InstanceRendererOptions.Count - 1);
        _instanceLaunchJvmArguments = setup.JvmArguments;
        _instanceLaunchGameArguments = setup.GameArguments;
        _instanceClasspathHead = setup.ClasspathHead;
        _instanceLaunchBeforeCommand = setup.PreLaunchCommand;
        _waitForInstanceLaunchBeforeCommand = setup.WaitForPreLaunchCommand;
        _ignoreInstanceJavaCompatibilityWarning = setup.IgnoreJavaCompatibilityWarning;
        _disableInstanceFileValidation = setup.DisableFileValidation;
        _followInstanceLauncherProxy = setup.FollowLauncherProxy;
        _disableInstanceJavaLaunchWrapper = setup.DisableJavaLaunchWrapper;
        _disableInstanceRetroWrapper = setup.DisableRetroWrapper;
        _useInstanceDebugLog4jConfig = setup.UseDebugLog4jConfig;
    }

    private void RefreshInstanceSetupSurface()
    {
        if (!IsInstanceSetupSurface)
        {
            return;
        }

        RaisePropertyChanged(nameof(InstanceIsolationOptions));
        RaisePropertyChanged(nameof(SelectedInstanceIsolationIndex));
        RaisePropertyChanged(nameof(InstanceWindowTitleSetting));
        RaisePropertyChanged(nameof(UseDefaultInstanceWindowTitle));
        RaisePropertyChanged(nameof(InstanceCustomInfoSetting));
        RaisePropertyChanged(nameof(InstanceJavaOptions));
        RaisePropertyChanged(nameof(SelectedInstanceJavaIndex));
        RaisePropertyChanged(nameof(UseGlobalInstanceRamAllocation));
        RaisePropertyChanged(nameof(UseAutomaticInstanceRamAllocation));
        RaisePropertyChanged(nameof(UseCustomInstanceRamAllocation));
        RaisePropertyChanged(nameof(IsInstanceCustomRamAllocationEnabled));
        RaisePropertyChanged(nameof(InstanceCustomRamAllocation));
        RaisePropertyChanged(nameof(InstanceCustomRamAllocationLabel));
        RaisePropertyChanged(nameof(SelectedInstanceMemoryOptimizeIndex));
        RaisePropertyChanged(nameof(InstanceUsedRamLabel));
        RaisePropertyChanged(nameof(InstanceTotalRamLabel));
        RaisePropertyChanged(nameof(InstanceAllocatedRamLabel));
        RaisePropertyChanged(nameof(ShowInstanceRamAllocationWarning));
        RaisePropertyChanged(nameof(ShowInstance32BitJavaWarning));
        RaisePropertyChanged(nameof(SelectedInstanceServerLoginRequireIndex));
        RaisePropertyChanged(nameof(ShowInstanceServerAuthFields));
        RaisePropertyChanged(nameof(ShowInstanceServerLoginLockedWarning));
        RaisePropertyChanged(nameof(CanLockInstanceLogin));
        RaisePropertyChanged(nameof(InstanceServerAuthServer));
        RaisePropertyChanged(nameof(InstanceServerAuthRegister));
        RaisePropertyChanged(nameof(InstanceServerAuthName));
        RaisePropertyChanged(nameof(InstanceServerAutoJoin));
        RaisePropertyChanged(nameof(InstanceRendererOptions));
        RaisePropertyChanged(nameof(SelectedInstanceRendererIndex));
        RaisePropertyChanged(nameof(InstanceLaunchJvmArguments));
        RaisePropertyChanged(nameof(InstanceLaunchGameArguments));
        RaisePropertyChanged(nameof(InstanceClasspathHead));
        RaisePropertyChanged(nameof(InstanceLaunchBeforeCommand));
        RaisePropertyChanged(nameof(WaitForInstanceLaunchBeforeCommand));
        RaisePropertyChanged(nameof(IgnoreInstanceJavaCompatibilityWarning));
        RaisePropertyChanged(nameof(DisableInstanceFileValidation));
        RaisePropertyChanged(nameof(FollowInstanceLauncherProxy));
        RaisePropertyChanged(nameof(DisableInstanceJavaLaunchWrapper));
        RaisePropertyChanged(nameof(DisableInstanceRetroWrapper));
        RaisePropertyChanged(nameof(UseInstanceDebugLog4jConfig));
    }

    private string GetSelectedInstanceJavaKey()
    {
        if (_instanceJavaOptionEntries.Count == 0)
        {
            return "global";
        }

        var index = Math.Clamp(SelectedInstanceJavaIndex, 0, _instanceJavaOptionEntries.Count - 1);
        return _instanceJavaOptionEntries[index].Key;
    }

    private void SetInstanceMemoryMode(bool selected, int mode)
    {
        if (!selected || _instanceMemoryModeIndex == mode)
        {
            return;
        }

        _instanceMemoryModeIndex = mode;
        RaisePropertyChanged(nameof(UseGlobalInstanceRamAllocation));
        RaisePropertyChanged(nameof(UseAutomaticInstanceRamAllocation));
        RaisePropertyChanged(nameof(UseCustomInstanceRamAllocation));
        RaisePropertyChanged(nameof(IsInstanceCustomRamAllocationEnabled));
        RaisePropertyChanged(nameof(InstanceAllocatedRamLabel));
        RaisePropertyChanged(nameof(ShowInstanceRamAllocationWarning));
        RaisePropertyChanged(nameof(ShowInstance32BitJavaWarning));
        RaisePropertyChanged(nameof(SelectedInstanceMemoryOptimizeIndex));
        RaisePropertyChanged(nameof(InstanceMemoryOptimizeOptions));
        RaisePropertyChanged(nameof(InstanceUsedRamLabel));
        RaisePropertyChanged(nameof(InstanceTotalRamLabel));
        RaisePropertyChanged(nameof(InstanceCustomRamAllocationLabel));
        RaisePropertyChanged(nameof(InstanceCustomRamAllocation));
        RaisePropertyChanged(nameof(UseGlobalInstanceRamAllocation));
        RaisePropertyChanged(nameof(UseAutomaticInstanceRamAllocation));
        RaisePropertyChanged(nameof(UseCustomInstanceRamAllocation));
    }
}
