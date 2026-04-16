using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public SetupLocalizationCatalog SetupText => _setupText;

    private void RefreshSetupLocalizationCatalog()
    {
        _setupText = CreateSetupLocalizationCatalog();
    }

    private SetupLocalizationCatalog CreateSetupLocalizationCatalog()
    {
        return new SetupLocalizationCatalog
        {
            Launch = new SetupLaunchLocalization
            {
                OptionsCardHeader = _i18n.T("setup.launch.cards.options.header"),
                DefaultIsolationLabel = _i18n.T("setup.launch.fields.default_isolation"),
                GameWindowTitleLabel = _i18n.T("setup.launch.fields.game_window_title"),
                CustomInfoLabel = _i18n.T("setup.launch.fields.custom_info"),
                LauncherVisibilityLabel = _i18n.T("setup.launch.fields.launcher_visibility"),
                ProcessPriorityLabel = _i18n.T("setup.launch.fields.process_priority"),
                WindowSizeLabel = _i18n.T("setup.launch.fields.window_size"),
                MicrosoftAuthLabel = _i18n.T("setup.launch.fields.microsoft_auth"),
                PreferredIpStackLabel = _i18n.T("setup.launch.fields.preferred_ip_stack"),
                MemoryCardHeader = _i18n.T("setup.launch.cards.memory.header"),
                Memory32BitWarning = _i18n.T("setup.launch.memory.warnings.java_32_bit"),
                MemoryOverallocationWarning = _i18n.T("setup.launch.memory.warnings.overallocated"),
                AutomaticAllocationLabel = _i18n.T("setup.launch.memory.modes.automatic"),
                AutomaticAllocationDescription = _i18n.T("setup.launch.memory.modes.automatic_description"),
                CustomAllocationLabel = _i18n.T("setup.launch.memory.modes.custom"),
                OptimizeBeforeLaunchLabel = _i18n.T("setup.launch.memory.optimize_before_launch"),
                AdvancedCardHeader = _i18n.T("setup.launch.cards.advanced.header"),
                RendererLabel = _i18n.T("setup.launch.fields.renderer"),
                JvmArgumentsLabel = _i18n.T("setup.launch.fields.jvm_arguments_head"),
                GameArgumentsLabel = _i18n.T("setup.launch.fields.game_arguments_tail"),
                BeforeCommandLabel = _i18n.T("setup.launch.fields.before_command"),
                EnvironmentVariablesLabel = _i18n.T("setup.launch.fields.environment_variables"),
                EnvironmentVariablesHint = _i18n.T("setup.launch.fields.environment_variables_hint"),
                WaitForBeforeCommandLabel = _i18n.T("setup.launch.fields.wait_for_before_command"),
                LinuxDisplayBackendLabel = _i18n.T("setup.launch.fields.linux_display_backend"),
                ForceX11Label = _i18n.T("setup.launch.fields.force_x11"),
                ForceX11Hint = _i18n.T("setup.launch.fields.force_x11_hint"),
                DisableJavaLaunchWrapperLabel = _i18n.T("setup.launch.flags.disable_java_launch_wrapper"),
                DisableRetroWrapperLabel = _i18n.T("setup.launch.flags.disable_retro_wrapper"),
                RequireDedicatedGpuLabel = _i18n.T("setup.launch.flags.require_dedicated_gpu"),
                UseJavaExecutableLabel = _i18n.T("setup.launch.flags.use_java_executable"),
                InstanceSettingsHint = _i18n.T("setup.launch.instance_settings.hint"),
                InstanceSettingsButton = _i18n.T("setup.launch.instance_settings.button"),
                IsolationOptions =
                [
                    _i18n.T("setup.launch.options.isolation.disabled"),
                    _i18n.T("setup.launch.options.isolation.mods"),
                    _i18n.T("setup.launch.options.isolation.non_release"),
                    _i18n.T("setup.launch.options.isolation.mods_and_non_release"),
                    _i18n.T("setup.launch.options.isolation.all")
                ],
                VisibilityOptions =
                [
                    _i18n.T("setup.launch.options.visibility.close_after_launch"),
                    _i18n.T("setup.launch.options.visibility.hide_then_close"),
                    _i18n.T("setup.launch.options.visibility.hide_then_reopen"),
                    _i18n.T("setup.launch.options.visibility.minimize"),
                    _i18n.T("setup.launch.options.visibility.unchanged")
                ],
                PriorityOptions =
                [
                    _i18n.T("setup.launch.options.priority.high"),
                    _i18n.T("setup.launch.options.priority.medium"),
                    _i18n.T("setup.launch.options.priority.low")
                ],
                WindowTypeOptions =
                [
                    _i18n.T("setup.launch.options.window.fullscreen"),
                    _i18n.T("setup.launch.options.window.default"),
                    _i18n.T("setup.launch.options.window.match_launcher"),
                    _i18n.T("setup.launch.options.window.custom"),
                    _i18n.T("setup.launch.options.window.maximized")
                ],
                MicrosoftAuthOptions =
                [
                    _i18n.T("setup.launch.options.microsoft_auth.web_account_manager"),
                    _i18n.T("setup.launch.options.microsoft_auth.device_code")
                ],
                PreferredIpStackOptions =
                [
                    _i18n.T("setup.launch.options.preferred_ip_stack.ipv4_first"),
                    _i18n.T("setup.launch.options.preferred_ip_stack.java_default"),
                    _i18n.T("setup.launch.options.preferred_ip_stack.ipv6_first")
                ],
                RendererOptions =
                [
                    _i18n.T("setup.launch.options.renderer.game_default"),
                    _i18n.T("setup.launch.options.renderer.software"),
                    _i18n.T("setup.launch.options.renderer.directx12"),
                    _i18n.T("setup.launch.options.renderer.vulkan")
                ]
            },
            Ui = new SetupUiLocalization
            {
                BasicCardHeader = _i18n.T("setup.ui.cards.basic.header"),
                LocaleLabel = _i18n.T("setup.ui.fields.locale"),
                LauncherOpacityLabel = _i18n.T("setup.ui.fields.launcher_opacity"),
                ThemeLabel = _i18n.T("setup.ui.fields.theme"),
                LightPaletteLabel = _i18n.T("setup.ui.fields.light_palette"),
                DarkPaletteLabel = _i18n.T("setup.ui.fields.dark_palette"),
                LightCustomLabel = _i18n.T("setup.ui.fields.light_custom"),
                DarkCustomLabel = _i18n.T("setup.ui.fields.dark_custom"),
                CustomThemeHint = _i18n.T("setup.ui.fields.custom_theme_hint"),
                ThemeRefreshHint = _i18n.T("setup.ui.fields.custom_theme_refresh_hint"),
                ThemeSwitchUnsupportedNotice = _i18n.T("setup.ui.theme_switch.unsupported_notice"),
                ThemeSwitchUnsupportedAction = _i18n.T("setup.ui.theme_switch.unsupported_action"),
                ShowLauncherLogoLabel = _i18n.T("setup.ui.flags.show_launcher_logo"),
                LockWindowSizeLabel = _i18n.T("setup.ui.flags.lock_window_size"),
                ShowLaunchingHintLabel = _i18n.T("setup.ui.flags.show_launching_hint"),
                FontCardHeader = _i18n.T("setup.ui.cards.font.header"),
                GlobalFontLabel = _i18n.T("setup.ui.fields.global_font"),
                MotdFontLabel = _i18n.T("setup.ui.fields.motd_font"),
                BackgroundAdaptLabel = _i18n.T("setup.ui.background.fields.suit"),
                BackgroundOpacityLabel = _i18n.T("setup.ui.background.fields.opacity"),
                BackgroundBlurLabel = _i18n.T("setup.ui.background.fields.blur"),
                BackgroundColorfulLabel = _i18n.T("setup.ui.background.flags.colorful"),
                BackgroundOpenFolderButton = _i18n.T("setup.ui.background.actions.open_folder"),
                BackgroundRefreshButton = _i18n.T("setup.ui.background.actions.refresh"),
                BackgroundClearButton = _i18n.T("setup.ui.background.actions.clear"),
                MusicCardHeader = _i18n.T("setup.ui.cards.music.header"),
                MusicVolumeLabel = _i18n.T("setup.ui.music.fields.volume"),
                MusicRandomLabel = _i18n.T("setup.ui.music.flags.random"),
                MusicAutoStartLabel = _i18n.T("setup.ui.music.flags.auto_start"),
                MusicStartOnLaunchLabel = _i18n.T("setup.ui.music.flags.start_on_game_launch"),
                MusicStopOnLaunchLabel = _i18n.T("setup.ui.music.flags.stop_on_game_launch"),
                MusicEnableSmtcLabel = _i18n.T("setup.ui.music.flags.enable_smtc"),
                MusicOpenFolderButton = _i18n.T("setup.ui.music.actions.open_folder"),
                MusicRefreshButton = _i18n.T("setup.ui.music.actions.refresh"),
                MusicClearButton = _i18n.T("setup.ui.music.actions.clear"),
                TitleBarCardHeader = _i18n.T("setup.ui.cards.title_bar.header"),
                LogoTypeNoneLabel = _i18n.T("setup.ui.title_bar.options.none"),
                LogoTypeDefaultLabel = _i18n.T("setup.ui.title_bar.options.default"),
                LogoTypeTextLabel = _i18n.T("setup.ui.title_bar.options.text"),
                LogoTypeImageLabel = _i18n.T("setup.ui.title_bar.options.image"),
                LogoAlignLeftLabel = _i18n.T("setup.ui.title_bar.flags.align_left"),
                LogoTextLabel = _i18n.T("setup.ui.title_bar.fields.text"),
                ChangeLogoImageButton = _i18n.T("setup.ui.title_bar.actions.change_image"),
                ClearLogoImageButton = _i18n.T("setup.ui.title_bar.actions.clear_image"),
                HomepageCardHeader = _i18n.T("setup.ui.cards.homepage.header"),
                HomepageTypeBlankLabel = _i18n.T("setup.ui.homepage.options.blank"),
                HomepageTypePresetLabel = _i18n.T("setup.ui.homepage.options.preset"),
                HomepageTypeLocalLabel = _i18n.T("setup.ui.homepage.options.local_file"),
                HomepageTypeRemoteLabel = _i18n.T("setup.ui.homepage.options.remote"),
                HomepagePresetNotice = _i18n.T("setup.ui.homepage.notices.preset_content"),
                HomepageSecurityWarning = _i18n.T("setup.ui.homepage.notices.security_warning"),
                HomepageRefreshButton = _i18n.T("setup.ui.homepage.actions.refresh"),
                HomepageGenerateTutorialButton = _i18n.T("setup.ui.homepage.actions.generate_tutorial"),
                HomepageViewTutorialButton = _i18n.T("setup.ui.homepage.actions.view_tutorial"),
                HomepageUrlLabel = _i18n.T("setup.ui.homepage.fields.url"),
                HomepagePresetLabel = _i18n.T("setup.ui.homepage.fields.preset"),
                HiddenFeaturesHint = _i18n.T("setup.ui.hidden_features.hint"),
                DarkModeOptions =
                [
                    _i18n.T("setup.ui.options.dark_mode.light"),
                    _i18n.T("setup.ui.options.dark_mode.dark"),
                    _i18n.T("setup.ui.options.dark_mode.follow_system")
                ],
                ThemeColorOptions =
                [
                    _i18n.T("setup.ui.options.theme_color.cat_blue"),
                    _i18n.T("setup.ui.options.theme_color.lemon_cyan"),
                    _i18n.T("setup.ui.options.theme_color.grass_green"),
                    _i18n.T("setup.ui.options.theme_color.pineapple_yellow"),
                    _i18n.T("setup.ui.options.theme_color.oak_brown"),
                    _i18n.T("setup.ui.options.theme_color.custom")
                ],
                FontOptions =
                [
                    _i18n.T("setup.ui.options.font.default"),
                    _i18n.T("setup.ui.options.font.source_han_sans"),
                    _i18n.T("setup.ui.options.font.lxgw_wenkai"),
                    _i18n.T("setup.ui.options.font.jetbrains_mono")
                ],
                BackgroundSuitOptions =
                [
                    _i18n.T("setup.ui.options.background_suit.smart"),
                    _i18n.T("setup.ui.options.background_suit.center"),
                    _i18n.T("setup.ui.options.background_suit.fit"),
                    _i18n.T("setup.ui.options.background_suit.stretch"),
                    _i18n.T("setup.ui.options.background_suit.tile"),
                    _i18n.T("setup.ui.options.background_suit.top_left"),
                    _i18n.T("setup.ui.options.background_suit.top_right"),
                    _i18n.T("setup.ui.options.background_suit.bottom_left"),
                    _i18n.T("setup.ui.options.background_suit.bottom_right")
                ]
            },
            GameManage = new SetupGameManageLocalization
            {
                GameResourcesCardHeader = _i18n.T("setup.game_manage.cards.game_resources.header"),
                FileDownloadSourceLabel = _i18n.T("setup.game_manage.fields.file_download_source"),
                VersionListSourceLabel = _i18n.T("setup.game_manage.fields.version_list_source"),
                MaxThreadCountLabel = _i18n.T("setup.game_manage.fields.max_thread_count"),
                SpeedLimitLabel = _i18n.T("setup.game_manage.fields.speed_limit"),
                DownloadTimeoutLabel = _i18n.T("setup.game_manage.fields.download_timeout"),
                TargetFolderLabel = _i18n.T("setup.game_manage.fields.target_folder"),
                TargetFolderHint = _i18n.T("setup.game_manage.fields.target_folder_hint"),
                InstallBehaviorLabel = _i18n.T("setup.game_manage.fields.install_behavior"),
                AutoSelectNewInstanceLabel = _i18n.T("setup.game_manage.flags.auto_select_new_instance"),
                UpgradePartialAuthlibLabel = _i18n.T("setup.game_manage.flags.upgrade_partial_authlib"),
                CommunityResourcesCardHeader = _i18n.T("setup.game_manage.cards.community_resources.header"),
                CommunityDownloadSourceLabel = _i18n.T("setup.game_manage.fields.community_download_source"),
                FileNameFormatLabel = _i18n.T("setup.game_manage.fields.file_name_format"),
                ModLocalNameStyleLabel = _i18n.T("setup.game_manage.fields.mod_local_name_style"),
                HideQuiltLoaderLabel = _i18n.T("setup.game_manage.flags.hide_quilt_loader"),
                AccessibilityCardHeader = _i18n.T("setup.game_manage.cards.accessibility.header"),
                GameUpdateNotificationsLabel = _i18n.T("setup.game_manage.fields.game_update_notifications"),
                ReleaseUpdateNotificationsLabel = _i18n.T("setup.game_manage.flags.release_update_notifications"),
                SnapshotUpdateNotificationsLabel = _i18n.T("setup.game_manage.flags.snapshot_update_notifications"),
                GameLanguageLabel = _i18n.T("setup.game_manage.fields.game_language"),
                AutoSwitchGameLanguageLabel = _i18n.T("setup.game_manage.flags.auto_switch_game_language"),
                DetectClipboardLinksLabel = _i18n.T("setup.game_manage.flags.detect_clipboard_links"),
                DownloadSourceOptions =
                [
                    _i18n.T("setup.game_manage.options.download_source.prefer_mirror"),
                    _i18n.T("setup.game_manage.options.download_source.prefer_official_then_mirror"),
                    _i18n.T("setup.game_manage.options.download_source.prefer_official")
                ],
                FileNameFormatOptions =
                [
                    _i18n.T("setup.game_manage.options.file_name_format.display_name_brackets"),
                    _i18n.T("setup.game_manage.options.file_name_format.display_name_square_brackets"),
                    _i18n.T("setup.game_manage.options.file_name_format.display_name_prefix"),
                    _i18n.T("setup.game_manage.options.file_name_format.display_name_suffix"),
                    _i18n.T("setup.game_manage.options.file_name_format.file_name_only")
                ],
                ModLocalNameStyleOptions =
                [
                    _i18n.T("setup.game_manage.options.mod_local_name_style.title_local_detail_file"),
                    _i18n.T("setup.game_manage.options.mod_local_name_style.title_file_detail_local")
                ]
            },
            About = new SetupAboutLocalization
            {
                AboutCardHeader = _i18n.T("setup.about.cards.about.header"),
                AcknowledgementsCardHeader = _i18n.T("setup.about.cards.acknowledgements.header")
            },
            Log = new SetupLogLocalization
            {
                ActionsCardHeader = _i18n.T("setup.log.cards.actions.header"),
                ExportLogButton = _i18n.T("setup.log.actions.export_latest"),
                ExportAllLogsButton = _i18n.T("setup.log.actions.export_all"),
                OpenDirectoryButton = _i18n.T("setup.log.actions.open_directory"),
                CleanHistoryButton = _i18n.T("setup.log.actions.clean_history"),
                AllLogsCardHeader = _i18n.T("setup.log.cards.all_logs.header")
            },
            Feedback = new SetupFeedbackLocalization
            {
                SubmitCardHeader = _i18n.T("setup.feedback.cards.submit.header"),
                SubmitHint = _i18n.T("setup.feedback.submit.hint"),
                OpenFeedbackButton = _i18n.T("setup.feedback.submit.button"),
                LoadingSectionTitle = _i18n.T("setup.feedback.loading.section_title"),
                LoadingEntryTitle = _i18n.T("setup.feedback.loading.entry_title"),
                LoadingEntrySummary = _i18n.T("setup.feedback.loading.entry_summary"),
                LoadFailedSectionTitle = _i18n.T("setup.feedback.failed.section_title"),
                LoadFailedEntryTitle = _i18n.T("setup.feedback.failed.entry_title")
            },
            Update = new SetupUpdateLocalization
            {
                UpdateChannelLabel = _i18n.T("setup.update.fields.channel"),
                UpdateModeLabel = _i18n.T("setup.update.fields.mode"),
                DownloadAndInstallButton = _i18n.T("setup.update.actions.download_and_install"),
                MoreInfoButton = _i18n.T("setup.update.actions.more_info"),
                CheckAgainButton = _i18n.T("setup.update.actions.check_again"),
                ViewChangelogButton = _i18n.T("setup.update.actions.view_changelog"),
                ChannelOptions =
                [
                    _i18n.T("setup.update.options.channel.release"),
                    _i18n.T("setup.update.options.channel.beta"),
                    _i18n.T("setup.update.options.channel.dev")
                ],
                ModeOptions =
                [
                    _i18n.T("setup.update.options.mode.auto_download_and_install"),
                    _i18n.T("setup.update.options.mode.auto_download_and_prompt"),
                    _i18n.T("setup.update.options.mode.prompt_only"),
                    _i18n.T("setup.update.options.mode.disabled")
                ]
            },
            Java = new SetupJavaLocalization
            {
                AddButton = _i18n.T("setup.java.actions.add"),
                AutoSelectLabel = _i18n.T("setup.java.auto_select.label"),
                AutoSelectDescription = _i18n.T("setup.java.auto_select.description"),
                OpenButton = _i18n.T("setup.java.actions.open"),
                DetailsButton = _i18n.T("setup.java.actions.details")
            },
            LauncherMisc = new SetupLauncherMiscLocalization
            {
                SystemCardHeader = _i18n.T("setup.launcher_misc.cards.system.header"),
                LauncherAnnouncementsLabel = _i18n.T("setup.launcher_misc.fields.announcements"),
                MaxAnimationFpsLabel = _i18n.T("setup.launcher_misc.fields.max_animation_fps"),
                RealTimeLogLinesLabel = _i18n.T("setup.launcher_misc.fields.realtime_log_lines"),
                DisableHardwareAccelerationLabel = _i18n.T("setup.launcher_misc.flags.disable_hardware_acceleration"),
                ExportSettingsButton = _i18n.T("setup.launcher_misc.actions.export_settings"),
                ImportSettingsButton = _i18n.T("setup.launcher_misc.actions.import_settings"),
                NetworkCardHeader = _i18n.T("setup.launcher_misc.cards.network.header"),
                EnableDoHLabel = _i18n.T("setup.launcher_misc.flags.enable_doh"),
                HttpProxyLabel = _i18n.T("setup.launcher_misc.fields.http_proxy"),
                NoProxyLabel = _i18n.T("setup.launcher_misc.options.http_proxy.none"),
                SystemProxyLabel = _i18n.T("setup.launcher_misc.options.http_proxy.system"),
                CustomProxyLabel = _i18n.T("setup.launcher_misc.options.http_proxy.custom"),
                CustomProxyWarning = _i18n.T("setup.launcher_misc.http_proxy.warning"),
                HttpProxyAddressWatermark = _i18n.T("setup.launcher_misc.http_proxy.address_watermark"),
                HttpProxyUsernameLabel = _i18n.T("setup.launcher_misc.http_proxy.username"),
                HttpProxyValueWatermark = _i18n.T("setup.launcher_misc.http_proxy.value_watermark"),
                HttpProxyPasswordLabel = _i18n.T("setup.launcher_misc.http_proxy.password"),
                ApplyProxyButton = _i18n.T("setup.launcher_misc.actions.apply_proxy"),
                DebugCardHeader = _i18n.T("setup.launcher_misc.cards.debug.header"),
                DebugAnimationSpeedLabel = _i18n.T("setup.launcher_misc.fields.debug_animation_speed"),
                SkipCopyDuringDownloadLabel = _i18n.T("setup.launcher_misc.flags.skip_copy_during_download"),
                DebugModeLabel = _i18n.T("setup.launcher_misc.flags.debug_mode"),
                DebugDelayLabel = _i18n.T("setup.launcher_misc.flags.debug_delay"),
                SystemActivityOptions =
                [
                    _i18n.T("setup.launcher_misc.options.system_activity.all"),
                    _i18n.T("setup.launcher_misc.options.system_activity.important_only"),
                    _i18n.T("setup.launcher_misc.options.system_activity.disabled")
                ]
            }
        };
    }

    private void RaiseSectionBLocalizedProperties()
    {
        RaisePropertyChanged(nameof(SetupText));
        RaisePropertyChanged(nameof(LaunchIsolationOptions));
        RaisePropertyChanged(nameof(LaunchVisibilityOptions));
        RaisePropertyChanged(nameof(LaunchPriorityOptions));
        RaisePropertyChanged(nameof(LaunchWindowTypeOptions));
        RaisePropertyChanged(nameof(LaunchMicrosoftAuthOptions));
        RaisePropertyChanged(nameof(LaunchPreferredIpStackOptions));
        RaisePropertyChanged(nameof(LaunchRendererOptions));
        RaisePropertyChanged(nameof(UpdateChannelOptions));
        RaisePropertyChanged(nameof(UpdateModeOptions));
        RaisePropertyChanged(nameof(DarkModeOptions));
        RaisePropertyChanged(nameof(ThemeColorOptions));
        RaisePropertyChanged(nameof(FontOptions));
        RaisePropertyChanged(nameof(CustomThemeColorInputHint));
        RaisePropertyChanged(nameof(DownloadSourceOptions));
        RaisePropertyChanged(nameof(FileNameFormatOptions));
        RaisePropertyChanged(nameof(ModLocalNameStyleOptions));
        RaisePropertyChanged(nameof(SystemActivityOptions));
        RaisePropertyChanged(nameof(BackgroundSuitOptions));
        RaisePropertyChanged(nameof(DownloadTimeoutLabel));
        RaisePropertyChanged(nameof(DebugAnimationSpeedLabel));
        RaisePropertyChanged(nameof(BackgroundBlurLabel));
        RaisePropertyChanged(nameof(BackgroundCardHeader));
    }
}

internal sealed class SetupLocalizationCatalog
{
    public required SetupLaunchLocalization Launch { get; init; }

    public required SetupUiLocalization Ui { get; init; }

    public required SetupGameManageLocalization GameManage { get; init; }

    public required SetupAboutLocalization About { get; init; }

    public required SetupLogLocalization Log { get; init; }

    public required SetupFeedbackLocalization Feedback { get; init; }

    public required SetupUpdateLocalization Update { get; init; }

    public required SetupJavaLocalization Java { get; init; }

    public required SetupLauncherMiscLocalization LauncherMisc { get; init; }
}

internal sealed class SetupLaunchLocalization
{
    public required string OptionsCardHeader { get; init; }
    public required string DefaultIsolationLabel { get; init; }
    public required string GameWindowTitleLabel { get; init; }
    public required string CustomInfoLabel { get; init; }
    public required string LauncherVisibilityLabel { get; init; }
    public required string ProcessPriorityLabel { get; init; }
    public required string WindowSizeLabel { get; init; }
    public required string MicrosoftAuthLabel { get; init; }
    public required string PreferredIpStackLabel { get; init; }
    public required string MemoryCardHeader { get; init; }
    public required string Memory32BitWarning { get; init; }
    public required string MemoryOverallocationWarning { get; init; }
    public required string AutomaticAllocationLabel { get; init; }
    public required string AutomaticAllocationDescription { get; init; }
    public required string CustomAllocationLabel { get; init; }
    public required string OptimizeBeforeLaunchLabel { get; init; }
    public required string AdvancedCardHeader { get; init; }
    public required string RendererLabel { get; init; }
    public required string JvmArgumentsLabel { get; init; }
    public required string GameArgumentsLabel { get; init; }
    public required string BeforeCommandLabel { get; init; }
    public required string EnvironmentVariablesLabel { get; init; }
    public required string EnvironmentVariablesHint { get; init; }
    public required string WaitForBeforeCommandLabel { get; init; }
    public required string LinuxDisplayBackendLabel { get; init; }
    public required string ForceX11Label { get; init; }
    public required string ForceX11Hint { get; init; }
    public required string DisableJavaLaunchWrapperLabel { get; init; }
    public required string DisableRetroWrapperLabel { get; init; }
    public required string RequireDedicatedGpuLabel { get; init; }
    public required string UseJavaExecutableLabel { get; init; }
    public required string InstanceSettingsHint { get; init; }
    public required string InstanceSettingsButton { get; init; }
    public required IReadOnlyList<string> IsolationOptions { get; init; }
    public required IReadOnlyList<string> VisibilityOptions { get; init; }
    public required IReadOnlyList<string> PriorityOptions { get; init; }
    public required IReadOnlyList<string> WindowTypeOptions { get; init; }
    public required IReadOnlyList<string> MicrosoftAuthOptions { get; init; }
    public required IReadOnlyList<string> PreferredIpStackOptions { get; init; }
    public required IReadOnlyList<string> RendererOptions { get; init; }
}

internal sealed class SetupUiLocalization
{
    public required string BasicCardHeader { get; init; }
    public required string LocaleLabel { get; init; }
    public required string LauncherOpacityLabel { get; init; }
    public required string ThemeLabel { get; init; }
    public required string LightPaletteLabel { get; init; }
    public required string DarkPaletteLabel { get; init; }
    public required string LightCustomLabel { get; init; }
    public required string DarkCustomLabel { get; init; }
    public required string CustomThemeHint { get; init; }
    public required string ThemeRefreshHint { get; init; }
    public required string ThemeSwitchUnsupportedNotice { get; init; }
    public required string ThemeSwitchUnsupportedAction { get; init; }
    public required string ShowLauncherLogoLabel { get; init; }
    public required string LockWindowSizeLabel { get; init; }
    public required string ShowLaunchingHintLabel { get; init; }
    public required string FontCardHeader { get; init; }
    public required string GlobalFontLabel { get; init; }
    public required string MotdFontLabel { get; init; }
    public required string BackgroundAdaptLabel { get; init; }
    public required string BackgroundOpacityLabel { get; init; }
    public required string BackgroundBlurLabel { get; init; }
    public required string BackgroundColorfulLabel { get; init; }
    public required string BackgroundOpenFolderButton { get; init; }
    public required string BackgroundRefreshButton { get; init; }
    public required string BackgroundClearButton { get; init; }
    public required string MusicCardHeader { get; init; }
    public required string MusicVolumeLabel { get; init; }
    public required string MusicRandomLabel { get; init; }
    public required string MusicAutoStartLabel { get; init; }
    public required string MusicStartOnLaunchLabel { get; init; }
    public required string MusicStopOnLaunchLabel { get; init; }
    public required string MusicEnableSmtcLabel { get; init; }
    public required string MusicOpenFolderButton { get; init; }
    public required string MusicRefreshButton { get; init; }
    public required string MusicClearButton { get; init; }
    public required string TitleBarCardHeader { get; init; }
    public required string LogoTypeNoneLabel { get; init; }
    public required string LogoTypeDefaultLabel { get; init; }
    public required string LogoTypeTextLabel { get; init; }
    public required string LogoTypeImageLabel { get; init; }
    public required string LogoAlignLeftLabel { get; init; }
    public required string LogoTextLabel { get; init; }
    public required string ChangeLogoImageButton { get; init; }
    public required string ClearLogoImageButton { get; init; }
    public required string HomepageCardHeader { get; init; }
    public required string HomepageTypeBlankLabel { get; init; }
    public required string HomepageTypePresetLabel { get; init; }
    public required string HomepageTypeLocalLabel { get; init; }
    public required string HomepageTypeRemoteLabel { get; init; }
    public required string HomepagePresetNotice { get; init; }
    public required string HomepageSecurityWarning { get; init; }
    public required string HomepageRefreshButton { get; init; }
    public required string HomepageGenerateTutorialButton { get; init; }
    public required string HomepageViewTutorialButton { get; init; }
    public required string HomepageUrlLabel { get; init; }
    public required string HomepagePresetLabel { get; init; }
    public required string HiddenFeaturesHint { get; init; }
    public required IReadOnlyList<string> DarkModeOptions { get; init; }
    public required IReadOnlyList<string> ThemeColorOptions { get; init; }
    public required IReadOnlyList<string> FontOptions { get; init; }
    public required IReadOnlyList<string> BackgroundSuitOptions { get; init; }
}

internal sealed class SetupGameManageLocalization
{
    public required string GameResourcesCardHeader { get; init; }
    public required string FileDownloadSourceLabel { get; init; }
    public required string VersionListSourceLabel { get; init; }
    public required string MaxThreadCountLabel { get; init; }
    public required string SpeedLimitLabel { get; init; }
    public required string DownloadTimeoutLabel { get; init; }
    public required string TargetFolderLabel { get; init; }
    public required string TargetFolderHint { get; init; }
    public required string InstallBehaviorLabel { get; init; }
    public required string AutoSelectNewInstanceLabel { get; init; }
    public required string UpgradePartialAuthlibLabel { get; init; }
    public required string CommunityResourcesCardHeader { get; init; }
    public required string CommunityDownloadSourceLabel { get; init; }
    public required string FileNameFormatLabel { get; init; }
    public required string ModLocalNameStyleLabel { get; init; }
    public required string HideQuiltLoaderLabel { get; init; }
    public required string AccessibilityCardHeader { get; init; }
    public required string GameUpdateNotificationsLabel { get; init; }
    public required string ReleaseUpdateNotificationsLabel { get; init; }
    public required string SnapshotUpdateNotificationsLabel { get; init; }
    public required string GameLanguageLabel { get; init; }
    public required string AutoSwitchGameLanguageLabel { get; init; }
    public required string DetectClipboardLinksLabel { get; init; }
    public required IReadOnlyList<string> DownloadSourceOptions { get; init; }
    public required IReadOnlyList<string> FileNameFormatOptions { get; init; }
    public required IReadOnlyList<string> ModLocalNameStyleOptions { get; init; }
}

internal sealed class SetupAboutLocalization
{
    public required string AboutCardHeader { get; init; }
    public required string AcknowledgementsCardHeader { get; init; }
}

internal sealed class SetupLogLocalization
{
    public required string ActionsCardHeader { get; init; }
    public required string ExportLogButton { get; init; }
    public required string ExportAllLogsButton { get; init; }
    public required string OpenDirectoryButton { get; init; }
    public required string CleanHistoryButton { get; init; }
    public required string AllLogsCardHeader { get; init; }
}

internal sealed class SetupFeedbackLocalization
{
    public required string SubmitCardHeader { get; init; }
    public required string SubmitHint { get; init; }
    public required string OpenFeedbackButton { get; init; }
    public required string LoadingSectionTitle { get; init; }
    public required string LoadingEntryTitle { get; init; }
    public required string LoadingEntrySummary { get; init; }
    public required string LoadFailedSectionTitle { get; init; }
    public required string LoadFailedEntryTitle { get; init; }
}

internal sealed class SetupUpdateLocalization
{
    public required string UpdateChannelLabel { get; init; }
    public required string UpdateModeLabel { get; init; }
    public required string DownloadAndInstallButton { get; init; }
    public required string MoreInfoButton { get; init; }
    public required string CheckAgainButton { get; init; }
    public required string ViewChangelogButton { get; init; }
    public required IReadOnlyList<string> ChannelOptions { get; init; }
    public required IReadOnlyList<string> ModeOptions { get; init; }
}

internal sealed class SetupJavaLocalization
{
    public required string AddButton { get; init; }
    public required string AutoSelectLabel { get; init; }
    public required string AutoSelectDescription { get; init; }
    public required string OpenButton { get; init; }
    public required string DetailsButton { get; init; }
}

internal sealed class SetupLauncherMiscLocalization
{
    public required string SystemCardHeader { get; init; }
    public required string LauncherAnnouncementsLabel { get; init; }
    public required string MaxAnimationFpsLabel { get; init; }
    public required string RealTimeLogLinesLabel { get; init; }
    public required string DisableHardwareAccelerationLabel { get; init; }
    public required string ExportSettingsButton { get; init; }
    public required string ImportSettingsButton { get; init; }
    public required string NetworkCardHeader { get; init; }
    public required string EnableDoHLabel { get; init; }
    public required string HttpProxyLabel { get; init; }
    public required string NoProxyLabel { get; init; }
    public required string SystemProxyLabel { get; init; }
    public required string CustomProxyLabel { get; init; }
    public required string CustomProxyWarning { get; init; }
    public required string HttpProxyAddressWatermark { get; init; }
    public required string HttpProxyUsernameLabel { get; init; }
    public required string HttpProxyValueWatermark { get; init; }
    public required string HttpProxyPasswordLabel { get; init; }
    public required string ApplyProxyButton { get; init; }
    public required string DebugCardHeader { get; init; }
    public required string DebugAnimationSpeedLabel { get; init; }
    public required string SkipCopyDuringDownloadLabel { get; init; }
    public required string DebugModeLabel { get; init; }
    public required string DebugDelayLabel { get; init; }
    public required IReadOnlyList<string> SystemActivityOptions { get; init; }
}
