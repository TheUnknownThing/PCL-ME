using Avalonia.Controls;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.Panes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private int _selectedInstanceIsolationIndex;
    private string _instanceWindowTitle = string.Empty;
    private bool _useDefaultInstanceWindowTitle;
    private string _instanceCustomInfo = string.Empty;
    private IReadOnlyList<FrontendInstanceJavaOption> _instanceJavaOptionEntries = [];
    private int _instanceMemoryModeIndex = 2;
    private double _instanceCustomRamAllocation = 2.5;
    private double _instanceUsedRamGb;
    private double _instanceTotalRamGb;
    private double _instanceAutomaticAllocatedRamGb;
    private double _instanceGlobalAllocatedRamGb;
    private bool _showInstanceRamAllocationWarning;
    private bool _showInstance32BitJavaWarning;
    private int _selectedInstanceServerLoginRequireIndex;
    private string _instanceServerAuthServer = "https://littleskin.cn/api/yggdrasil";
    private string _instanceServerAutoJoin = string.Empty;
    private int _selectedInstanceJavaIndex;
    private string _instanceClasspathHead = string.Empty;
    private int _selectedInstanceRendererIndex;
    private string _instanceLaunchWrapperCommand = string.Empty;
    private string _instanceLaunchJvmArguments = string.Empty;
    private string _instanceLaunchGameArguments = string.Empty;
    private string _instanceLaunchBeforeCommand = string.Empty;
    private string _instanceEnvironmentVariables = string.Empty;
    private bool _waitForInstanceLaunchBeforeCommand = true;
    private int _selectedInstanceForceX11OnWaylandIndex;
    private bool _ignoreInstanceJavaCompatibilityWarning;
    private bool _disableInstanceFileValidation;
    private bool _followInstanceLauncherProxy;
    private bool _disableInstanceJavaLaunchWrapper;
    private bool _disableInstanceRetroWrapper;
    private bool _useInstanceDebugLog4jConfig;

    public IReadOnlyList<string> InstanceIsolationOptions =>
    [
        SD("instance.settings.options.follow_global"),
        SD("instance.settings.options.on"),
        SD("instance.settings.options.off")
    ];

    public IReadOnlyList<string> InstanceServerLoginRequireOptions =>
    [
        SD("instance.settings.server.require.none"),
        SD("instance.settings.server.require.official"),
        SD("instance.settings.server.require.third_party"),
        SD("instance.settings.server.require.both")
    ];

    public IReadOnlyList<string> InstanceRendererOptions =>
    [
        SD("instance.settings.options.follow_global"),
        SD("instance.settings.renderer.game_default"),
        SD("instance.settings.renderer.software"),
        SD("instance.settings.renderer.directx12"),
        SD("instance.settings.renderer.vulkan")
    ];

    public IReadOnlyList<string> InstanceForceX11OnWaylandOptions =>
    [
        SD("instance.settings.options.follow_global"),
        SD("instance.settings.linux.force_x11"),
        SD("instance.settings.linux.system_default")
    ];

    public IReadOnlyList<string> InstanceJavaOptions => _instanceJavaOptionEntries
        .Select(entry => entry.Label)
        .ToArray();

    public int SelectedInstanceIsolationIndex
    {
        get => _selectedInstanceIsolationIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, InstanceIsolationOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedInstanceIsolationIndex, normalizedValue);
            }
        }
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
        set
        {
            if (TryNormalizeSelectionIndex(value, InstanceJavaOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedInstanceJavaIndex, normalizedValue);
            }
        }
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
                RaisePropertyChanged(nameof(InstanceAllocatedRamBarWidth));
                RaisePropertyChanged(nameof(InstanceFreeRamBarWidth));
                RaisePropertyChanged(nameof(ShowInstanceRamAllocationWarning));
            }
        }
    }

    public string InstanceCustomRamAllocationLabel => FormatMemorySummarySize(Math.Round(InstanceCustomRamAllocation));

    public string InstanceUsedRamLabel => FormatMemorySummarySize(_instanceUsedRamGb);

    public string InstanceTotalRamLabel => FormatMemorySummarySize(_instanceTotalRamGb);

    public string InstanceAllocatedRamLabel => FormatMemorySummarySize(ResolveInstanceAllocatedRamGb());

    public GridLength InstanceUsedRamBarWidth => CreateMemoryBarWidth(_instanceUsedRamGb);

    public GridLength InstanceAllocatedRamBarWidth => CreateMemoryBarWidth(ResolveInstanceAllocatedRamGb());

    public GridLength InstanceFreeRamBarWidth => CreateMemoryBarWidth(Math.Max(
        _instanceTotalRamGb - _instanceUsedRamGb - ResolveInstanceAllocatedRamGb(),
        0));

    public bool ShowInstanceRamAllocationWarning => UseCustomInstanceRamAllocation
        && (_showInstanceRamAllocationWarning || InstanceCustomRamAllocation >= 8);


    public bool ShowInstance32BitJavaWarning => _showInstance32BitJavaWarning;

    public int SelectedInstanceServerLoginRequireIndex
    {
        get => _selectedInstanceServerLoginRequireIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, InstanceServerLoginRequireOptions.Count, out var nextValue))
            {
                return;
            }

            if (SetProperty(ref _selectedInstanceServerLoginRequireIndex, nextValue))
            {
                RaisePropertyChanged(nameof(ShowInstanceServerAuthFields));
                RaisePropertyChanged(nameof(ShowInstanceServerProfileActions));
                RaisePropertyChanged(nameof(CanCreateInstanceProfile));
            }
        }
    }

    public bool ShowInstanceServerAuthFields => SelectedInstanceServerLoginRequireIndex >= 2;

    public bool ShowInstanceServerProfileActions => ShowInstanceServerAuthFields;

    public bool CanCreateInstanceProfile => ShowInstanceServerProfileActions;

    public string InstanceServerAuthServer
    {
        get => _instanceServerAuthServer;
        set => SetProperty(ref _instanceServerAuthServer, value);
    }

    public string InstanceServerAutoJoin
    {
        get => _instanceServerAutoJoin;
        set => SetProperty(ref _instanceServerAutoJoin, value);
    }

    public int SelectedInstanceRendererIndex
    {
        get => _selectedInstanceRendererIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, InstanceRendererOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedInstanceRendererIndex, normalizedValue);
            }
        }
    }

    public string InstanceLaunchJvmArguments
    {
        get => _instanceLaunchJvmArguments;
        set => SetProperty(ref _instanceLaunchJvmArguments, value);
    }

    public string InstanceLaunchWrapperCommand
    {
        get => _instanceLaunchWrapperCommand;
        set => SetProperty(ref _instanceLaunchWrapperCommand, value);
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

    public string InstanceEnvironmentVariables
    {
        get => _instanceEnvironmentVariables;
        set => SetProperty(ref _instanceEnvironmentVariables, value);
    }

    public bool WaitForInstanceLaunchBeforeCommand
    {
        get => _waitForInstanceLaunchBeforeCommand;
        set => SetProperty(ref _waitForInstanceLaunchBeforeCommand, value);
    }

    public int SelectedInstanceForceX11OnWaylandIndex
    {
        get => _selectedInstanceForceX11OnWaylandIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, InstanceForceX11OnWaylandOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedInstanceForceX11OnWaylandIndex, normalizedValue);
            }
        }
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
        _instanceUsedRamGb = setup.UsedMemoryGb;
        _instanceTotalRamGb = setup.TotalMemoryGb;
        _instanceAutomaticAllocatedRamGb = setup.AutomaticAllocatedMemoryGb;
        _instanceGlobalAllocatedRamGb = setup.GlobalAllocatedMemoryGb;
        _showInstanceRamAllocationWarning = setup.ShowMemoryWarning;
        _showInstance32BitJavaWarning = setup.Show32BitJavaWarning;
        _selectedInstanceServerLoginRequireIndex = Math.Clamp(setup.ServerLoginRequirementIndex, 0, InstanceServerLoginRequireOptions.Count - 1);
        _instanceServerAuthServer = setup.AuthServer;
        _instanceServerAutoJoin = setup.AutoJoinServer;
        _selectedInstanceRendererIndex = Math.Clamp(setup.RendererIndex, 0, InstanceRendererOptions.Count - 1);
        _instanceLaunchWrapperCommand = setup.WrapperCommand;
        _instanceLaunchJvmArguments = setup.JvmArguments;
        _instanceLaunchGameArguments = setup.GameArguments;
        _instanceClasspathHead = setup.ClasspathHead;
        _instanceLaunchBeforeCommand = setup.PreLaunchCommand;
        _instanceEnvironmentVariables = setup.EnvironmentVariables;
        _waitForInstanceLaunchBeforeCommand = setup.WaitForPreLaunchCommand;
        _selectedInstanceForceX11OnWaylandIndex = Math.Clamp(setup.ForceX11OnWaylandMode, 0, InstanceForceX11OnWaylandOptions.Count - 1);
        _ignoreInstanceJavaCompatibilityWarning = setup.IgnoreJavaCompatibilityWarning;
        _disableInstanceFileValidation = setup.DisableFileValidation;
        _followInstanceLauncherProxy = setup.FollowLauncherProxy;
        _disableInstanceJavaLaunchWrapper = setup.DisableJavaLaunchWrapper;
        _disableInstanceRetroWrapper = setup.DisableRetroWrapper;
        _useInstanceDebugLog4jConfig = setup.UseDebugLog4jConfig;
    }

    private void RefreshInstanceSetupSurface()
    {
        if (!IsCurrentStandardRightPane(StandardRightPaneKind.InstanceSetup))
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
        RaisePropertyChanged(nameof(InstanceUsedRamLabel));
        RaisePropertyChanged(nameof(InstanceTotalRamLabel));
        RaisePropertyChanged(nameof(InstanceAllocatedRamLabel));
        RaisePropertyChanged(nameof(InstanceUsedRamBarWidth));
        RaisePropertyChanged(nameof(InstanceAllocatedRamBarWidth));
        RaisePropertyChanged(nameof(InstanceFreeRamBarWidth));
        RaisePropertyChanged(nameof(ShowInstanceRamAllocationWarning));
        RaisePropertyChanged(nameof(ShowInstance32BitJavaWarning));
        RaisePropertyChanged(nameof(SelectedInstanceServerLoginRequireIndex));
        RaisePropertyChanged(nameof(ShowInstanceServerAuthFields));
        RaisePropertyChanged(nameof(ShowInstanceServerProfileActions));
        RaisePropertyChanged(nameof(CanCreateInstanceProfile));
        RaisePropertyChanged(nameof(InstanceServerAuthServer));
        RaisePropertyChanged(nameof(InstanceServerAutoJoin));
        RaisePropertyChanged(nameof(InstanceRendererOptions));
        RaisePropertyChanged(nameof(SelectedInstanceRendererIndex));
        RaisePropertyChanged(nameof(InstanceLaunchWrapperCommand));
        RaisePropertyChanged(nameof(InstanceLaunchJvmArguments));
        RaisePropertyChanged(nameof(InstanceLaunchGameArguments));
        RaisePropertyChanged(nameof(InstanceClasspathHead));
        RaisePropertyChanged(nameof(InstanceLaunchBeforeCommand));
        RaisePropertyChanged(nameof(InstanceEnvironmentVariables));
        RaisePropertyChanged(nameof(WaitForInstanceLaunchBeforeCommand));
        RaisePropertyChanged(nameof(InstanceForceX11OnWaylandOptions));
        RaisePropertyChanged(nameof(SelectedInstanceForceX11OnWaylandIndex));
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
        RaisePropertyChanged(nameof(InstanceAllocatedRamBarWidth));
        RaisePropertyChanged(nameof(InstanceFreeRamBarWidth));
        RaisePropertyChanged(nameof(ShowInstanceRamAllocationWarning));
        RaisePropertyChanged(nameof(ShowInstance32BitJavaWarning));
        RaisePropertyChanged(nameof(InstanceUsedRamLabel));
        RaisePropertyChanged(nameof(InstanceTotalRamLabel));
        RaisePropertyChanged(nameof(InstanceCustomRamAllocationLabel));
        RaisePropertyChanged(nameof(InstanceCustomRamAllocation));
        RaisePropertyChanged(nameof(UseGlobalInstanceRamAllocation));
        RaisePropertyChanged(nameof(UseAutomaticInstanceRamAllocation));
        RaisePropertyChanged(nameof(UseCustomInstanceRamAllocation));
    }

    private static GridLength CreateMemoryBarWidth(double memoryGb)
    {
        return new GridLength(Math.Max(memoryGb, 0.05), GridUnitType.Star);
    }

    private double ResolveInstanceAllocatedRamGb()
    {
        return _instanceMemoryModeIndex switch
        {
            1 => Math.Round(InstanceCustomRamAllocation, 1),
            2 => _instanceGlobalAllocatedRamGb,
            _ => _instanceAutomaticAllocatedRamGb
        };
    }
}
