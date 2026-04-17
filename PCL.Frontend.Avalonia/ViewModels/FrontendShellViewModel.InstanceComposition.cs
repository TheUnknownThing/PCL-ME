using System.IO;
using System.Text.Json;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly HashSet<string> RevealOnlyArtifactExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat",
        ".cmd",
        ".command",
        ".conf",
        ".config",
        ".csv",
        ".desktop",
        ".ini",
        ".json",
        ".log",
        ".md",
        ".properties",
        ".ps1",
        ".sh",
        ".text",
        ".toml",
        ".tsv",
        ".txt",
        ".xml",
        ".yaml",
        ".yml"
    };

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

    private static FrontendInstanceCompositionService.LoadMode ResolveInstanceCompositionLoadMode(LauncherFrontendRoute route)
    {
        if (route.Page != LauncherFrontendPageKey.InstanceSetup)
        {
            return FrontendInstanceCompositionService.LoadMode.Lightweight;
        }

        return route.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionInstall => FrontendInstanceCompositionService.LoadMode.InstallAware,
            LauncherFrontendSubpageKey.VersionMod
                or LauncherFrontendSubpageKey.VersionModDisabled
                or LauncherFrontendSubpageKey.VersionResourcePack
                or LauncherFrontendSubpageKey.VersionShader
                or LauncherFrontendSubpageKey.VersionSchematic
                or LauncherFrontendSubpageKey.VersionExport => FrontendInstanceCompositionService.LoadMode.Full,
            _ => FrontendInstanceCompositionService.LoadMode.Lightweight
        };
    }

    private bool HasSufficientInstanceCompositionLoadMode(FrontendInstanceCompositionService.LoadMode requiredLoadMode)
        => _instanceCompositionLoadMode >= requiredLoadMode;

    private void ApplyInstanceComposition(FrontendInstanceComposition composition, bool initializeAllSurfaces = true)
    {
        _instanceComposition = composition;
        _suppressInstancePersistence = true;
        try
        {
            if (initializeAllSurfaces)
            {
                InitializeInstanceOverviewSurface();
                InitializeInstanceExportSurface();
                InitializeInstanceInstallSurface();
                InitializeInstanceContentSurfaces();
                InitializeInstanceSetupSurface();
                RaiseInstanceSurfaceProperties();
            }
            else
            {
                InitializeActiveInstanceSurface();
                RefreshActiveInstanceSurface();
            }
        }
        finally
        {
            _suppressInstancePersistence = false;
        }
    }

    private void ReloadInstanceComposition(bool reloadDependentCompositions = true, bool initializeAllSurfaces = true)
        => ReloadInstanceComposition(
            FrontendInstanceCompositionService.LoadMode.Full,
            reloadDependentCompositions,
            initializeAllSurfaces);

    private void ReloadInstanceComposition(
        FrontendInstanceCompositionService.LoadMode loadMode,
        bool reloadDependentCompositions = true,
        bool initializeAllSurfaces = true)
    {
        _instanceCompositionLoadMode = loadMode;
        ApplyInstanceComposition(
            FrontendInstanceCompositionService.Compose(_shellActionService.RuntimePaths, loadMode, _i18n),
            initializeAllSurfaces);

        if (!reloadDependentCompositions)
        {
            return;
        }

        ReloadToolsComposition();
        ReloadVersionSavesComposition();
        ReloadDownloadComposition();
    }

    private void InitializeActiveInstanceSurface()
    {
        switch (_currentRoute.Subpage)
        {
            case LauncherFrontendSubpageKey.VersionOverall:
                InitializeInstanceOverviewSurface();
                break;
            case LauncherFrontendSubpageKey.VersionSetup:
                InitializeInstanceSetupSurface();
                break;
            case LauncherFrontendSubpageKey.VersionExport:
                InitializeInstanceExportSurface();
                break;
            case LauncherFrontendSubpageKey.VersionInstall:
                InitializeInstanceInstallSurface();
                break;
            case LauncherFrontendSubpageKey.VersionWorld:
            case LauncherFrontendSubpageKey.VersionScreenshot:
            case LauncherFrontendSubpageKey.VersionServer:
            case LauncherFrontendSubpageKey.VersionMod:
            case LauncherFrontendSubpageKey.VersionModDisabled:
            case LauncherFrontendSubpageKey.VersionResourcePack:
            case LauncherFrontendSubpageKey.VersionShader:
            case LauncherFrontendSubpageKey.VersionSchematic:
                InitializeInstanceContentSurfaces();
                break;
            default:
                InitializeInstanceOverviewSurface();
                break;
        }
    }

    private void RefreshActiveInstanceSurface()
    {
        switch (_currentRoute.Subpage)
        {
            case LauncherFrontendSubpageKey.VersionOverall:
                RefreshInstanceOverviewSurface();
                break;
            case LauncherFrontendSubpageKey.VersionSetup:
                RefreshInstanceSetupSurface();
                break;
            case LauncherFrontendSubpageKey.VersionExport:
                RefreshInstanceExportSurface();
                break;
            case LauncherFrontendSubpageKey.VersionInstall:
                RefreshInstanceInstallSurface();
                break;
            case LauncherFrontendSubpageKey.VersionWorld:
            case LauncherFrontendSubpageKey.VersionScreenshot:
            case LauncherFrontendSubpageKey.VersionServer:
            case LauncherFrontendSubpageKey.VersionMod:
            case LauncherFrontendSubpageKey.VersionModDisabled:
            case LauncherFrontendSubpageKey.VersionResourcePack:
            case LauncherFrontendSubpageKey.VersionShader:
            case LauncherFrontendSubpageKey.VersionSchematic:
                RefreshInstanceContentSurfaces();
                break;
            default:
                RefreshInstanceOverviewSurface();
                break;
        }
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
                switch (SelectedInstanceIsolationIndex)
                {
                    case 0:
                        _shellActionService.RemoveInstanceValues(instanceDirectory, ["VersionArgumentIndieV2"]);
                        break;
                    case 1:
                        _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentIndieV2", true);
                        break;
                    default:
                        _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentIndieV2", false);
                        break;
                }
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
            case nameof(InstanceLaunchWrapperCommand):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceWrapper", InstanceLaunchWrapperCommand);
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
            case nameof(InstanceEnvironmentVariables):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceEnvironmentVariables", InstanceEnvironmentVariables);
                break;
            case nameof(WaitForInstanceLaunchBeforeCommand):
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceRunWait", WaitForInstanceLaunchBeforeCommand);
                break;
            case nameof(SelectedInstanceForceX11OnWaylandIndex):
                if (SelectedInstanceForceX11OnWaylandIndex == 0)
                {
                    _shellActionService.RemoveInstanceValues(instanceDirectory, ["VersionAdvanceForceX11OnWayland"]);
                }
                else
                {
                    _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceForceX11OnWayland", SelectedInstanceForceX11OnWaylandIndex);
                }
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
        RaisePropertyChanged(nameof(HasSelectedInstance));
        RaisePropertyChanged(nameof(ShowLaunchVersionSetupButton));
        RaisePropertyChanged(nameof(LaunchVersionSelectButtonColumnSpan));
    }

    private void OpenInstanceTarget(string activity, string? target, string emptyState)
    {
        var failureTitle = $"{activity} failed";
        if (string.IsNullOrWhiteSpace(target))
        {
            AddFailureActivity(failureTitle, emptyState);
            return;
        }

        string? error;
        var openSucceeded = ShouldRevealInstanceTargetInShell(target)
            ? _shellActionService.TryRevealExternalTarget(target, out error)
            : _shellActionService.TryOpenExternalTarget(target, out error);
        if (openSucceeded)
        {
            AddActivity(activity, target);
            return;
        }

        AddFailureActivity(failureTitle, error ?? target);
    }

    private void OpenInstanceDirectoryTarget(string activity, string? target, string emptyState)
    {
        var failureTitle = $"{activity} failed";
        if (string.IsNullOrWhiteSpace(target))
        {
            AddFailureActivity(failureTitle, emptyState);
            return;
        }

        try
        {
            Directory.CreateDirectory(target);
        }
        catch (Exception ex)
        {
            AddFailureActivity(failureTitle, ex.Message);
            return;
        }

        OpenInstanceTarget(activity, target, emptyState);
    }

    private static bool ShouldRevealInstanceTargetInShell(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || Directory.Exists(target) || !File.Exists(target))
        {
            return false;
        }

        var extension = Path.GetExtension(target);
        var neverRevealInShell = !string.IsNullOrWhiteSpace(extension) && RevealOnlyArtifactExtensions.Contains(extension);
        return !neverRevealInShell;
    }

    private void LockInstanceLogin()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("Lock authentication mode", "No instance is currently selected.");
            return;
        }

        if (IsInstanceServerLoginLocked)
        {
            AddActivity("Lock authentication mode", "The current instance authentication mode is already locked.");
            return;
        }

        IsInstanceServerLoginLocked = true;
        AddActivity("Lock authentication mode", _instanceComposition.Selection.InstanceName);
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
            "global" => "Use global settings",
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
            _ => "Use global settings"
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
