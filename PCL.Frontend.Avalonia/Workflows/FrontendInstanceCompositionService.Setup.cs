using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;
using fNbt;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstanceCompositionService
{
    private static FrontendInstanceSetupState BuildSetupState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig,
        YamlFileProvider instanceConfig,
        IReadOnlyList<FrontendJavaEntry> javaEntries,
        II18nService? i18n)
    {
        var javaPreference = ReadJavaPreference(ReadValue(instanceConfig, "VersionArgumentJavaSelect", "global"));
        var javaOptions = BuildJavaOptions(javaEntries, selection.LauncherDirectory, javaPreference, i18n);
        var selectedJavaKey = ResolveSelectedJavaKey(javaPreference, selection.LauncherDirectory);
        var selectedJavaIndex = javaOptions.FindIndex(option => string.Equals(option.Key, selectedJavaKey, StringComparison.Ordinal));
        if (selectedJavaIndex < 0)
        {
            selectedJavaIndex = 0;
        }

        var resolvedJava = ResolveSelectedJava(javaPreference, javaEntries, selection.LauncherDirectory);
        var (totalMemoryGb, availableMemoryGb) = FrontendSystemMemoryService.GetPhysicalMemoryState();
        var customMemoryValue = ReadValue(instanceConfig, "VersionRamCustom", 15);
        var memoryModeIndex = Math.Clamp(ReadValue(instanceConfig, "VersionRamType", 2), 0, 2);
        var globalMemoryModeIndex = Math.Clamp(ReadValue(localConfig, "LaunchRamType", 0), 0, 1);
        var globalCustomMemoryGb = FrontendSetupCompositionService.MapStoredLaunchRamToGb(ReadValue(localConfig, "LaunchRamCustom", 15));
        var customMemoryGb = FrontendSetupCompositionService.MapStoredLaunchRamToGb(customMemoryValue);
        var modCount = Directory.Exists(ResolveResourceDirectory(selection, ResourceKind.Mods))
            ? Directory.EnumerateFiles(ResolveResourceDirectory(selection, ResourceKind.Mods), "*", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var effectiveMemoryModeIndex = memoryModeIndex == 2 ? globalMemoryModeIndex : memoryModeIndex;
        var effectiveCustomMemoryGb = memoryModeIndex == 2 ? globalCustomMemoryGb : customMemoryGb;
        var automaticAllocatedMemory = FrontendSystemMemoryService.CalculateAllocatedMemoryGb(
            0,
            effectiveCustomMemoryGb,
            selection.IsModable,
            !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion),
            modCount,
            resolvedJava?.Is64Bit,
            totalMemoryGb,
            availableMemoryGb);
        var allocatedMemory = FrontendSystemMemoryService.CalculateAllocatedMemoryGb(
            effectiveMemoryModeIndex,
            effectiveCustomMemoryGb,
            selection.IsModable,
            !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion),
            modCount,
            resolvedJava?.Is64Bit,
            totalMemoryGb,
            availableMemoryGb);
        var usedMemoryGb = Math.Max(totalMemoryGb - availableMemoryGb, 0);

        return new FrontendInstanceSetupState(
            IsolationIndex: ResolveInstanceIsolationIndex(instanceConfig),
            WindowTitle: ReadValue(instanceConfig, "VersionArgumentTitle", string.Empty),
            UseDefaultWindowTitle: ReadValue(instanceConfig, "VersionArgumentTitleEmpty", false),
            CustomInfo: ReadValue(instanceConfig, "VersionArgumentInfo", string.Empty),
            JavaOptions: javaOptions,
            SelectedJavaIndex: selectedJavaIndex,
            SelectedJavaKey: selectedJavaKey,
            MemoryModeIndex: memoryModeIndex,
            CustomMemoryAllocationGb: customMemoryGb,
            OptimizeMemoryIndex: Math.Clamp(ReadValue(instanceConfig, "VersionRamOptimize", 0), 0, 2),
            UsedMemoryGb: usedMemoryGb,
            TotalMemoryGb: totalMemoryGb,
            AutomaticAllocatedMemoryGb: automaticAllocatedMemory,
            GlobalAllocatedMemoryGb: allocatedMemory,
            UsedMemoryLabel: $"{usedMemoryGb:0.0} GB",
            TotalMemoryLabel: $" / {totalMemoryGb:0.0} GB",
            AllocatedMemoryLabel: $"{allocatedMemory:0.0} GB",
            ShowMemoryWarning: memoryModeIndex == 1 && totalMemoryGb > 0 && allocatedMemory / totalMemoryGb > 0.75,
            Show32BitJavaWarning: resolvedJava is { Is64Bit: false },
            ServerLoginRequirementIndex: Math.Clamp(ReadValue(instanceConfig, "VersionServerLoginRequire", 0), 0, 3),
            IsServerLoginLocked: ReadValue(instanceConfig, "VersionServerLoginLock", false),
            AuthServer: ReadValue(instanceConfig, "VersionServerAuthServer", string.Empty),
            AuthRegister: ReadValue(instanceConfig, "VersionServerAuthRegister", string.Empty),
            AuthName: ReadValue(instanceConfig, "VersionServerAuthName", string.Empty),
            AutoJoinServer: ReadValue(instanceConfig, "VersionServerEnter", string.Empty),
            RendererIndex: Math.Clamp(ReadValue(instanceConfig, "VersionAdvanceRenderer", 0), 0, 4),
            WrapperCommand: ReadValue(instanceConfig, "VersionAdvanceWrapper", string.Empty),
            JvmArguments: ReadValue(instanceConfig, "VersionAdvanceJvm", string.Empty),
            GameArguments: ReadValue(instanceConfig, "VersionAdvanceGame", string.Empty),
            ClasspathHead: ReadValue(instanceConfig, "VersionAdvanceClasspathHead", string.Empty),
            PreLaunchCommand: ReadValue(instanceConfig, "VersionAdvanceRun", string.Empty),
            EnvironmentVariables: ReadValue(instanceConfig, "VersionAdvanceEnvironmentVariables", string.Empty),
            WaitForPreLaunchCommand: ReadValue(instanceConfig, "VersionAdvanceRunWait", true),
            ForceX11OnWaylandMode: Math.Clamp(ReadValue(instanceConfig, "VersionAdvanceForceX11OnWayland", 0), 0, 2),
            IgnoreJavaCompatibilityWarning: ReadValue(instanceConfig, "VersionAdvanceJava", false),
            DisableFileValidation: ReadValue(instanceConfig, "VersionAdvanceAssetsV2", false),
            FollowLauncherProxy: ReadValue(instanceConfig, "VersionAdvanceUseProxyV2", false),
            DisableJavaLaunchWrapper: ReadValue(instanceConfig, "VersionAdvanceDisableJLW", false),
            DisableRetroWrapper: ReadValue(instanceConfig, "VersionAdvanceDisableRW", false),
            UseDebugLog4jConfig: ReadValue(instanceConfig, "VersionUseDebugLog4j2Config", false));
    }

}
