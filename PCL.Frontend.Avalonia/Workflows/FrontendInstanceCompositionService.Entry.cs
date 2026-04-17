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
    public static FrontendInstanceComposition Compose(FrontendRuntimePaths runtimePaths, II18nService? i18n = null)
    {
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        return Compose(runtimePaths, selectedInstanceName, LoadMode.Full, i18n);
    }

    public static FrontendInstanceComposition Compose(
        FrontendRuntimePaths runtimePaths,
        LoadMode loadMode,
        II18nService? i18n = null)
    {
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        return Compose(runtimePaths, selectedInstanceName, loadMode, i18n);
    }

    public static FrontendInstanceComposition Compose(
        FrontendRuntimePaths runtimePaths,
        string? selectedInstanceName,
        II18nService? i18n = null)
        => Compose(runtimePaths, selectedInstanceName, LoadMode.Full, i18n);

    public static FrontendInstanceComposition Compose(
        FrontendRuntimePaths runtimePaths,
        string? selectedInstanceName,
        LoadMode loadMode,
        II18nService? i18n = null)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var launcherDirectory = FrontendLauncherPathService.ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var resolvedInstanceName = selectedInstanceName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(resolvedInstanceName))
        {
            return CreateEmptyComposition(launcherDirectory, i18n: i18n);
        }

        var instanceDirectory = Path.Combine(launcherDirectory, "versions", resolvedInstanceName);
        if (!Directory.Exists(instanceDirectory))
        {
            return CreateEmptyComposition(launcherDirectory, resolvedInstanceName, i18n);
        }

        var instanceConfig = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
        var manifestSummary = ReadManifestSummary(launcherDirectory, resolvedInstanceName);
        var isIndie = ResolveIsolationEnabled(localConfig, instanceConfig, manifestSummary);
        var indieDirectory = isIndie ? instanceDirectory : launcherDirectory;
        var vanillaVersion = manifestSummary.VanillaVersion?.ToString() ?? ReadValue(instanceConfig, "VersionVanillaName", Text(i18n, "instance.common.unknown_version", "Unknown"));
        var selection = new FrontendInstanceSelectionState(
            HasSelection: true,
            InstanceName: resolvedInstanceName,
            InstanceDirectory: instanceDirectory,
            IndieDirectory: indieDirectory,
            LauncherDirectory: launcherDirectory,
            IsIndie: isIndie,
            IsModable: IsModable(manifestSummary),
            HasLabyMod: manifestSummary.HasLabyMod,
            VanillaVersion: vanillaVersion);
        var javaEntries = ParseJavaEntries(ReadValue(localConfig, "LaunchArgumentJavaUser", "[]"));
        var installManifestSummary = MergeInstallAddonStates(
            selection,
            manifestSummary,
            includeMetadataFallback: loadMode >= LoadMode.InstallAware);
        var setupState = BuildSetupState(selection, manifestSummary, sharedConfig, localConfig, instanceConfig, javaEntries, i18n);
        var exportState = loadMode == LoadMode.Full
            ? BuildExportState(selection, manifestSummary, i18n)
            : CreatePlaceholderExportState(selection, manifestSummary);
        var modEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.Mods, i18n)
            : [];
        var disabledModEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.DisabledMods, i18n)
            : [];
        var resourcePackEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.ResourcePacks, i18n)
            : [];
        var shaderEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.Shaders, i18n)
            : [];
        var schematicEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.Schematics, i18n)
            : [];

        return new FrontendInstanceComposition(
            selection,
            BuildOverviewState(selection, manifestSummary, instanceConfig, setupState, i18n),
            setupState,
            exportState,
            BuildInstallState(selection, installManifestSummary, i18n),
            new FrontendInstanceContentState(BuildWorldEntries(selection, i18n)),
            new FrontendInstanceScreenshotState(BuildScreenshotEntries(selection, i18n)),
            new FrontendInstanceServerState(BuildServerEntries(selection, i18n)),
            new FrontendInstanceResourceState(modEntries),
            new FrontendInstanceResourceState(disabledModEntries),
            new FrontendInstanceResourceState(resourcePackEntries),
            new FrontendInstanceResourceState(shaderEntries),
            new FrontendInstanceResourceState(schematicEntries));
    }

    private static FrontendInstanceComposition CreateEmptyComposition(
        string launcherDirectory,
        string? instanceName = null,
        II18nService? i18n = null)
    {
        var resolvedInstanceName = string.IsNullOrWhiteSpace(instanceName)
            ? Text(i18n, "instance.common.no_selection", "No instance selected")
            : instanceName;
        var selection = new FrontendInstanceSelectionState(
            HasSelection: false,
            InstanceName: resolvedInstanceName,
            InstanceDirectory: string.Empty,
            IndieDirectory: launcherDirectory,
            LauncherDirectory: launcherDirectory,
            IsIndie: false,
            IsModable: false,
            HasLabyMod: false,
            VanillaVersion: Text(i18n, "instance.common.unknown_version", "Unknown"));
        var setup = new FrontendInstanceSetupState(
            IsolationIndex: 0,
            WindowTitle: string.Empty,
            UseDefaultWindowTitle: false,
            CustomInfo: string.Empty,
            JavaOptions:
            [
                new FrontendInstanceJavaOption("global", Text(i18n, "instance.settings.options.follow_global", "Follow global setting")),
                new FrontendInstanceJavaOption("auto", Text(i18n, "instance.settings.java_options.auto_select", "Automatically choose a suitable Java"))
            ],
            SelectedJavaIndex: 0,
            SelectedJavaKey: "global",
            MemoryModeIndex: 2,
            CustomMemoryAllocationGb: FrontendSetupCompositionService.MapStoredLaunchRamToGb(15),
            OptimizeMemoryIndex: 0,
            UsedMemoryGb: 0,
            TotalMemoryGb: 0,
            AutomaticAllocatedMemoryGb: 0,
            GlobalAllocatedMemoryGb: 0,
            UsedMemoryLabel: "0.0 GB",
            TotalMemoryLabel: " / 0.0 GB",
            AllocatedMemoryLabel: "0.0 GB",
            ShowMemoryWarning: false,
            Show32BitJavaWarning: false,
            ServerLoginRequirementIndex: 0,
            IsServerLoginLocked: false,
            AuthServer: string.Empty,
            AuthRegister: string.Empty,
            AuthName: string.Empty,
            AutoJoinServer: string.Empty,
            RendererIndex: 0,
            JvmArguments: string.Empty,
            GameArguments: string.Empty,
            ClasspathHead: string.Empty,
            PreLaunchCommand: string.Empty,
            EnvironmentVariables: string.Empty,
            WaitForPreLaunchCommand: true,
            ForceX11OnWaylandMode: 0,
            IgnoreJavaCompatibilityWarning: false,
            DisableFileValidation: false,
            FollowLauncherProxy: false,
            DisableJavaLaunchWrapper: false,
            DisableRetroWrapper: false,
            UseDebugLog4jConfig: false);

        return new FrontendInstanceComposition(
            selection,
            new FrontendInstanceOverviewState(
                resolvedInstanceName,
                Text(i18n, "instance.overview.messages.select_to_view_details", "Select an instance to view details."),
                null,
                0,
                0,
                false,
                [],
                []),
            setup,
            new FrontendInstanceExportState(resolvedInstanceName, "1.0.0", false, false, false, []),
            new FrontendInstanceInstallState(
                resolvedInstanceName,
                Text(i18n, "instance.common.no_selection", "No instance selected"),
                Text(i18n, "instance.install.minecraft.version", "Minecraft {version}", ("version", Text(i18n, "instance.common.unknown_version", "Unknown"))),
                "Grass.png",
                [],
                []),
            new FrontendInstanceContentState([]),
            new FrontendInstanceScreenshotState([]),
            new FrontendInstanceServerState([]),
            new FrontendInstanceResourceState([]),
            new FrontendInstanceResourceState([]),
            new FrontendInstanceResourceState([]),
            new FrontendInstanceResourceState([]),
            new FrontendInstanceResourceState([]));
    }

}
