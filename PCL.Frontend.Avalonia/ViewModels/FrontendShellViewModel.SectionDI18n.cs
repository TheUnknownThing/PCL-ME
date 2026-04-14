using System;
using System.Collections.Generic;
using System.Linq;

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
    public string InstanceSetupMemoryOptimizeLabelText => SD("instance.settings.memory.optimize_before_launch");
    public string InstanceSetupAdvancedHeaderText => SD("instance.settings.sections.advanced");
    public string InstanceSetupRendererLabelText => SD("instance.settings.advanced.renderer");
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

    private string LocalizeInstanceSummarySegment(string segment)
    {
        return segment switch
        {
            "独立实例" => SD("instance.common.independent"),
            "共用实例" => SD("instance.common.shared"),
            "未选择实例" => SD("instance.common.no_selection"),
            _ => segment
        };
    }

    private string LocalizeRawInstanceSubtitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        return string.Join(" / ", raw.Split(" / ", StringSplitOptions.TrimEntries).Select(LocalizeInstanceSummarySegment));
    }

    private string LocalizeOverviewInfoLabel(string label)
    {
        return label switch
        {
            "启动次数" => SD("instance.overview.info.launch_count"),
            "整合包版本" => SD("instance.overview.info.modpack_version"),
            "实例描述" => SD("instance.overview.info.description"),
            _ => label
        };
    }

    private string LocalizeOverviewInfoValue(string label, string value)
    {
        return (label, value) switch
        {
            ("启动次数", "从未启动") => SD("instance.overview.info.launch_count_never"),
            (_, "已安装") => SD("instance.common.installed"),
            (_, "未安装") => SD("instance.common.not_installed"),
            _ => LocalizeInstanceSummarySegment(value)
        };
    }

    private string LocalizeOverviewTag(string tag)
    {
        return tag switch
        {
            "支持 Mod" => SD("instance.overview.tags.mod_supported"),
            "已收藏" => SD("instance.overview.tags.favorited"),
            "自动" => SD("instance.overview.categories.auto"),
            "隐藏实例" => SD("instance.overview.categories.hidden"),
            "可安装 Mod" => SD("instance.overview.categories.modable"),
            "常规实例" => SD("instance.overview.categories.regular"),
            "不常用实例" => SD("instance.overview.categories.rare"),
            "愚人节版本" => SD("instance.overview.categories.april_fools"),
            _ => tag
        };
    }

    private string LocalizeOverviewCategoryOption(string label)
    {
        return label switch
        {
            "自动" => SD("instance.overview.categories.auto"),
            "从实例列表中隐藏" => SD("instance.overview.categories.hide_from_list"),
            "可安装 Mod 的实例" => SD("instance.overview.categories.modable_long"),
            "常规实例" => SD("instance.overview.categories.regular"),
            "不常用实例" => SD("instance.overview.categories.rare"),
            "愚人节版本" => SD("instance.overview.categories.april_fools"),
            _ => label
        };
    }

    private string LocalizeOverviewIconOption(string label)
    {
        return label switch
        {
            "自动" => SD("instance.overview.icons.auto"),
            "圆石" => SD("instance.overview.icons.cobblestone"),
            "命令方块" => SD("instance.overview.icons.command_block"),
            "金块" => SD("instance.overview.icons.gold_block"),
            "草方块" => SD("instance.overview.icons.grass_block"),
            "土径" => SD("instance.overview.icons.grass_path"),
            "铁砧" => SD("instance.overview.icons.anvil"),
            "红石块" => SD("instance.overview.icons.redstone_block"),
            "红石灯（开）" => SD("instance.overview.icons.redstone_lamp_on"),
            "红石灯（关）" => SD("instance.overview.icons.redstone_lamp_off"),
            "鸡蛋" => SD("instance.overview.icons.egg"),
            "布料（Fabric）" => SD("instance.overview.icons.fabric"),
            "方格（Quilt）" => SD("instance.overview.icons.quilt"),
            "狐狸（NeoForge）" => SD("instance.overview.icons.neoforge"),
            _ => label
        };
    }

    private string LocalizeExportTitle(string title)
    {
        return title switch
        {
            "游戏本体" => SD("instance.export.groups.game"),
            "游戏本体设置" => SD("instance.export.items.game_settings"),
            "游戏本体个人信息" => SD("instance.export.items.game_personal"),
            "OptiFine 设置" => SD("instance.export.items.optifine_settings"),
            "Mod" => SD("instance.export.groups.mods"),
            "已禁用的 Mod" => SD("instance.export.items.disabled_mods"),
            "整合包重要数据" => SD("instance.export.items.important_data"),
            "Mod 设置" => SD("instance.export.items.mod_settings"),
            "资源包" => SD("instance.export.groups.resource_packs"),
            "光影包" => SD("instance.export.groups.shaders"),
            "截图" => SD("instance.export.groups.screenshots"),
            "导出的结构" => SD("instance.export.groups.schematics"),
            "录像回放" => SD("instance.export.groups.replays"),
            "单人游戏存档" => SD("instance.export.groups.worlds"),
            "多人游戏服务器列表" => SD("instance.export.groups.servers"),
            "PCL 启动器程序" => SD("instance.export.groups.launcher"),
            "PCL 个性化内容" => SD("instance.export.items.launcher_personalization"),
            _ => title
        };
    }

    private string LocalizeExportDescription(string description)
    {
        return description switch
        {
            "模组" => SD("instance.export.descriptions.mods"),
            "纹理包 / 材质包" => SD("instance.export.descriptions.resource_packs"),
            "schematics 文件夹" => SD("instance.export.descriptions.schematics"),
            "Replay Mod 的录像文件" => SD("instance.export.descriptions.replays"),
            "世界 / 地图" => SD("instance.export.descriptions.worlds"),
            "打包跨平台版 PCL，以便没有启动器的玩家安装整合包" => SD("instance.export.descriptions.launcher"),
            "检测到 options.txt" => SD("instance.export.detected.options"),
            "未检测到配置文件" => SD("instance.export.detected.config_missing"),
            "检测到 OptiFine 设置" => SD("instance.export.detected.optifine_settings"),
            "未检测到个人设置" => SD("instance.export.detected.personal_missing"),
            "当前实例包含 OptiFine" => SD("instance.export.detected.optifine_present"),
            "当前实例未安装 OptiFine" => SD("instance.export.detected.optifine_missing"),
            "检测到 config 文件夹" => SD("instance.export.detected.config_folder"),
            "未检测到 config 文件夹" => SD("instance.export.detected.config_folder_missing"),
            "检测到配置目录" => SD("instance.export.detected.config_directory"),
            "未检测到配置目录" => SD("instance.export.detected.config_directory_missing"),
            "检测到实例 PCL 配置目录" => SD("instance.export.detected.pcl_directory"),
            "未检测到实例 PCL 配置目录" => SD("instance.export.detected.pcl_directory_missing"),
            _ => description
        };
    }

    private string LocalizeResourceMeta(string meta)
    {
        if (string.IsNullOrWhiteSpace(meta))
        {
            return meta;
        }

        return meta
            .Replace("数据包", SD("save_detail.datapack.name"), StringComparison.Ordinal)
            .Replace("资源包", SD("instance.content.resource.kind.resource_pack"), StringComparison.Ordinal)
            .Replace("光影包", SD("instance.content.resource.kind.shader"), StringComparison.Ordinal)
            .Replace("投影文件", SD("instance.content.resource.kind.schematic_file"), StringComparison.Ordinal)
            .Replace("压缩包", SD("instance.content.resource.meta.archive"), StringComparison.Ordinal)
            .Replace("文件夹", SD("instance.content.resource.meta.folder"), StringComparison.Ordinal);
    }

    private string LocalizeResourceSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return summary;
        }

        return summary.Replace("文件夹", SD("instance.content.resource.meta.folder"), StringComparison.Ordinal);
    }

    private string LocalizeVersionSaveLabel(string label)
    {
        return label switch
        {
            "实例" => SD("save_detail.labels.instance"),
            "存档名称" => SD("save_detail.labels.save_name"),
            "存档路径" => SD("save_detail.labels.save_path"),
            "最后修改" => SD("save_detail.labels.last_modified"),
            "文件大小" => SD("save_detail.labels.file_size"),
            "文件总数" => SD("save_detail.labels.file_count"),
            "数据包数量" => SD("save_detail.labels.datapack_count"),
            "备份数量" => SD("save_detail.labels.backup_count"),
            "图标" => SD("save_detail.labels.icon"),
            "关卡名称" => SD("save_detail.labels.level_name"),
            "游戏模式" => SD("save_detail.labels.game_mode"),
            "难度" => SD("save_detail.labels.difficulty"),
            "允许作弊" => SD("save_detail.labels.allow_commands"),
            "极限模式" => SD("save_detail.labels.hardcore"),
            "下雨中" => SD("save_detail.labels.raining"),
            "雷暴中" => SD("save_detail.labels.thundering"),
            "游戏天数" => SD("save_detail.labels.day_count"),
            "存档版本" => SD("save_detail.labels.save_version"),
            "存档格式" => SD("save_detail.labels.save_format"),
            "玩家维度" => SD("save_detail.labels.player_dimension"),
            _ => label
        };
    }

    private string LocalizeVersionSaveValue(string value)
    {
        return value switch
        {
            "未找到" => SD("save_detail.values.not_found"),
            "已提供" => SD("save_detail.values.provided"),
            "未提供" => SD("save_detail.values.not_provided"),
            "生存" => SD("save_detail.values.survival"),
            "创造" => SD("save_detail.values.creative"),
            "冒险" => SD("save_detail.values.adventure"),
            "旁观" => SD("save_detail.values.spectator"),
            "和平" => SD("save_detail.values.peaceful"),
            "简单" => SD("save_detail.values.easy"),
            "普通" => SD("save_detail.values.normal"),
            "困难" => SD("save_detail.values.hard"),
            "否" => SD("save_detail.values.no"),
            "是" => SD("save_detail.values.yes"),
            "未知" => SD("save_detail.values.unknown"),
            _ => value
        };
    }

    private string LocalizeServerStatusText(string text)
    {
        return text switch
        {
            "已保存服务器" => SD("instance.content.server.status.saved"),
            "正在连接..." => SD("instance.content.server.status.connecting"),
            "服务器在线" => SD("instance.content.server.status.online"),
            "服务器离线" => SD("instance.content.server.status.offline"),
            "离线" => SD("instance.content.server.status.offline_short"),
            "正在连接" => SD("instance.content.server.status.connecting_short"),
            _ when text.StartsWith("无法连接: ", StringComparison.Ordinal) =>
                SD("instance.content.server.status.connection_failed", ("message", text["无法连接: ".Length..])),
            _ => text
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
