using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Core.Logging;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private enum BackgroundContentRefreshMode
    {
        None,
        Immediate,
        Deferred
    }

    private void InitializeAboutEntries()
    {
        ReplaceItems(AboutProjectEntries,
        [
            new AboutEntryViewModel(
                _i18n.T("setup.about.project.original_author.title"),
                _i18n.T("setup.about.project.original_author.summary"),
                LoadLauncherBitmap("Images", "Heads", "LTCat.jpg"),
                _i18n.T("setup.about.project.original_author.action"),
                CreateLinkCommand(_i18n.T("setup.about.project.original_author.command"), "https://ifdian.net/a/LTCat")),
            new AboutEntryViewModel(
                _i18n.T("setup.about.project.community.title"),
                _i18n.T("setup.about.project.community.summary"),
                LoadLauncherBitmap("Images", "Heads", "PCL-Community.png"),
                _i18n.T("setup.about.project.community.action"),
                CreateLinkCommand(_i18n.T("setup.about.project.community.command"), "https://github.com/PCL-Community")),
            new AboutEntryViewModel(
                _i18n.T("setup.about.project.repo.title"),
                _setupComposition.About.LauncherVersionSummary,
                LoadLauncherBitmap("Images", "Heads", "Logo-CE.png"),
                _i18n.T("setup.about.project.repo.action"),
                CreateLinkCommand(_i18n.T("setup.about.project.repo.command"), "https://github.com/TheUnknownThing/PCL-ME"))
        ]);

        ReplaceItems(AboutAcknowledgementEntries,
        [
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.bangbang93.title"), _i18n.T("setup.about.acknowledgements.bangbang93.summary"), LoadLauncherBitmap("Images", "Heads", "bangbang93.png"), _i18n.T("setup.about.acknowledgements.bangbang93.action"), CreateLinkCommand(_i18n.T("setup.about.acknowledgements.bangbang93.command"), "https://afdian.com/a/bangbang93")),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.mcmod.title"), _i18n.T("setup.about.acknowledgements.mcmod.summary"), LoadLauncherBitmap("Images", "Heads", "wiki.png"), _i18n.T("setup.about.acknowledgements.mcmod.action"), CreateLinkCommand(_i18n.T("setup.about.acknowledgements.mcmod.command"), "https://www.mcmod.cn")),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.pysio.title"), _i18n.T("setup.about.acknowledgements.pysio.summary"), LoadLauncherBitmap("Images", "Heads", "Pysio.jpg"), _i18n.T("setup.about.acknowledgements.pysio.action"), CreateLinkCommand(_i18n.T("setup.about.acknowledgements.pysio.command"), "https://www.pysio.online")),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.yunmoan.title"), _i18n.T("setup.about.acknowledgements.yunmoan.summary"), LoadLauncherBitmap("Images", "Heads", "Yunmoan.jpg"), _i18n.T("setup.about.acknowledgements.yunmoan.action"), CreateLinkCommand(_i18n.T("setup.about.acknowledgements.yunmoan.command"), "https://www.zyghit.cn")),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.z0z0r4.title"), _i18n.T("setup.about.acknowledgements.z0z0r4.summary"), LoadLauncherBitmap("Images", "Heads", "z0z0r4.png"), null, null),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.00ll00.title"), _i18n.T("setup.about.acknowledgements.00ll00.summary"), LoadLauncherBitmap("Images", "Heads", "00ll00.png"), null, null),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.patrick.title"), _i18n.T("setup.about.acknowledgements.patrick.summary"), LoadLauncherBitmap("Images", "Heads", "Patrick.png"), null, null),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.hao_tian.title"), _i18n.T("setup.about.acknowledgements.hao_tian.summary"), LoadLauncherBitmap("Images", "Heads", "Hao_Tian.jpg"), null, null),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.mcbbs.title"), _i18n.T("setup.about.acknowledgements.mcbbs.summary"), LoadLauncherBitmap("Images", "Heads", "MCBBS.png"), null, null),
            new AboutEntryViewModel(_i18n.T("setup.about.acknowledgements.community.title"), _i18n.T("setup.about.acknowledgements.community.summary"), LoadLauncherBitmap("Images", "Heads", "PCL2.png"), null, null)
        ]);
    }

    private void InitializeLogEntries()
    {
        ReplaceItems(LogEntries,
            _setupComposition.Log.Entries.Select(entry =>
                CreateSimpleEntry(
                    entry.Title,
                    entry.Summary,
                    CreateOpenTargetCommand(
                        _i18n.T(
                            "setup.log.activities.open_log",
                            new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["title"] = entry.Title
                            }),
                        entry.Path,
                        entry.Summary))));
    }

    private void InitializeUpdateSurface()
    {
        _selectedUpdateChannelIndex = Math.Clamp(_setupComposition.Update.UpdateChannelIndex, 0, UpdateChannelOptions.Count - 1);
        _selectedUpdateModeIndex = Math.Clamp(_setupComposition.Update.UpdateModeIndex, 0, UpdateModeOptions.Count - 1);
    }

    private void InitializeLaunchSettingsSurface()
    {
        _selectedLaunchIsolationIndex = _setupComposition.Launch.IsolationIndex;
        _launchWindowTitle = _setupComposition.Launch.WindowTitle;
        _launchCustomInfo = _setupComposition.Launch.CustomInfo;
        _selectedLaunchVisibilityIndex = _setupComposition.Launch.VisibilityIndex;
        _selectedLaunchPriorityIndex = _setupComposition.Launch.PriorityIndex;
        _selectedLaunchWindowTypeIndex = _setupComposition.Launch.WindowTypeIndex;
        _launchWindowWidth = _setupComposition.Launch.WindowWidth;
        _launchWindowHeight = _setupComposition.Launch.WindowHeight;
        _useAutomaticRamAllocation = _setupComposition.Launch.UseAutomaticRamAllocation;
        _customRamAllocation = _setupComposition.Launch.CustomRamAllocationGb;
        var (totalMemoryGb, availableMemoryGb) = FrontendSystemMemoryService.GetPhysicalMemoryState();
        _launchTotalRamGb = totalMemoryGb;
        _launchUsedRamGb = Math.Max(totalMemoryGb - availableMemoryGb, 0);
        _launchAutomaticAllocatedRamGb = FrontendSystemMemoryService.CalculateAllocatedMemoryGb(
            0,
            _customRamAllocation,
            isModable: false,
            hasOptiFine: false,
            modCount: 0,
            is64BitJava: null,
            totalMemoryGb,
            availableMemoryGb);
        _isLaunchAdvancedOptionsExpanded = false;
        _selectedLaunchRendererIndex = _setupComposition.Launch.RendererIndex;
        _launchWrapperCommand = _setupComposition.Launch.WrapperCommand;
        _launchJvmArguments = _setupComposition.Launch.JvmArguments;
        _launchGameArguments = _setupComposition.Launch.GameArguments;
        _launchBeforeCommand = _setupComposition.Launch.BeforeCommand;
        _launchEnvironmentVariables = _setupComposition.Launch.EnvironmentVariables;
        _waitForLaunchBeforeCommand = _setupComposition.Launch.WaitForBeforeCommand;
        _forceX11OnWaylandForLaunch = _setupComposition.Launch.ForceX11OnWayland;
        _disableJavaLaunchWrapper = _setupComposition.Launch.DisableJavaLaunchWrapper;
        _disableRetroWrapper = _setupComposition.Launch.DisableRetroWrapper;
        _requireDedicatedGpu = _setupComposition.Launch.RequireDedicatedGpu;
        _useJavaExecutable = _setupComposition.Launch.UseJavaExecutable;
        _selectedLaunchPreferredIpStackIndex = _setupComposition.Launch.PreferredIpStackIndex;
    }

    private void InitializeToolsTestSurface()
    {
        var testState = _toolsComposition.Test;
        AchievementPreviewImage = null;
        HeadPreviewImage = null;
        _toolDownloadUrl = testState.DownloadUrl;
        _toolDownloadUserAgent = testState.DownloadUserAgent;
        _toolDownloadFolder = testState.DownloadFolder;
        _toolDownloadName = testState.DownloadName;
        _officialSkinPlayerName = testState.OfficialSkinPlayerName;
        _achievementBlockId = testState.AchievementBlockId;
        _achievementTitle = testState.AchievementTitle;
        _achievementFirstLine = testState.AchievementFirstLine;
        _achievementSecondLine = testState.AchievementSecondLine;
        _showAchievementPreview = testState.ShowAchievementPreview;
        _selectedHeadSizeIndex = testState.SelectedHeadSizeIndex;
        _selectedHeadSkinPath = testState.SelectedHeadSkinPath;
        if (_showAchievementPreview)
        {
            _showAchievementPreview = false;
        }

        RefreshHeadPreviewFromSelection(addActivity: false);

        ReplaceItems(ToolboxActions, testState.ToolboxActions.Select(CreateToolboxAction));
        InitializeMinecraftServerQuerySurface();
    }

    private void InitializeDownloadInstallSurface()
    {
        if (!_downloadInstallIsInSelectionStage && !_downloadInstallIsNameEditedByUser)
        {
            _downloadInstallName = T("download.install.generated_name.default");
        }

        RefreshDownloadInstallSurfaceState();
    }

    private void RefreshDownloadInstallSurface()
    {
        if (!IsCurrentStandardRightPane(StandardRightPaneKind.DownloadInstall))
        {
            return;
        }

        SyncDownloadInstallRouteState();
        InitializeDownloadInstallSurface();
    }

    private void RefreshDownloadCatalogSurface()
    {
        _downloadCatalogRefreshCts?.Cancel();
        DownloadCatalogIntroTitle = string.Empty;
        DownloadCatalogIntroBody = string.Empty;
        DownloadCatalogLoadingText = LocalizeDownloadCatalogLoadingText(
            _currentRoute.Subpage,
            FrontendDownloadRemoteCatalogService.GetLoadingText(_currentRoute.Subpage, _i18n));
        ReplaceItems(DownloadCatalogIntroActions, []);
        ReplaceItems(DownloadCatalogSections, []);
        SetDownloadCatalogLoading(false);

        if (!IsCurrentStandardRightPane(StandardRightPaneKind.DownloadCatalog))
        {
            return;
        }

        var refreshVersion = ++_downloadCatalogRefreshVersion;
        var route = _currentRoute.Subpage;
        var cts = new CancellationTokenSource();
        _downloadCatalogRefreshCts = cts;
        SetDownloadCatalogLoading(true);
        _ = LoadDownloadCatalogSurfaceAsync(route, refreshVersion, cts.Token);
    }

    private async Task LoadDownloadCatalogSurfaceAsync(
        LauncherFrontendSubpageKey route,
        int refreshVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var state = await FrontendDownloadCompositionService.LoadCatalogStateAsync(
                _launcherActionService.RuntimePaths,
                _instanceComposition,
                route,
                _i18n,
                cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested
                    || refreshVersion != _downloadCatalogRefreshVersion
                    || _currentRoute.Subpage != route
                    || !IsCurrentStandardRightPane(StandardRightPaneKind.DownloadCatalog))
                {
                    return;
                }

                ApplyDownloadCatalogState(state);
                SetDownloadCatalogLoading(false);
            });
        }
        catch (OperationCanceledException)
        {
            // A newer route refresh superseded this load.
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (refreshVersion != _downloadCatalogRefreshVersion)
                {
                    return;
                }

                DownloadCatalogIntroTitle = T("download.catalog.errors.load_failed_title");
                DownloadCatalogIntroBody = T("download.catalog.errors.load_failed_body", ("error", ex.Message));
                DownloadCatalogLoadingText = T("download.catalog.errors.retry_later");
                ReplaceItems(DownloadCatalogIntroActions, []);
                ReplaceItems(DownloadCatalogSections, BuildDownloadCatalogErrorSections(ex.Message));
                SetDownloadCatalogLoading(false);
            });
        }
    }

    private void ApplyDownloadCatalogState(FrontendDownloadCatalogState state)
    {
        if (!string.IsNullOrWhiteSpace(state.LoadError))
        {
            DownloadCatalogLoadingText = T("download.catalog.errors.retry_later");
            SetDownloadCatalogIntro(
                T("download.catalog.errors.load_failed_title"),
                T("download.catalog.errors.load_failed_body", ("error", state.LoadError)),
                state.Actions.Select(action =>
                    new DownloadCatalogActionViewModel(
                        LocalizeDownloadCatalogActionText(action.Text),
                        action.IsHighlight ? PclButtonColorState.Highlight : PclButtonColorState.Normal,
                        string.IsNullOrWhiteSpace(action.Target)
                            ? CreateIntentCommand(LocalizeDownloadCatalogActionText(action.Text), state.LoadError)
                            : CreateOpenTargetCommand(LocalizeDownloadCatalogActionText(action.Text), action.Target, action.Target))).ToArray());
            ReplaceItems(DownloadCatalogSections, BuildDownloadCatalogErrorSections(state.LoadError));
            return;
        }

        var localizedIntroTitle = LocalizeDownloadCatalogIntroTitle(_currentRoute.Subpage, state.IntroTitle);
        var localizedIntroBody = LocalizeDownloadCatalogIntroBody(_currentRoute.Subpage, state.IntroBody, state.StaleError);
        DownloadCatalogLoadingText = LocalizeDownloadCatalogLoadingText(_currentRoute.Subpage, state.LoadingText);
        SetDownloadCatalogIntro(
            localizedIntroTitle,
            localizedIntroBody,
            state.Actions.Select(action =>
                new DownloadCatalogActionViewModel(
                    LocalizeDownloadCatalogActionText(action.Text),
                    action.IsHighlight ? PclButtonColorState.Highlight : PclButtonColorState.Normal,
                    string.IsNullOrWhiteSpace(action.Target)
                        ? CreateIntentCommand(LocalizeDownloadCatalogActionText(action.Text), localizedIntroTitle)
                        : CreateOpenTargetCommand(LocalizeDownloadCatalogActionText(action.Text), action.Target, action.Target))).ToArray());
        ReplaceItems(
            DownloadCatalogSections,
            state.Sections.Select(section => CreateDownloadCatalogSectionViewModel(_currentRoute.Subpage, section)));
    }

    private void SetDownloadCatalogLoading(bool isLoading)
    {
        if (_isDownloadCatalogLoading == isLoading)
        {
            return;
        }

        _isDownloadCatalogLoading = isLoading;
        RaisePropertyChanged(nameof(ShowDownloadCatalogLoadingCard));
        RaisePropertyChanged(nameof(ShowDownloadCatalogContent));
    }

    private IReadOnlyList<DownloadCatalogSectionViewModel> BuildDownloadCatalogErrorSections(string error)
    {
        return
        [
            CreateDownloadCatalogSection(
                T("download.catalog.errors.section_title"),
                [
                    new DownloadCatalogEntryViewModel(
                        T("download.catalog.errors.empty_title"),
                        T("download.catalog.errors.empty_description"),
                        string.Empty,
                        T("resource_detail.actions.view_details"),
                        CreateIntentCommand(T("download.catalog.errors.load_failed_title"), error))
                ])
        ];
    }

    private void RefreshDownloadFavoriteSurface()
    {
        ReplaceItems(DownloadFavoriteSections, []);

        if (!IsCurrentStandardRightPane(StandardRightPaneKind.DownloadFavorites))
        {
            return;
        }

        EnsureDownloadCompositionRemoteStateLoaded();
        ShowDownloadFavoriteWarning = _downloadComposition.Favorites.ShowWarning;
        DownloadFavoriteWarningText = _downloadComposition.Favorites.WarningText;
        var selectedTarget = GetSelectedDownloadFavoriteTargetState();
        SyncDownloadFavoriteSelectionTarget(selectedTarget.Id);
        var visibleEntries = selectedTarget.Sections
            .Select(section => new
            {
                Title = LocalizeDownloadFavoriteSectionTitle(section.Title),
                Entries = section.Entries
                    .Select(LocalizeDownloadFavoriteEntry)
                    .ToArray()
            })
            .Select(section => new DownloadFavoriteSectionViewModel(
                section.Title,
                section.Entries
                    .Where(item =>
                        string.IsNullOrWhiteSpace(DownloadFavoriteSearchQuery)
                        || item.Title.Contains(DownloadFavoriteSearchQuery, StringComparison.OrdinalIgnoreCase)
                        || item.Info.Contains(DownloadFavoriteSearchQuery, StringComparison.OrdinalIgnoreCase)
                        || item.Meta.Contains(DownloadFavoriteSearchQuery, StringComparison.OrdinalIgnoreCase))
                    .Select(CreateDownloadFavoriteEntryViewModel)
                    .ToArray()))
            .Where(section => section.Items.Count > 0)
            .ToArray();

        _downloadFavoriteSelectedProjectIds.IntersectWith(
            visibleEntries
                .SelectMany(section => section.Items)
                .Select(entry => entry.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path)));

        ReplaceItems(DownloadFavoriteSections, visibleEntries);
        RaisePropertyChanged(nameof(HasDownloadFavoriteSections));
        RaisePropertyChanged(nameof(HasNoDownloadFavoriteSections));
        RaiseDownloadFavoriteSelectionProperties();
    }

    private InstanceResourceEntryViewModel CreateDownloadFavoriteEntryViewModel(FrontendDownloadCatalogEntry entry)
    {
        var projectId = entry.Identity ?? string.Empty;
        var actionCommand = string.IsNullOrWhiteSpace(entry.Target)
            ? CreateIntentCommand(
                T("download.catalog.activities.open_favorite_entry", ("entry_title", entry.Title)),
                entry.Info)
            : CreateDownloadFavoriteCommand(entry);
        var icon = LoadCachedBitmapFromPath(entry.IconPath);
        if (icon is null && !string.IsNullOrWhiteSpace(entry.IconName))
        {
            icon = LoadLauncherBitmap("Images", "Blocks", entry.IconName);
        }

        var viewModel = new InstanceResourceEntryViewModel(
            icon: icon,
            title: entry.Title,
            info: entry.Info,
            meta: entry.Meta,
            path: projectId,
            actionCommand: actionCommand,
            actionToolTip: T("resource_detail.actions.view_details"),
            isEnabled: true,
            description: entry.Info,
            showSelection: true,
            isSelected: !string.IsNullOrWhiteSpace(projectId) && _downloadFavoriteSelectedProjectIds.Contains(projectId),
            selectionChanged: isSelected => HandleDownloadFavoriteSelectionChanged(projectId, isSelected),
            infoCommand: actionCommand,
            deleteCommand: string.IsNullOrWhiteSpace(projectId)
                ? null
                : new ActionCommand(() => _ = RemoveDownloadFavoriteAsync(projectId, entry.Title)));
        QueueDownloadFavoriteIconLoad(viewModel, entry.IconUrl);
        return viewModel;
    }

    private FrontendDownloadCatalogEntry LocalizeDownloadFavoriteEntry(FrontendDownloadCatalogEntry entry)
    {
        return entry with
        {
            Meta = LocalizeDownloadFavoriteMeta(entry.Meta)
        };
    }

    private string LocalizeDownloadFavoriteSectionTitle(string value)
    {
        return value switch
        {
            "other" => T("resource_detail.values.other"),
            _ => LocalizeCommunityProjectProjectType(value)
        };
    }

    private string LocalizeDownloadFavoriteMeta(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith("favorite_meta|", StringComparison.Ordinal))
        {
            return value;
        }

        var segments = value
            .Split('|')
            .Skip(1)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        var parts = new List<string>();
        var targetName = segments.ElementAtOrDefault(0);
        if (!string.IsNullOrWhiteSpace(targetName))
        {
            parts.Add(targetName);
        }

        var source = segments.ElementAtOrDefault(1);
        if (!string.IsNullOrWhiteSpace(source))
        {
            parts.Add(source);
        }

        var projectType = LocalizeCommunityProjectProjectType(segments.ElementAtOrDefault(2) ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(projectType))
        {
            parts.Add(projectType);
        }

        var author = segments.ElementAtOrDefault(3);
        if (!string.IsNullOrWhiteSpace(author))
        {
            parts.Add(author);
        }

        var updatedLabel = segments.ElementAtOrDefault(4);
        if (!string.IsNullOrWhiteSpace(updatedLabel) && !string.Equals(updatedLabel, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(T("download.favorites.meta.updated", ("value", LocalizeCommunityProjectUpdatedLabel(updatedLabel))));
        }

        if (int.TryParse(segments.ElementAtOrDefault(5), out var downloadCount) && downloadCount > 0)
        {
            parts.Add(T("download.favorites.meta.downloads", ("value", FormatCompactCount(downloadCount))));
        }

        return string.Join(" • ", parts);
    }

    private void InitializeGameManageSurface()
    {
        _selectedDownloadSourceIndex = _setupComposition.GameManage.DownloadSourceIndex;
        _selectedVersionSourceIndex = _setupComposition.GameManage.VersionSourceIndex;
        _downloadThreadLimit = _setupComposition.GameManage.DownloadThreadLimit;
        _downloadSpeedLimit = _setupComposition.GameManage.DownloadSpeedLimit;
        _downloadTimeoutSeconds = _setupComposition.GameManage.DownloadTimeoutSeconds;
        _autoSelectNewInstance = _setupComposition.GameManage.AutoSelectNewInstance;
        _selectedCommunityDownloadSourceIndex = _setupComposition.GameManage.CommunityDownloadSourceIndex;
        _selectedFileNameFormatIndex = _setupComposition.GameManage.FileNameFormatIndex;
        _selectedModLocalNameStyleIndex = _setupComposition.GameManage.ModLocalNameStyleIndex;
        _ignoreQuiltLoader = _setupComposition.GameManage.IgnoreQuiltLoader;
        _autoSwitchGameLanguageToChinese = _setupComposition.GameManage.AutoSwitchGameLanguageToChinese;
        _detectClipboardResourceLinks = _setupComposition.GameManage.DetectClipboardResourceLinks;
    }

    private void InitializeGlobalSetupSettings()
    {
        _ignoreQuiltLoader = _setupComposition.GameManage.IgnoreQuiltLoader;
        _maxRealTimeLogValue = _setupComposition.LauncherMisc.MaxRealTimeLogValue;
        _debugModeEnabled = _setupComposition.LauncherMisc.DebugModeEnabled;
        ApplyLaunchLogRetentionPreference();
        RefreshDebugModeSurface();
    }

    private void InitializeLauncherMiscSurface()
    {
        _selectedLauncherLocaleIndex = ResolveLauncherLocaleIndex(_i18n.Locale);
        _selectedSystemActivityIndex = _setupComposition.LauncherMisc.SystemActivityIndex;
        _maxRealTimeLogValue = _setupComposition.LauncherMisc.MaxRealTimeLogValue;
        _disableHardwareAcceleration = _setupComposition.LauncherMisc.DisableHardwareAcceleration;
        _selectedSecureDnsModeIndex = _setupComposition.LauncherMisc.SecureDnsModeIndex;
        _selectedSecureDnsProviderIndex = _setupComposition.LauncherMisc.SecureDnsProviderIndex;
        _selectedHttpProxyTypeIndex = _setupComposition.LauncherMisc.HttpProxyTypeIndex;
        _httpProxyAddress = _setupComposition.LauncherMisc.HttpProxyAddress;
        _httpProxyUsername = _setupComposition.LauncherMisc.HttpProxyUsername;
        _httpProxyPassword = _setupComposition.LauncherMisc.HttpProxyPassword;
        _proxyTestFeedbackText = string.Empty;
        _isProxyTestFeedbackSuccess = false;
        _debugAnimationSpeed = _setupComposition.LauncherMisc.DebugAnimationSpeed;
        _debugModeEnabled = _setupComposition.LauncherMisc.DebugModeEnabled;
        ApplyLaunchLogRetentionPreference();
        RefreshDebugModeSurface();
        RefreshLaunchAnnouncements();
    }

    private void InitializeJavaSurface()
    {
        _selectedJavaRuntimeKey = _setupComposition.Java.SelectedRuntimeKey;
        ReplaceItems(JavaRuntimeEntries,
            _setupComposition.Java.Entries.Select(entry =>
                CreateJavaRuntimeEntry(
                    entry.Key,
                    entry.Title,
                    entry.Folder,
                    entry.Tags,
                    entry.IsEnabled)));
        SyncJavaSelection();
        LogWrapper.Trace(
            "SetupJava",
            $"InitializeJavaSurface: selected='{_selectedJavaRuntimeKey}', uiEntries={JavaRuntimeEntries.Count}.");
    }

    private void InitializeUiSurface(
        bool refreshFeatureToggleGroups = true,
        BackgroundContentRefreshMode backgroundContentRefreshMode = BackgroundContentRefreshMode.Immediate)
    {
        _selectedLauncherLocaleIndex = ResolveLauncherLocaleIndex(_i18n.Locale);
        _selectedDarkModeIndex = _setupComposition.Ui.DarkModeIndex;
        _selectedLightColorIndex = FrontendAppearanceService.NormalizeThemeColorIndex(_setupComposition.Ui.LightColorIndex, ThemeColorOptions.Count);
        _selectedDarkColorIndex = FrontendAppearanceService.NormalizeThemeColorIndex(_setupComposition.Ui.DarkColorIndex, ThemeColorOptions.Count);
        CustomLightThemeColorHex = _setupComposition.Ui.LightCustomColorHex;
        CustomDarkThemeColorHex = _setupComposition.Ui.DarkCustomColorHex;
        SeedCustomThemeColorIfNeeded(isDarkPalette: false, _selectedLightColorIndex, 0);
        SeedCustomThemeColorIfNeeded(isDarkPalette: true, _selectedDarkColorIndex, 0);
        _launcherOpacity = _setupComposition.Ui.LauncherOpacity;
        _uiScaleFactor = _setupComposition.Ui.UiScaleFactor;
        _selectedUiScaleFactorIndex = FrontendStartupScalingService.ResolveClosestScaleFactorIndex(_uiScaleFactor);
        _showLauncherLogo = _setupComposition.Ui.ShowLauncherLogo;
        _lockWindowSize = _setupComposition.Ui.LockWindowSize;
        _showLaunchingHint = _setupComposition.Ui.ShowLaunchingHint;
        _selectedGlobalFontIndex = _setupComposition.Ui.GlobalFontIndex;
        _selectedMotdFontIndex = _setupComposition.Ui.MotdFontIndex;
        _backgroundColorful = _setupComposition.Ui.BackgroundColorful;
        _backgroundOpacity = _setupComposition.Ui.BackgroundOpacity;
        _backgroundBlur = _setupComposition.Ui.BackgroundBlur;
        _selectedBackgroundSuitIndex = Math.Clamp(_setupComposition.Ui.BackgroundSuitIndex, 0, BackgroundSuitOptions.Count - 1);
        _selectedLogoTypeIndex = _setupComposition.Ui.LogoTypeIndex;
        _logoAlignLeft = _setupComposition.Ui.LogoAlignLeft;
        _logoText = _setupComposition.Ui.LogoText;
        _selectedHomepageTypeIndex = _setupComposition.Ui.HomepageTypeIndex;
        _homepageUrl = _setupComposition.Ui.HomepageUrl;
        _selectedHomepagePresetIndex = Math.Clamp(_setupComposition.Ui.HomepagePresetIndex, 0, HomepagePresetOptions.Count - 1);
        RefreshTitleBarLogoImage();
        RefreshLaunchHomepage(forceRefresh: false);
        if (refreshFeatureToggleGroups)
        {
            RefreshUiFeatureToggleGroups();
        }

        switch (backgroundContentRefreshMode)
        {
            case BackgroundContentRefreshMode.Immediate:
                RefreshBackgroundContentState(selectNewAsset: _currentBackgroundAssetPath is null, addActivity: false);
                break;
            case BackgroundContentRefreshMode.Deferred:
                QueueBackgroundContentRefresh(selectNewAsset: _currentBackgroundAssetPath is null);
                break;
        }
    }

    private void RefreshUiFeatureToggleGroups()
    {
        ReplaceItems(UiFeatureToggleGroups,
            _setupComposition.Ui.ToggleGroups.Select(group =>
                new UiFeatureToggleGroupViewModel(
                    group.Title,
                    group.Items.Select(item =>
                        new UiFeatureToggleItemViewModel(
                            item.Title,
                            item.IsChecked,
                            isChecked => PersistUiToggle(item.ConfigKey, item.Title, isChecked))).ToArray())));
    }

    private void RefreshHelpTopics()
    {
        var query = HelpSearchQuery.Trim();
        var entries = _toolsComposition.Help.Entries;
        var hasSearchQuery = !string.IsNullOrWhiteSpace(query);

        if (hasSearchQuery)
        {
            var results = entries
                .Where(entry => entry.ShowInSearch && HelpEntryMatchesQuery(entry, query))
                .Select(entry => CreateHelpTopic(entry, entry.GroupTitles.FirstOrDefault() ?? LT("shell.tools.help.default_group")))
                .OrderByDescending(topic => GetHelpSearchScore(topic, query))
                .ThenBy(topic => topic.Title, StringComparer.Ordinal)
                .ToArray();

            ReplaceItems(HelpSearchResults, results);
            ReplaceItems(HelpTopicGroups, []);
        }
        else
        {
            ReplaceItems(HelpSearchResults, []);

            var orderedGroupTitles = entries
                .SelectMany((entry, entryIndex) => entry.GroupTitles.Select(groupTitle => new { GroupTitle = groupTitle, EntryIndex = entryIndex }))
                .Where(item => !string.IsNullOrWhiteSpace(item.GroupTitle))
                .GroupBy(item => item.GroupTitle, StringComparer.Ordinal)
                .Select(group => new { GroupTitle = group.Key, FirstEntryIndex = group.Min(item => item.EntryIndex) })
                .OrderBy(title => IsPrimaryHelpGuideGroup(title.GroupTitle) ? 0 : 1)
                .ThenBy(title => title.FirstEntryIndex)
                .Select(title => title.GroupTitle)
                .ToArray();

            var groups = orderedGroupTitles
                .Select(groupTitle =>
                {
                    var items = entries
                        .Where(entry => entry.GroupTitles.Contains(groupTitle, StringComparer.Ordinal))
                        .Select(entry => CreateHelpTopic(entry, groupTitle))
                        .ToArray();
                    return new HelpTopicGroupViewModel(
                        LocalizeHelpGroupTitle(groupTitle),
                        items,
                        isExpanded: IsPrimaryHelpGuideGroup(groupTitle));
                })
                .Where(group => group.Items.Count > 0)
                .ToArray();

            ReplaceItems(HelpTopicGroups, groups);
        }

        RaisePropertyChanged(nameof(IsHelpSearchActive));
        RaisePropertyChanged(nameof(ShowHelpTopicLibrary));
        RaisePropertyChanged(nameof(HelpSearchResultsHeader));
        RaisePropertyChanged(nameof(ToolsHelpSearchWatermark));
        RaisePropertyChanged(nameof(ToolsHelpNoResultsText));
        RaiseCollectionStateProperties();
    }

    private HelpTopicViewModel CreateHelpTopic(FrontendToolsHelpEntry entry, string groupTitle)
    {
        return new HelpTopicViewModel(
            LocalizeHelpGroupTitle(groupTitle),
            entry.Title,
            entry.Summary,
            entry.Keywords,
            ResolveHelpTopicIcon(entry),
            new ActionCommand(() => OpenHelpTopic(entry)));
    }

    private string LocalizeHelpGroupTitle(string groupTitle)
    {
        return groupTitle switch
        {
            "Guides" => LT("shell.tools.help.groups.guides"),
            "Help" => LT("shell.tools.help.groups.help"),
            "Launcher" => LT("shell.tools.help.groups.launcher"),
            "Personalization" => LT("shell.tools.help.groups.personalization"),
            _ => groupTitle
        };
    }

    private static bool IsPrimaryHelpGuideGroup(string groupTitle)
    {
        return string.Equals(groupTitle, "Guides", StringComparison.OrdinalIgnoreCase);
    }

    private static Bitmap? ResolveHelpTopicIcon(FrontendToolsHelpEntry entry)
    {
        var customIcon = ResolveHelpTopicCustomIcon(entry.Logo);
        if (customIcon is not null)
        {
            return customIcon;
        }

        if (entry.IsEvent)
        {
            return FrontendHelpEventTypeResolver.Resolve(entry.EventType) == FrontendHelpEventType.Popup
                ? LoadLauncherBitmap("Images", "Blocks", "GrassPath.png")
                : LoadLauncherBitmap("Images", "Blocks", "CommandBlock.png");
        }

        return LoadLauncherBitmap("Images", "Blocks", "Grass.png");
    }

    private static Bitmap? ResolveHelpTopicCustomIcon(string? rawLogo)
    {
        if (string.IsNullOrWhiteSpace(rawLogo))
        {
            return null;
        }

        if (Uri.TryCreate(rawLogo, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                return LoadCachedBitmapFromPath(uri.LocalPath);
            }

            if (!string.Equals(uri.Scheme, "pack", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        var normalizedPath = rawLogo
            .Replace("pack://application:,,,/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        if (Path.IsPathRooted(normalizedPath))
        {
            return LoadCachedBitmapFromPath(normalizedPath);
        }

        var pathSegments = normalizedPath
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return pathSegments.Length == 0
            ? null
            : LoadCachedBitmapFromPath(GetLauncherAssetPath(pathSegments));
    }

    private bool HelpEntryMatchesQuery(FrontendToolsHelpEntry entry, string query)
    {
        return entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.GroupTitles.Any(groupTitle => LocalizeHelpGroupTitle(groupTitle).Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetHelpSearchScore(HelpTopicViewModel topic, string query)
    {
        if (topic.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (topic.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (topic.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return topic.GroupTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private FeedbackSectionViewModel CreateFeedbackSection(string title, bool isExpanded, IReadOnlyList<SimpleListEntryViewModel> items)
    {
        return new FeedbackSectionViewModel(title, items, isExpanded);
    }

    private SimpleListEntryViewModel CreateSimpleEntry(string title, string info)
    {
        return new SimpleListEntryViewModel(
            title,
            info,
            new ActionCommand(() => AddActivity(
                T("resource_detail.actions.view_details"),
                info)));
    }

    private SimpleListEntryViewModel CreateSimpleEntry(string title, string info, ActionCommand command)
    {
        return new SimpleListEntryViewModel(title, info, command);
    }

    private ToolboxActionViewModel CreateToolboxAction(string title, string toolTip, double minWidth, PclButtonColorState colorType, ActionCommand command)
    {
        return new ToolboxActionViewModel(title, toolTip, minWidth, colorType, command);
    }

    private SurfaceNoticeViewModel CreateNoticeStrip(
        string text,
        string backgroundResourceKey,
        string backgroundFallback,
        string borderResourceKey,
        string borderFallback,
        string foregroundResourceKey,
        string foregroundFallback,
        Thickness? margin = null)
    {
        return new SurfaceNoticeViewModel(
            text,
            FrontendThemeResourceResolver.GetBrush(backgroundResourceKey, backgroundFallback),
            FrontendThemeResourceResolver.GetBrush(borderResourceKey, borderFallback),
            FrontendThemeResourceResolver.GetBrush(foregroundResourceKey, foregroundFallback),
            margin ?? default);
    }

    private SurfaceNoticeViewModel CreateWarningNoticeStrip(string text, Thickness? margin = null)
    {
        return CreateNoticeStrip(
            text,
            "ColorBrushSemanticWarningBackground",
            "#FFF8DD",
            "ColorBrushSemanticWarningBorder",
            "#F2D777",
            "ColorBrushSemanticWarningForeground",
            "#6E5800",
            margin);
    }

    private SurfaceNoticeViewModel CreateDangerNoticeStrip(string text, Thickness? margin = null)
    {
        return CreateNoticeStrip(
            text,
            "ColorBrushSemanticDangerBackground",
            "#FFF1EA",
            "ColorBrushSemanticDangerBorder",
            "#F1C8B6",
            "ColorBrushSemanticDangerForeground",
            "#A94F2B",
            margin);
    }

    private SurfaceNoticeViewModel CreateSuccessNoticeStrip(string text, Thickness? margin = null)
    {
        return CreateNoticeStrip(
            text,
            "ColorBrushSemanticSuccessBackground",
            "#EAF7F4",
            "ColorBrushSemanticSuccessBorder",
            "#C8E6DF",
            "ColorBrushSemanticSuccessForeground",
            "#24534E",
            margin);
    }

    private DownloadInstallOptionViewModel CreateDownloadInstallOption(
        string title,
        string selection,
        Bitmap? icon,
        string detailText,
        string selectText,
        bool canSelect,
        ActionCommand selectCommand,
        bool canClear,
        ActionCommand clearCommand)
    {
        return new DownloadInstallOptionViewModel(
            title,
            selection,
            icon,
            detailText,
            selectText,
            canSelect,
            selectCommand,
            canClear,
            clearCommand);
    }

    private void SetDownloadCatalogIntro(string title, string body, IReadOnlyList<DownloadCatalogActionViewModel> actions)
    {
        DownloadCatalogIntroTitle = title;
        DownloadCatalogIntroBody = body;
        ReplaceItems(DownloadCatalogIntroActions, actions);
    }

    private DownloadCatalogSectionViewModel CreateDownloadCatalogSection(
        string title,
        IReadOnlyList<DownloadCatalogEntryViewModel> items,
        bool isCollapsible = false,
        bool isExpanded = true,
        Func<CancellationToken, Task<IReadOnlyList<DownloadCatalogEntryViewModel>>>? loadEntriesAsync = null,
        string loadingText = "fetch_versions")
    {
        return new DownloadCatalogSectionViewModel(title, items, isCollapsible, isExpanded, loadEntriesAsync, loadingText);
    }

    private DownloadCatalogSectionViewModel CreateDownloadCatalogSectionViewModel(
        LauncherFrontendSubpageKey route,
        FrontendDownloadCatalogSection section)
    {
        Func<CancellationToken, Task<IReadOnlyList<DownloadCatalogEntryViewModel>>>? loadEntriesAsync = null;
        if (!string.IsNullOrWhiteSpace(section.LazyLoadToken))
        {
            var lazyLoadToken = section.LazyLoadToken;
            loadEntriesAsync = async cancellationToken =>
            {
                var entries = await FrontendDownloadCompositionService.LoadCatalogSectionEntriesAsync(
                    _launcherActionService.RuntimePaths,
                    _instanceComposition,
                    route,
                    lazyLoadToken,
                    _i18n,
                    cancellationToken);
                return entries.Select(CreateDownloadCatalogEntryViewModel).ToArray();
            };
        }

        return CreateDownloadCatalogSection(
            LocalizeDownloadCatalogSectionTitle(route, section.Title),
            section.Entries.Select(CreateDownloadCatalogEntryViewModel).ToArray(),
            section.IsCollapsible,
            section.IsInitiallyExpanded,
            loadEntriesAsync,
            LocalizeDownloadCatalogLoadingText(route, section.LoadingText));
    }

    private DownloadCatalogEntryViewModel CreateDownloadCatalogEntry(string title, string info, string meta, string actionText)
    {
        return new DownloadCatalogEntryViewModel(
            title,
            info,
            meta,
            LocalizeDownloadCatalogActionText(actionText),
            new ActionCommand(() => AddActivity(
                T("download.catalog.activities.entry_action", ("entry_title", title)),
                BuildActivityDetail(meta, info))));
    }

    private DownloadCatalogEntryViewModel CreateDownloadCatalogEntryViewModel(FrontendDownloadCatalogEntry entry)
    {
        return new DownloadCatalogEntryViewModel(
            entry.Title,
            entry.Info,
            entry.Meta,
            LocalizeDownloadCatalogActionText(entry.ActionText),
            CreateDownloadCatalogCommand(entry));
    }

    private string LocalizeDownloadCatalogActionText(string actionText)
    {
        return actionText switch
        {
            "save_installer" => T("download.catalog.actions.save_installer"),
            "open_website" => T("common.actions.open_website"),
            "view_details" => T("resource_detail.actions.view_details"),
            _ => actionText
        };
    }

    private string LocalizeDownloadCatalogSectionTitle(LauncherFrontendSubpageKey route, string title)
    {
        if (string.IsNullOrWhiteSpace(title) || route != LauncherFrontendSubpageKey.DownloadClient)
        {
            return title switch
            {
                "latest_versions" => T("download.catalog.sections.latest_versions"),
                "remote_catalog" => T("download.catalog.sections.remote_catalog"),
                _ when title.StartsWith("version_list ", StringComparison.Ordinal) => title.Replace(
                    "version_list",
                    T("download.catalog.sections.version_list"),
                    StringComparison.Ordinal),
                _ => title
            };
        }

        var match = Regex.Match(title, @"^(?<group>[^()]+?)(?: \((?<count>\d+)\))?$");
        if (!match.Success)
        {
            return title;
        }

        var localizedGroup = match.Groups["group"].Value.Trim() switch
        {
            "latest_versions" => T("download.catalog.sections.latest_versions"),
            "remote_catalog" => T("download.catalog.sections.remote_catalog"),
            "version_list" => T("download.catalog.sections.version_list"),
            "latest" => T("download.install.catalog.groups.latest"),
            "release" => T("download.install.catalog.groups.release"),
            "preview" => T("download.install.catalog.groups.preview"),
            "legacy" => T("download.install.catalog.groups.legacy"),
            "april_fools" => T("download.install.catalog.groups.april_fools"),
            _ => title
        };

        return match.Groups["count"].Success
            ? $"{localizedGroup} ({match.Groups["count"].Value})"
            : localizedGroup;
    }

    private string LocalizeDownloadCatalogIntroTitle(LauncherFrontendSubpageKey route, string title)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadForge => T("download.catalog.intro.forge.title"),
            LauncherFrontendSubpageKey.DownloadNeoForge => T("download.catalog.intro.neoforge.title"),
            LauncherFrontendSubpageKey.DownloadFabric => T("download.catalog.intro.fabric.title"),
            LauncherFrontendSubpageKey.DownloadLegacyFabric => T("download.catalog.intro.legacy_fabric.title"),
            LauncherFrontendSubpageKey.DownloadQuilt => T("download.catalog.intro.quilt.title"),
            LauncherFrontendSubpageKey.DownloadOptiFine => T("download.catalog.intro.optifine.title"),
            LauncherFrontendSubpageKey.DownloadLiteLoader => T("download.catalog.intro.liteloader.title"),
            LauncherFrontendSubpageKey.DownloadLabyMod => T("download.catalog.intro.labymod.title"),
            LauncherFrontendSubpageKey.DownloadCleanroom => T("download.catalog.intro.cleanroom.title"),
            _ => title
        };
    }

    private string LocalizeDownloadCatalogIntroBody(LauncherFrontendSubpageKey route, string body, string? staleError)
    {
        var localizedBody = route switch
        {
            LauncherFrontendSubpageKey.DownloadForge => T("download.catalog.intro.forge.body"),
            LauncherFrontendSubpageKey.DownloadNeoForge => T("download.catalog.intro.neoforge.body"),
            LauncherFrontendSubpageKey.DownloadFabric => T("download.catalog.intro.fabric.body"),
            LauncherFrontendSubpageKey.DownloadLegacyFabric => T("download.catalog.intro.legacy_fabric.body"),
            LauncherFrontendSubpageKey.DownloadQuilt => T("download.catalog.intro.quilt.body"),
            LauncherFrontendSubpageKey.DownloadOptiFine => T("download.catalog.intro.optifine.body"),
            LauncherFrontendSubpageKey.DownloadLiteLoader => T("download.catalog.intro.liteloader.body"),
            LauncherFrontendSubpageKey.DownloadLabyMod => T("download.catalog.intro.labymod.body"),
            LauncherFrontendSubpageKey.DownloadCleanroom => T("download.catalog.intro.cleanroom.body"),
            _ => body
        };

        if (!string.IsNullOrWhiteSpace(staleError))
        {
            return T("download.catalog.intro.stale_cache", ("body", localizedBody), ("error", staleError));
        }

        return localizedBody;
    }

    private string LocalizeDownloadCatalogLoadingText(LauncherFrontendSubpageKey route, string loadingText)
    {
        if (string.IsNullOrWhiteSpace(loadingText))
        {
            return string.Empty;
        }

        if (string.Equals(loadingText, "fetch_list", StringComparison.Ordinal))
        {
            return T("download.catalog.loading.fetch_list", ("surface_name", ResolveDownloadCatalogRouteTitle(route)));
        }

        if (string.Equals(loadingText, "fetch_versions", StringComparison.Ordinal))
        {
            return T("download.catalog.loading.fetch_versions");
        }

        return loadingText;
    }

    private IReadOnlyList<DownloadCatalogSectionViewModel> BuildDownloadFavoriteSections()
    {
        return
        [
            CreateDownloadCatalogSection("Mod",
            [
                CreateDownloadCatalogEntry("Sodium", T("download.favorites.demo.entries.sodium.info"), BuildActivityDetail("Fabric", T("instance.overview.tags.favorited")), "view_details"),
                CreateDownloadCatalogEntry("Mod Menu", T("download.favorites.demo.entries.mod_menu.info"), BuildActivityDetail("Fabric", T("instance.overview.tags.favorited")), "view_details")
            ]),
            CreateDownloadCatalogSection(T("shell.navigation.subpages.download_pack.title"),
            [
                CreateDownloadCatalogEntry("All The Mods 9", T("download.favorites.demo.entries.atm9.info"), BuildActivityDetail(T("shell.navigation.subpages.download_pack.title"), T("instance.overview.tags.favorited")), "view_details")
            ]),
            CreateDownloadCatalogSection(T("shell.navigation.subpages.download_resource_pack.title"),
            [
                CreateDownloadCatalogEntry("Faithful 32x", T("download.favorites.demo.entries.faithful.info"), BuildActivityDetail(T("shell.navigation.subpages.download_resource_pack.title"), T("instance.overview.tags.favorited")), "view_details"),
                CreateDownloadCatalogEntry("Complementary Shaders", T("download.favorites.demo.entries.complementary.info"), BuildActivityDetail(T("shell.navigation.subpages.download_shader.title"), T("instance.overview.tags.favorited")), "view_details")
            ])
        ];
    }

    private ActionCommand CreateLinkCommand(string title, string url)
    {
        return CreateOpenTargetCommand(title, url, url);
    }

    private ActionCommand CreateIntentCommand(string title, string detail)
    {
        return new ActionCommand(() => AddActivity(title, detail));
    }

    private static string BuildActivityDetail(params string?[] parts)
    {
        return string.Join(
            " • ",
            parts
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));
    }

    private ActionCommand CreateDownloadCatalogCommand(FrontendDownloadCatalogEntry entry)
    {
        if (entry.ActionKind == FrontendDownloadCatalogEntryActionKind.DownloadFile
            && !string.IsNullOrWhiteSpace(entry.Target))
        {
            return new ActionCommand(() => _ = DownloadCatalogFileAsync(entry));
        }

        return string.IsNullOrWhiteSpace(entry.Target)
            ? CreateIntentCommand(
                T("download.catalog.activities.entry_action", ("entry_title", entry.Title)),
                BuildActivityDetail(entry.Info, entry.Meta))
            : CreateOpenTargetCommand(
                T("download.catalog.activities.open_entry", ("entry_title", entry.Title)),
                entry.Target,
                entry.Target);
    }

    private async Task DownloadCatalogFileAsync(FrontendDownloadCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Target))
        {
            AddFailureActivity(
                T("resource_detail.activities.download_failed", ("entry_title", entry.Title)),
                T("download.catalog.download.missing_url"));
            return;
        }

        var suggestedFileName = ResolveDownloadCatalogSuggestedFileName(entry);
        var extension = Path.GetExtension(suggestedFileName);
        var patterns = string.IsNullOrWhiteSpace(extension) ? Array.Empty<string>() : [$"*{extension}"];
        var typeName = T(
            "download.catalog.download.installer_type",
            ("surface_name", ResolveDownloadCatalogRouteTitle(_currentRoute.Subpage)));

        string? targetPath;
        try
        {
            targetPath = await _launcherActionService.PickSaveFileAsync(
                T("download.catalog.download.pick_save_path_title"),
                suggestedFileName,
                typeName,
                ResolveDownloadCatalogStartDirectory(),
                patterns);
        }
        catch (Exception ex)
        {
            AddFailureActivity(
                T("resource_detail.activities.pick_save_path_failed", ("entry_title", entry.Title)),
                ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            AddActivity(
                T("resource_detail.activities.download_canceled", ("entry_title", entry.Title)),
                T("download.catalog.download.no_save_path"));
            return;
        }

        TaskCenter.Register(new FrontendManagedFileDownloadTask(
            T("download.catalog.download.task_title", ("file_name", Path.GetFileNameWithoutExtension(targetPath))),
            entry.Target,
            targetPath,
            ResolveDownloadRequestTimeout(),
            _launcherActionService.GetDownloadTransferOptions(),
            onStarted: filePath => AvaloniaHintBus.Show(
                T("download.catalog.download.hints.started", ("file_name", Path.GetFileName(filePath))),
                AvaloniaHintTheme.Info),
            onCompleted: filePath => AvaloniaHintBus.Show(
                T("download.catalog.download.hints.completed", ("file_name", Path.GetFileName(filePath))),
                AvaloniaHintTheme.Success),
            onFailed: message => AvaloniaHintBus.Show(message, AvaloniaHintTheme.Error)));
        AddActivity(
            T("resource_detail.activities.download_started", ("entry_title", entry.Title)),
            targetPath);
    }

    private string ResolveDownloadCatalogSuggestedFileName(FrontendDownloadCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.SuggestedFileName))
        {
            return entry.SuggestedFileName!.Trim();
        }

        if (Uri.TryCreate(entry.Target, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return entry.Title.Trim() + ".jar";
    }

    private string? ResolveDownloadCatalogStartDirectory()
    {
        if (string.IsNullOrWhiteSpace(ToolDownloadFolder))
        {
            return null;
        }

        var directory = Path.GetFullPath(ToolDownloadFolder);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private string ResolveDownloadCatalogRouteTitle(LauncherFrontendSubpageKey route)
    {
        var title = LauncherLocalizationService.ResolveSubpageTitle(route, _i18n);
        return string.IsNullOrWhiteSpace(title)
            ? T("download.catalog.download.file_type_default")
            : title;
    }

    private ActionCommand CreateDownloadFavoriteCommand(FrontendDownloadCatalogEntry entry)
    {
        if (FrontendCommunityProjectService.TryParseCompDetailTarget(entry.Target, out var projectId))
        {
            return new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title));
        }

        return CreateOpenTargetCommand(
            T("download.catalog.activities.open_favorite_entry", ("entry_title", entry.Title)),
            entry.Target!,
            entry.Target!);
    }

    private ActionCommand CreateOpenTargetCommand(string title, string target, string detail)
    {
        return new ActionCommand(() =>
        {
            if (_launcherActionService.TryOpenExternalTarget(target, out var error))
            {
                AddActivity(title, detail);
            }
            else
            {
                AddFailureActivity(T("common.activities.failed", ("title", title)), error ?? detail);
            }
        });
    }

}
