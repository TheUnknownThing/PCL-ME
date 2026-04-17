using System;
using System.Collections.Generic;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    internal string SD(string key)
    {
        return _i18n.T(key);
    }

    internal string SD(string key, params (string Name, object? Value)[] args)
    {
        if (args.Length == 0)
        {
            return _i18n.T(key);
        }

        var dictionary = new Dictionary<string, object?>(args.Length, StringComparer.Ordinal);
        foreach (var (name, value) in args)
        {
            dictionary[name] = value;
        }

        return _i18n.T(key, dictionary);
    }
    public string InstanceOverviewInfoHeaderText => SD("instance.overview.sections.info");
    public string InstanceOverviewShortcutsHeaderText => SD("instance.overview.sections.shortcuts");
    public string InstanceOverviewAdvancedHeaderText => SD("instance.overview.sections.advanced");
    public string InstanceOverviewPersonalizationHeaderText => SD("instance.overview.sections.personalization");
    public string InstanceOverviewIconLabelText => SD("instance.overview.personalization.icon");
    public string InstanceOverviewCategoryLabelText => SD("instance.overview.personalization.category");
    public string InstanceOverviewRenameText => SD("instance.overview.actions.rename");
    public string InstanceOverviewEditDescriptionText => SD("instance.overview.actions.edit_description");
    public string InstanceOverviewOpenInstanceFolderText => SD("instance.overview.shortcuts.instance_folder");
    public string InstanceOverviewOpenSavesFolderText => SD("instance.overview.shortcuts.saves_folder");
    public string InstanceOverviewOpenModsFolderText => SD("instance.overview.shortcuts.mods_folder");
    public string InstanceOverviewExportScriptText => SD("instance.overview.advanced.export_script");
    public string InstanceOverviewTestGameText => SD("instance.overview.advanced.test_game");
    public string InstanceOverviewRepairFilesText => SD("instance.overview.advanced.repair_files");
    public string InstanceOverviewResetText => SD("instance.overview.advanced.reset");
    public string InstanceOverviewDeleteText => SD("instance.overview.advanced.delete");
    public string InstanceOverviewPatchCoreText => SD("instance.overview.advanced.patch_core");

    public string InstanceSetupIntroText => SD("instance.settings.intro");
    public string InstanceSetupOpenGlobalSettingsText => SD("instance.settings.actions.global_settings");
    public string InstanceSetupLaunchHeaderText => SD("instance.settings.sections.launch");
    public string InstanceSetupIsolationLabelText => SD("instance.settings.launch.isolation");
    public string InstanceSetupWindowTitleLabelText => SD("instance.settings.launch.window_title");
    public string InstanceSetupDefaultWindowTitleText => SD("instance.settings.launch.default_window_title");
    public string InstanceSetupCustomInfoLabelText => SD("instance.settings.launch.custom_info");
    public string InstanceSetupJavaLabelText => SD("instance.settings.launch.java");
    public string InstanceSetupMemoryHeaderText => SD("instance.settings.sections.memory");
    public string InstanceSetupMemory32BitWarningText => SD("instance.settings.memory.warning_32bit");
    public string InstanceSetupMemoryAllocationWarningText => SD("instance.settings.memory.warning_allocation");
    public string InstanceSetupMemoryUseGlobalText => SD("instance.settings.memory.use_global");
    public string InstanceSetupMemoryUseAutoText => SD("instance.settings.memory.use_auto");
    public string InstanceSetupMemoryUseCustomText => SD("instance.settings.memory.use_custom");
    public string InstanceSetupAdvancedHeaderText => SD("instance.settings.sections.advanced");
    public string InstanceSetupRendererLabelText => SD("instance.settings.advanced.renderer");
    public string InstanceSetupWrapperCommandLabelText => SD("instance.settings.advanced.wrapper_command");
    public string InstanceSetupJvmArgumentsLabelText => SD("instance.settings.advanced.jvm_head");
    public string InstanceSetupGameArgumentsLabelText => SD("instance.settings.advanced.game_tail");
    public string InstanceSetupClasspathHeadLabelText => SD("instance.settings.advanced.classpath_head");
    public string InstanceSetupPreLaunchCommandLabelText => SD("instance.settings.advanced.prelaunch_command");
    public string InstanceSetupEnvironmentVariablesLabelText => SD("instance.settings.advanced.environment_variables");
    public string InstanceSetupEnvironmentVariablesHintText => SD("instance.settings.advanced.environment_variables_hint");
    public string InstanceSetupLinuxDisplayBackendLabelText => SD("instance.settings.advanced.linux_backend");
    public string InstanceSetupLinuxDisplayBackendHintText => SD("instance.settings.advanced.linux_backend_hint");
    public string InstanceSetupWaitForPreLaunchCommandText => SD("instance.settings.advanced.wait_for_prelaunch");
    public string InstanceSetupIgnoreJavaCompatibilityText => SD("instance.settings.advanced.ignore_java_compatibility");
    public string InstanceSetupDisableFileValidationText => SD("instance.settings.advanced.disable_file_validation");
    public string InstanceSetupFollowLauncherProxyText => SD("instance.settings.advanced.follow_launcher_proxy");
    public string InstanceSetupDisableJavaLaunchWrapperText => SD("instance.settings.advanced.disable_java_launch_wrapper");
    public string InstanceSetupDisableRetroWrapperText => SD("instance.settings.advanced.disable_retro_wrapper");
    public string InstanceSetupUseDebugLog4jText => SD("instance.settings.advanced.use_debug_log4j");
    public string InstanceSetupServerHeaderText => SD("instance.settings.sections.server");
    public string InstanceSetupServerLockedWarningText => SD("instance.settings.server.locked_warning");
    public string InstanceSetupServerLoginRequirementLabelText => SD("instance.settings.server.login_requirement");
    public string InstanceSetupServerAuthServerLabelText => SD("instance.settings.server.auth_server");
    public string InstanceSetupServerAuthRegisterLabelText => SD("instance.settings.server.auth_register");
    public string InstanceSetupServerAuthNameLabelText => SD("instance.settings.server.auth_name");
    public string InstanceSetupServerAutoJoinLabelText => SD("instance.settings.server.auto_join");
    public string InstanceSetupServerSetLittleSkinText => SD("instance.settings.server.set_littleskin");
    public string InstanceSetupServerLockLoginText => SD("instance.settings.server.lock_login");
    public string InstanceSetupServerCreateProfileText => SD("instance.settings.server.create_profile");

    public string InstanceInstallMinecraftHeaderText => SD("instance.install.minecraft.header");
    public string InstanceInstallModifyText => SD("instance.install.actions.modify");

    public string InstanceExportStartText => SD("instance.export.actions.start");
    public string InstanceExportModrinthWarningText => SD("instance.export.warnings.modrinth");
    public string InstanceExportNameLabelText => SD("instance.export.name");
    public string InstanceExportVersionLabelText => SD("instance.export.version");
    public string InstanceExportContentHeaderText => SD("instance.export.sections.content");
    public string InstanceExportResetText => SD("instance.export.actions.reset");
    public string InstanceExportAdvancedHeaderText => SD("instance.export.sections.advanced");
    public string InstanceExportIncludeWarningText => SD("instance.export.warnings.include_resources");
    public string InstanceExportIncludeResourcesText => SD("instance.export.options.include_resources");
    public string InstanceExportModrinthModeText => SD("instance.export.options.modrinth_mode");
    public string InstanceExportConfigHintText => SD("instance.export.config.hint");
    public string InstanceExportImportConfigText => SD("instance.export.actions.import_config");
    public string InstanceExportSaveConfigText => SD("instance.export.actions.save_config");
    public string InstanceExportGuideText => SD("instance.export.actions.guide");

    public string InstanceWorldSearchWatermark => SD("instance.content.world.search");
    public string InstanceWorldQuickActionsHeaderText => SD("instance.content.world.quick_actions");
    public string InstanceWorldOpenFolderText => SD("instance.content.world.actions.open_folder");
    public string InstanceWorldPasteClipboardText => SD("instance.content.world.actions.paste_clipboard");
    public string InstanceWorldListHeaderText => SD("instance.content.world.list");
    public string InstanceWorldOpenEntryText => SD("common.actions.open");
    public string InstanceWorldEmptyTitle => SD("instance.content.world.empty.title");
    public string InstanceWorldEmptyDescription => SD("instance.content.world.empty.description");

    public string InstanceScreenshotQuickActionsHeaderText => SD("instance.content.screenshot.quick_actions");
    public string InstanceScreenshotOpenFolderText => SD("instance.content.screenshot.actions.open_folder");
    public string InstanceScreenshotEmptyTitle => SD("instance.content.screenshot.empty.title");
    public string InstanceScreenshotEmptyDescription => SD("instance.content.screenshot.empty.description");

    public string InstanceServerQuickActionsHeaderText => SD("instance.content.server.quick_actions");
    public string InstanceServerRefreshAllText => SD("instance.content.server.actions.refresh_all");
    public string InstanceServerAddText => SD("instance.content.server.actions.add");
    public string InstanceServerRefreshText => SD("instance.content.server.actions.refresh");
    public string InstanceServerCopyAddressText => SD("instance.content.server.actions.copy_address");
    public string InstanceServerConnectText => SD("instance.content.server.actions.connect");
    public string InstanceServerEmptyTitle => SD("instance.content.server.empty.title");
    public string InstanceServerEmptyDescription => SD("instance.content.server.empty.description");

    public string InstanceResourceOpenFolderText => SD("instance.content.resource.actions.open_folder");
    public string InstanceResourceInstallFromFileText => SD("instance.content.resource.actions.install_from_file");
    public string InstanceResourceInstallFromFileInEmptyStateText => SD("instance.content.resource.actions.install_from_file_empty");
    public string InstanceResourceSelectAllText => SD("instance.content.resource.actions.select_all");
    public string InstanceResourceExportInfoText => SD("instance.content.resource.actions.export_info");
    public string InstanceResourceCheckModsText => SD("instance.content.resource.actions.check_mods");
    public string InstanceResourceEnableSelectedText => SD("instance.content.resource.actions.enable");
    public string InstanceResourceDisableSelectedText => SD("instance.content.resource.actions.disable");
    public string InstanceResourceDeleteSelectedText => SD("instance.content.resource.actions.delete");
    public string InstanceResourceClearSelectionText => SD("instance.content.resource.actions.clear_selection");
    public string InstanceResourceInstanceSelectText => SD("instance.content.resource.actions.instance_select");

    public string VersionSaveInfoHeaderText => SD("save_detail.overview.info");
    public string VersionSaveSettingsHeaderText => SD("save_detail.overview.settings");
    public string VersionSaveBackupCleanText => SD("save_detail.backup.actions.clean");
    public string VersionSaveBackupCreateText => SD("save_detail.backup.actions.create");
    public string VersionSaveBackupListHeaderText => SD("save_detail.backup.list");
    public string VersionSaveBackupOpenText => SD("common.actions.open");
    public string VersionSaveBackupEmptyTitle => SD("save_detail.backup.empty.title");
    public string VersionSaveBackupEmptyDescription => SD("save_detail.backup.empty.description");
    public string VersionSaveDatapackSearchWatermark => SD("save_detail.datapack.search");
    public string VersionSaveDatapackOpenFolderText => SD("save_detail.datapack.actions.open_folder");
    public string VersionSaveDatapackInstallFromFileText => SD("save_detail.datapack.actions.install_from_file");
    public string VersionSaveDatapackDownloadText => SD("save_detail.datapack.actions.download");
    public string VersionSaveDatapackDownloadNewText => SD("save_detail.datapack.actions.download_new");
    public string VersionSaveDatapackExportInfoText => SD("save_detail.datapack.actions.export_info");
    public string VersionSaveDatapackEmptyTitle => SD("save_detail.datapack.empty.title");
    public string VersionSaveDatapackEmptyDescription => SD("save_detail.datapack.empty.description");

    private string LocalizeResourceMeta(string meta)
    {
        if (string.IsNullOrWhiteSpace(meta))
        {
            return meta;
        }

        return meta
            .Replace("datapack", SD("save_detail.datapack.name"), StringComparison.OrdinalIgnoreCase)
            .Replace("resource pack", SD("instance.content.resource.kind.resource_pack"), StringComparison.OrdinalIgnoreCase)
            .Replace("shader", SD("instance.content.resource.kind.shader"), StringComparison.OrdinalIgnoreCase)
            .Replace("schematic file", SD("instance.content.resource.kind.schematic_file"), StringComparison.OrdinalIgnoreCase)
            .Replace("archive", SD("instance.content.resource.meta.archive"), StringComparison.OrdinalIgnoreCase)
            .Replace("folder", SD("instance.content.resource.meta.folder"), StringComparison.OrdinalIgnoreCase);
    }

    private string LocalizeResourceSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return summary;
        }

        return summary
            .Replace("Folder", SD("instance.content.resource.meta.folder"), StringComparison.OrdinalIgnoreCase);
    }

    private string LocalizeVersionSaveLabel(string label)
    {
        return label switch
        {
            "instance" => SD("save_detail.labels.instance"),
            "save_name" => SD("save_detail.labels.save_name"),
            "save_path" => SD("save_detail.labels.save_path"),
            "last_modified" => SD("save_detail.labels.last_modified"),
            "file_size" => SD("save_detail.labels.file_size"),
            "file_count" => SD("save_detail.labels.file_count"),
            "datapack_count" => SD("save_detail.labels.datapack_count"),
            "backup_count" => SD("save_detail.labels.backup_count"),
            "icon" => SD("save_detail.labels.icon"),
            "level_name" => SD("save_detail.labels.level_name"),
            "game_mode" => SD("save_detail.labels.game_mode"),
            "difficulty" => SD("save_detail.labels.difficulty"),
            "allow_commands" => SD("save_detail.labels.allow_commands"),
            "hardcore" => SD("save_detail.labels.hardcore"),
            "raining" => SD("save_detail.labels.raining"),
            "thundering" => SD("save_detail.labels.thundering"),
            "day_count" => SD("save_detail.labels.day_count"),
            "save_version" => SD("save_detail.labels.save_version"),
            "save_format" => SD("save_detail.labels.save_format"),
            "player_dimension" => SD("save_detail.labels.player_dimension"),
            _ => label
        };
    }

    private string LocalizeVersionSaveValue(string value)
    {
        return value switch
        {
            "not_found" => SD("save_detail.values.not_found"),
            "provided" => SD("save_detail.values.provided"),
            "not_provided" => SD("save_detail.values.not_provided"),
            "survival" => SD("save_detail.values.survival"),
            "creative" => SD("save_detail.values.creative"),
            "adventure" => SD("save_detail.values.adventure"),
            "spectator" => SD("save_detail.values.spectator"),
            "peaceful" => SD("save_detail.values.peaceful"),
            "easy" => SD("save_detail.values.easy"),
            "normal" => SD("save_detail.values.normal"),
            "hard" => SD("save_detail.values.hard"),
            "no" => SD("save_detail.values.no"),
            "yes" => SD("save_detail.values.yes"),
            "unknown" => SD("save_detail.values.unknown"),
            _ => value
        };
    }

    private void RefreshSectionDI18nSurfaces()
    {
        RefreshInstanceOverviewSurface();
        RefreshInstanceSetupSurface();
        RefreshInstanceInstallSurface();
        RefreshInstanceExportSurface();
        RefreshInstanceContentSurfaces();
        RefreshVersionSaveSurfaces();
    }
}
