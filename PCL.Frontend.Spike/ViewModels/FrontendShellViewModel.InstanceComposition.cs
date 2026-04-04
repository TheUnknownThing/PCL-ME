using System.IO;
using System.Text.Json;
using Avalonia.Media.Imaging;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string[] InstanceOverviewIconFiles =
    [
        string.Empty,
        "CobbleStone.png",
        "CommandBlock.png",
        "GoldBlock.png",
        "Grass.png",
        "GrassPath.png",
        "Anvil.png",
        "RedstoneBlock.png",
        "RedstoneLampOn.png",
        "RedstoneLampOff.png",
        "Egg.png",
        "Fabric.png",
        "Quilt.png",
        "NeoForge.png",
        "Cleanroom.png"
    ];

    private void ApplyInstanceComposition(FrontendInstanceComposition composition)
    {
        _instanceComposition = composition;
        _suppressInstancePersistence = true;
        try
        {
            InitializeInstanceOverviewSurface();
            InitializeInstanceExportSurface();
            InitializeInstanceInstallSurface();
            InitializeInstanceContentSurfaces();
            InitializeInstanceSetupSurface();
            RaiseInstanceSurfaceProperties();
        }
        finally
        {
            _suppressInstancePersistence = false;
        }
    }

    private void ReloadInstanceComposition()
    {
        ApplyInstanceComposition(FrontendInstanceCompositionService.Compose(_shellActionService.RuntimePaths));
        ReloadToolsComposition();
        ReloadVersionSavesComposition();
        ReloadDownloadComposition();
        RaisePropertyChanged(nameof(GameLinkWorldOptions));
        RaisePropertyChanged(nameof(SelectedGameLinkWorldIndex));
    }

    private void PersistInstanceSetting(string? propertyName)
    {
        if (_suppressInstancePersistence || string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;
        if (string.IsNullOrWhiteSpace(instanceDirectory))
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(SelectedInstanceOverviewIconIndex):
                PersistInstanceOverviewIconSelection(instanceDirectory, SelectedInstanceOverviewIconIndex);
                break;
            case nameof(SelectedInstanceOverviewCategoryIndex):
                _shellActionService.PersistInstanceValue(instanceDirectory, "DisplayType", SelectedInstanceOverviewCategoryIndex);
                break;
            case nameof(SelectedInstanceIsolationIndex):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentIndieV2", SelectedInstanceIsolationIndex == 0);
                break;
            case nameof(InstanceWindowTitleSetting):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentTitle", InstanceWindowTitleSetting);
                break;
            case nameof(UseDefaultInstanceWindowTitle):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentTitleEmpty", UseDefaultInstanceWindowTitle);
                break;
            case nameof(InstanceCustomInfoSetting):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentInfo", InstanceCustomInfoSetting);
                break;
            case nameof(SelectedInstanceJavaIndex):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentJavaSelect", BuildStoredInstanceJavaSelection());
                break;
            case nameof(UseGlobalInstanceRamAllocation):
            case nameof(UseAutomaticInstanceRamAllocation):
            case nameof(UseCustomInstanceRamAllocation):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionRamType", _instanceMemoryModeIndex);
                break;
            case nameof(InstanceCustomRamAllocation):
                _shellActionService.PersistInstanceValue(
                    instanceDirectory,
                    "VersionRamCustom",
                    FrontendSetupCompositionService.MapLaunchRamGbToStoredValue(InstanceCustomRamAllocation));
                break;
            case nameof(SelectedInstanceMemoryOptimizeIndex):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionRamOptimize", SelectedInstanceMemoryOptimizeIndex);
                break;
            case nameof(SelectedInstanceServerLoginRequireIndex):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionServerLoginRequire", SelectedInstanceServerLoginRequireIndex);
                break;
            case nameof(InstanceServerAuthServer):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionServerAuthServer", InstanceServerAuthServer);
                break;
            case nameof(InstanceServerAuthRegister):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionServerAuthRegister", InstanceServerAuthRegister);
                break;
            case nameof(InstanceServerAuthName):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionServerAuthName", InstanceServerAuthName);
                break;
            case nameof(InstanceServerAutoJoin):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionServerEnter", InstanceServerAutoJoin);
                break;
            case nameof(SelectedInstanceRendererIndex):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceRenderer", SelectedInstanceRendererIndex);
                break;
            case nameof(InstanceLaunchJvmArguments):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceJvm", InstanceLaunchJvmArguments);
                break;
            case nameof(InstanceLaunchGameArguments):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceGame", InstanceLaunchGameArguments);
                break;
            case nameof(InstanceClasspathHead):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceClasspathHead", InstanceClasspathHead);
                break;
            case nameof(InstanceLaunchBeforeCommand):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceRun", InstanceLaunchBeforeCommand);
                break;
            case nameof(WaitForInstanceLaunchBeforeCommand):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceRunWait", WaitForInstanceLaunchBeforeCommand);
                break;
            case nameof(IgnoreInstanceJavaCompatibilityWarning):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceJava", IgnoreInstanceJavaCompatibilityWarning);
                break;
            case nameof(DisableInstanceFileValidation):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceAssetsV2", DisableInstanceFileValidation);
                break;
            case nameof(FollowInstanceLauncherProxy):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceUseProxyV2", FollowInstanceLauncherProxy);
                break;
            case nameof(DisableInstanceJavaLaunchWrapper):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceDisableJLW", DisableInstanceJavaLaunchWrapper);
                break;
            case nameof(DisableInstanceRetroWrapper):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceDisableRW", DisableInstanceRetroWrapper);
                break;
            case nameof(UseInstanceDebugLog4jConfig):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionUseDebugLog4j2Config", UseInstanceDebugLog4jConfig);
                break;
            case nameof(IsInstanceServerLoginLocked):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionServerLoginLock", IsInstanceServerLoginLocked);
                break;
        }
    }

    private void RaiseInstanceSurfaceProperties()
    {
        RefreshInstanceOverviewSurface();
        RefreshInstanceSetupSurface();
        RefreshInstanceExportSurface();
        RefreshInstanceInstallSurface();
        RefreshInstanceContentSurfaces();
    }

    private void OpenInstanceTarget(string activity, string? target, string emptyState)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            AddActivity(activity, emptyState);
            return;
        }

        if (_shellActionService.TryOpenExternalTarget(target, out var error))
        {
            AddActivity(activity, target);
            return;
        }

        AddActivity(activity, error ?? target);
    }

    private void LockInstanceLogin()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("锁定验证方式", "当前未选择实例。");
            return;
        }

        if (IsInstanceServerLoginLocked)
        {
            AddActivity("锁定验证方式", "当前实例的验证方式已经锁定。");
            return;
        }

        IsInstanceServerLoginLocked = true;
        AddActivity("锁定验证方式", _instanceComposition.Selection.InstanceName);
        ReloadInstanceComposition();
    }

    private void PersistInstanceOverviewIconSelection(string instanceDirectory, int selectedIndex)
    {
        if (selectedIndex <= 0 || selectedIndex >= InstanceOverviewIconFiles.Length)
        {
            _shellActionService.RemoveInstanceValues(instanceDirectory, ["Logo", "LogoCustom"]);
            return;
        }

        _shellActionService.PersistInstanceValue(instanceDirectory, "LogoCustom", false);
        _shellActionService.PersistInstanceValue(
            instanceDirectory,
            "Logo",
            $"pack://application:,,,/images/Blocks/{InstanceOverviewIconFiles[selectedIndex]}");
    }

    private string BuildStoredInstanceJavaSelection()
    {
        var selectedKey = GetSelectedInstanceJavaKey();
        return selectedKey switch
        {
            "global" => "使用全局设置",
            "auto" => JsonSerializer.Serialize(new { kind = "auto" }),
            var key when key.StartsWith("existing:", StringComparison.Ordinal) => JsonSerializer.Serialize(new
            {
                kind = "exist",
                JavaExePath = key["existing:".Length..]
            }),
            var key when key.StartsWith("relative:", StringComparison.Ordinal) => JsonSerializer.Serialize(new
            {
                kind = "relative",
                RelativePath = key["relative:".Length..]
            }),
            _ => "使用全局设置"
        };
    }

    private Bitmap? LoadInstanceBitmap(string? path, params string[] fallbackSegments)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return new Bitmap(path);
        }

        return fallbackSegments.Length == 0
            ? null
            : LoadLauncherBitmap(fallbackSegments);
    }

    private string ResolveCurrentInstanceResourceDirectory(string folderName)
    {
        if (!_instanceComposition.Selection.HasSelection || string.IsNullOrWhiteSpace(folderName))
        {
            return string.Empty;
        }

        var selection = _instanceComposition.Selection;
        if (!selection.HasLabyMod || string.IsNullOrWhiteSpace(selection.VanillaVersion))
        {
            return Path.Combine(selection.IndieDirectory, folderName);
        }

        return Path.Combine(selection.IndieDirectory, "labymod-neo", "fabric", selection.VanillaVersion, folderName);
    }
}
