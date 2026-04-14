using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void InitializeAboutEntries()
    {
        ReplaceItems(AboutProjectEntries,
        [
            new AboutEntryViewModel(
                "龙腾猫跃",
                "Plain Craft Launcher 的原作者！",
                LoadLauncherBitmap("Images", "Heads", "LTCat.jpg"),
                "赞助原作者",
                CreateLinkCommand("赞助原作者", "https://ifdian.net/a/LTCat")),
            new AboutEntryViewModel(
                "PCL Community",
                "PCL-ME 当前延续自社区维护分支。",
                LoadLauncherBitmap("Images", "Heads", "PCL-Community.png"),
                "GitHub 主页",
                CreateLinkCommand("打开 PCL Community GitHub", "https://github.com/PCL-Community")),
            new AboutEntryViewModel(
                "PCL-ME",
                _setupComposition.About.LauncherVersionSummary,
                LoadLauncherBitmap("Images", "Heads", "Logo-CE.png"),
                "查看源代码",
                CreateLinkCommand("查看仓库源代码", "https://github.com/TheUnknownThing/PCL-ME"))
        ]);

        ReplaceItems(AboutAcknowledgementEntries,
        [
            new AboutEntryViewModel("bangbang93", "提供 BMCLAPI 镜像源和 Forge 安装工具。", LoadLauncherBitmap("Images", "Heads", "bangbang93.png"), "赞助镜像源", CreateLinkCommand("赞助 BMCLAPI 镜像源", "https://afdian.com/a/bangbang93")),
            new AboutEntryViewModel("MC 百科", "提供了 Mod 名称的中文翻译和更多相关信息！", LoadLauncherBitmap("Images", "Heads", "wiki.png"), "打开百科", CreateLinkCommand("打开 MC 百科", "https://www.mcmod.cn")),
            new AboutEntryViewModel("Pysio @ Akaere Network", "提供了 PCL-CE 的相关云服务", LoadLauncherBitmap("Images", "Heads", "Pysio.jpg"), "转到博客", CreateLinkCommand("打开 Pysio 博客", "https://www.pysio.online")),
            new AboutEntryViewModel("云默安 @ 至远光辉", "提供了 PCL-CE 的相关云服务", LoadLauncherBitmap("Images", "Heads", "Yunmoan.jpg"), "打开网站", CreateLinkCommand("打开至远光辉", "https://www.zyghit.cn")),
            new AboutEntryViewModel("z0z0r4", "提供了 MCIM 中国 Mod 下载镜像源和帮助库图床！", LoadLauncherBitmap("Images", "Heads", "z0z0r4.png"), null, null),
            new AboutEntryViewModel("00ll00", "提供了 Java Launch Wrapper 和一些重要服务支持！", LoadLauncherBitmap("Images", "Heads", "00ll00.png"), null, null),
            new AboutEntryViewModel("Patrick", "设计并制作了 PCL 图标，让龙猫从做图标的水深火热中得到了解脱……", LoadLauncherBitmap("Images", "Heads", "Patrick.png"), null, null),
            new AboutEntryViewModel("Hao_Tian", "在 PCL 内测中找出了一大堆没人想得到的诡异 Bug，有非同寻常的 Bug 体质", LoadLauncherBitmap("Images", "Heads", "Hao_Tian.jpg"), null, null),
            new AboutEntryViewModel("Minecraft 中文论坛", "虽然已经关站了，但感谢此前提供了 MCBBS 镜像源……", LoadLauncherBitmap("Images", "Heads", "MCBBS.png"), null, null),
            new AboutEntryViewModel("PCL 内群的各位", "感谢内群的沙雕网友们这么久以来对龙猫和 PCL 的支持与鼓励！", LoadLauncherBitmap("Images", "Heads", "PCL2.png"), null, null)
        ]);
    }

    private void InitializeLogEntries()
    {
        ReplaceItems(LogEntries,
            _setupComposition.Log.Entries.Select(entry =>
                CreateSimpleEntry(
                    entry.Title,
                    entry.Summary,
                    CreateOpenTargetCommand($"打开日志: {entry.Title}", entry.Path, entry.Summary))));
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
        _optimizeMemoryBeforeLaunch = _setupComposition.Launch.OptimizeMemoryBeforeLaunch;
        _isLaunchAdvancedOptionsExpanded = false;
        _selectedLaunchRendererIndex = _setupComposition.Launch.RendererIndex;
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
            _downloadInstallName = "新的安装方案";
        }

        RefreshDownloadInstallSurfaceState();
    }

    private void RefreshDownloadInstallSurface()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadInstall))
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
        DownloadCatalogLoadingText = FrontendDownloadRemoteCatalogService.GetLoadingText(_currentRoute.Subpage);
        ReplaceItems(DownloadCatalogIntroActions, []);
        ReplaceItems(DownloadCatalogSections, []);
        SetDownloadCatalogLoading(false);

        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadCatalog))
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
                _shellActionService.RuntimePaths,
                _instanceComposition,
                route,
                cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested
                    || refreshVersion != _downloadCatalogRefreshVersion
                    || _currentRoute.Subpage != route
                    || !IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadCatalog))
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

                DownloadCatalogIntroTitle = "远程目录加载失败";
                DownloadCatalogIntroBody = ex.Message;
                DownloadCatalogLoadingText = "请稍后重试。";
                ReplaceItems(DownloadCatalogIntroActions, []);
                ReplaceItems(
                    DownloadCatalogSections,
                    [
                        CreateDownloadCatalogSection(
                            "远程目录",
                            [
                                new DownloadCatalogEntryViewModel(
                                    "暂无可显示数据",
                                    "当前无法读取远程目录，请稍后重试。",
                                    string.Empty,
                                    "查看详情",
                                    CreateIntentCommand("下载页加载失败", ex.Message))
                            ])
                    ]);
                SetDownloadCatalogLoading(false);
            });
        }
    }

    private void ApplyDownloadCatalogState(FrontendDownloadCatalogState state)
    {
        DownloadCatalogLoadingText = state.LoadingText;
        SetDownloadCatalogIntro(
            state.IntroTitle,
            state.IntroBody,
            state.Actions.Select(action =>
                new DownloadCatalogActionViewModel(
                    action.Text,
                    action.IsHighlight ? PclButtonColorState.Highlight : PclButtonColorState.Normal,
                    string.IsNullOrWhiteSpace(action.Target)
                        ? CreateIntentCommand(action.Text, state.IntroTitle)
                        : CreateOpenTargetCommand(action.Text, action.Target, action.Target))).ToArray());
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

    private void RefreshDownloadFavoriteSurface()
    {
        ReplaceItems(DownloadFavoriteSections, []);

        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadFavorites))
        {
            return;
        }

        EnsureDownloadCompositionRemoteStateLoaded();
        ShowDownloadFavoriteWarning = _downloadComposition.Favorites.ShowWarning;
        DownloadFavoriteWarningText = _downloadComposition.Favorites.WarningText;
        var selectedTarget = GetSelectedDownloadFavoriteTargetState();
        SyncDownloadFavoriteSelectionTarget(selectedTarget.Id);
        var visibleEntries = selectedTarget.Sections
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
            ? CreateIntentCommand($"收藏夹条目: {entry.Title}", entry.Info)
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
            actionToolTip: "查看详情",
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
        _notifyReleaseUpdates = _setupComposition.GameManage.NotifyReleaseUpdates;
        _notifySnapshotUpdates = _setupComposition.GameManage.NotifySnapshotUpdates;
        _autoSwitchGameLanguageToChinese = _setupComposition.GameManage.AutoSwitchGameLanguageToChinese;
        _detectClipboardResourceLinks = _setupComposition.GameManage.DetectClipboardResourceLinks;
    }

    private void InitializeLauncherMiscSurface()
    {
        _selectedSystemActivityIndex = _setupComposition.LauncherMisc.SystemActivityIndex;
        _animationFpsLimit = _setupComposition.LauncherMisc.AnimationFpsLimit;
        _maxRealTimeLogValue = _setupComposition.LauncherMisc.MaxRealTimeLogValue;
        _disableHardwareAcceleration = _setupComposition.LauncherMisc.DisableHardwareAcceleration;
        _enableDoH = _setupComposition.LauncherMisc.EnableDoH;
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
    }

    private void InitializeUiSurface()
    {
        _selectedDarkModeIndex = _setupComposition.Ui.DarkModeIndex;
        _selectedLightColorIndex = FrontendAppearanceService.NormalizeThemeColorIndex(_setupComposition.Ui.LightColorIndex, ThemeColorOptions.Count);
        _selectedDarkColorIndex = FrontendAppearanceService.NormalizeThemeColorIndex(_setupComposition.Ui.DarkColorIndex, ThemeColorOptions.Count);
        CustomLightThemeColorHex = _setupComposition.Ui.LightCustomColorHex;
        CustomDarkThemeColorHex = _setupComposition.Ui.DarkCustomColorHex;
        SeedCustomThemeColorIfNeeded(isDarkPalette: false, _selectedLightColorIndex, 0);
        SeedCustomThemeColorIfNeeded(isDarkPalette: true, _selectedDarkColorIndex, 0);
        _launcherOpacity = _setupComposition.Ui.LauncherOpacity;
        _showLauncherLogo = _setupComposition.Ui.ShowLauncherLogo;
        _lockWindowSize = _setupComposition.Ui.LockWindowSize;
        _showLaunchingHint = _setupComposition.Ui.ShowLaunchingHint;
        _selectedGlobalFontIndex = _setupComposition.Ui.GlobalFontIndex;
        _selectedMotdFontIndex = _setupComposition.Ui.MotdFontIndex;
        _backgroundColorful = _setupComposition.Ui.BackgroundColorful;
        _backgroundOpacity = _setupComposition.Ui.BackgroundOpacity;
        _backgroundBlur = _setupComposition.Ui.BackgroundBlur;
        _selectedBackgroundSuitIndex = Math.Clamp(_setupComposition.Ui.BackgroundSuitIndex, 0, BackgroundSuitOptions.Count - 1);
        _musicVolume = _setupComposition.Ui.MusicVolume;
        _musicRandomPlay = _setupComposition.Ui.MusicRandomPlay;
        _musicAutoStart = _setupComposition.Ui.MusicAutoStart;
        _musicStartOnGameLaunch = _setupComposition.Ui.MusicStartOnGameLaunch;
        _musicStopOnGameLaunch = _setupComposition.Ui.MusicStopOnGameLaunch;
        _musicEnableSmtc = _setupComposition.Ui.MusicEnableSmtc;
        _selectedLogoTypeIndex = _setupComposition.Ui.LogoTypeIndex;
        _logoAlignLeft = _setupComposition.Ui.LogoAlignLeft;
        _logoText = _setupComposition.Ui.LogoText;
        _selectedHomepageTypeIndex = _setupComposition.Ui.HomepageTypeIndex;
        _homepageUrl = _setupComposition.Ui.HomepageUrl;
        _selectedHomepagePresetIndex = Math.Clamp(_setupComposition.Ui.HomepagePresetIndex, 0, HomepagePresetOptions.Count - 1);
        RefreshTitleBarLogoImage();
        RefreshLaunchHomepage(forceRefresh: false);

        ReplaceItems(UiFeatureToggleGroups,
            _setupComposition.Ui.ToggleGroups.Select(group =>
                new UiFeatureToggleGroupViewModel(
                    group.Title,
                    group.Items.Select(item =>
                        new UiFeatureToggleItemViewModel(
                            item.Title,
                            item.IsChecked,
                            isChecked => PersistUiToggle(item.ConfigKey, isChecked))).ToArray())));
        RefreshBackgroundContentState(selectNewAsset: _currentBackgroundAssetPath is null, addActivity: false);
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
                .Select(entry => CreateHelpTopic(entry, entry.GroupTitles.FirstOrDefault() ?? "帮助"))
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
                .OrderBy(title => string.Equals(title.GroupTitle, "指南", StringComparison.Ordinal) ? 0 : 1)
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
                        groupTitle,
                        items,
                        isExpanded: string.Equals(groupTitle, "指南", StringComparison.Ordinal));
                })
                .Where(group => group.Items.Count > 0)
                .ToArray();

            ReplaceItems(HelpTopicGroups, groups);
        }

        RaisePropertyChanged(nameof(IsHelpSearchActive));
        RaisePropertyChanged(nameof(ShowHelpTopicLibrary));
        RaisePropertyChanged(nameof(HelpSearchResultsHeader));
        RaiseCollectionStateProperties();
    }

    private HelpTopicViewModel CreateHelpTopic(FrontendToolsHelpEntry entry, string groupTitle)
    {
        return new HelpTopicViewModel(
            groupTitle,
            entry.Title,
            entry.Summary,
            entry.Keywords,
            ResolveHelpTopicIcon(entry),
            new ActionCommand(() => OpenHelpTopic(entry)));
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
            return string.Equals(entry.EventType, "弹出窗口", StringComparison.OrdinalIgnoreCase)
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

    private static bool HelpEntryMatchesQuery(FrontendToolsHelpEntry entry, string query)
    {
        return entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.GroupTitles.Any(groupTitle => groupTitle.Contains(query, StringComparison.OrdinalIgnoreCase));
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
        return new SimpleListEntryViewModel(title, info, new ActionCommand(() => AddActivity($"查看条目: {title}", info)));
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
        string loadingText = "正在获取版本列表")
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
                    _shellActionService.RuntimePaths,
                    _instanceComposition,
                    route,
                    lazyLoadToken,
                    cancellationToken);
                return entries.Select(CreateDownloadCatalogEntryViewModel).ToArray();
            };
        }

        return CreateDownloadCatalogSection(
            section.Title,
            section.Entries.Select(CreateDownloadCatalogEntryViewModel).ToArray(),
            section.IsCollapsible,
            section.IsInitiallyExpanded,
            loadEntriesAsync,
            section.LoadingText);
    }

    private DownloadCatalogEntryViewModel CreateDownloadCatalogEntry(string title, string info, string meta, string actionText)
    {
        return new DownloadCatalogEntryViewModel(
            title,
            info,
            meta,
            actionText,
            new ActionCommand(() => AddActivity($"下载页操作: {title}", $"{meta} • {info}")));
    }

    private DownloadCatalogEntryViewModel CreateDownloadCatalogEntryViewModel(FrontendDownloadCatalogEntry entry)
    {
        return new DownloadCatalogEntryViewModel(
            entry.Title,
            entry.Info,
            entry.Meta,
            entry.ActionText,
            CreateDownloadCatalogCommand(entry));
    }

    private IReadOnlyList<DownloadCatalogSectionViewModel> BuildDownloadFavoriteSections()
    {
        return
        [
            CreateDownloadCatalogSection("Mod",
            [
                CreateDownloadCatalogEntry("Sodium", "现代性能优化模组。", "Fabric • 已收藏", "查看详情"),
                CreateDownloadCatalogEntry("Mod Menu", "管理 Fabric 模组菜单。", "Fabric • 已收藏", "查看详情")
            ]),
            CreateDownloadCatalogSection("整合包",
            [
                CreateDownloadCatalogEntry("All The Mods 9", "大型整合包演示条目。", "整合包 • 已收藏", "查看详情")
            ]),
            CreateDownloadCatalogSection("资源包",
            [
                CreateDownloadCatalogEntry("Faithful 32x", "经典风格资源包。", "资源包 • 已收藏", "查看详情"),
                CreateDownloadCatalogEntry("Complementary Shaders", "常用光影收藏条目。", "光影包 • 已收藏", "查看详情")
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

    private ActionCommand CreateDownloadCatalogCommand(FrontendDownloadCatalogEntry entry)
    {
        if (entry.ActionKind == FrontendDownloadCatalogEntryActionKind.DownloadFile
            && !string.IsNullOrWhiteSpace(entry.Target))
        {
            return new ActionCommand(() => _ = DownloadCatalogFileAsync(entry));
        }

        return string.IsNullOrWhiteSpace(entry.Target)
            ? CreateIntentCommand($"下载页操作: {entry.Title}", $"{entry.Info} • {entry.Meta}".Trim(' ', '•'))
            : CreateOpenTargetCommand($"打开条目: {entry.Title}", entry.Target, entry.Target);
    }

    private async Task DownloadCatalogFileAsync(FrontendDownloadCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Target))
        {
            AddFailureActivity($"下载条目失败: {entry.Title}", "当前没有可用的下载地址。");
            return;
        }

        var suggestedFileName = ResolveDownloadCatalogSuggestedFileName(entry);
        var extension = Path.GetExtension(suggestedFileName);
        var patterns = string.IsNullOrWhiteSpace(extension) ? Array.Empty<string>() : [$"*{extension}"];
        var typeName = $"{GetDownloadCatalogRouteTitle(_currentRoute.Subpage)} 安装器";

        string? targetPath;
        try
        {
            targetPath = await _shellActionService.PickSaveFileAsync(
                "选择保存位置",
                suggestedFileName,
                typeName,
                ResolveDownloadCatalogStartDirectory(),
                patterns);
        }
        catch (Exception ex)
        {
            AddFailureActivity($"选择保存位置失败: {entry.Title}", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            AddActivity($"已取消下载: {entry.Title}", "没有选择保存位置。");
            return;
        }

        TaskCenter.Register(new FrontendManagedFileDownloadTask(
            $"下载 {Path.GetFileNameWithoutExtension(targetPath)}",
            entry.Target,
            targetPath,
            ResolveDownloadRequestTimeout(),
            onStarted: filePath => AvaloniaHintBus.Show($"开始下载 {Path.GetFileName(filePath)}", AvaloniaHintTheme.Info),
            onCompleted: filePath => AvaloniaHintBus.Show($"{Path.GetFileName(filePath)} 下载完成", AvaloniaHintTheme.Success),
            onFailed: message => AvaloniaHintBus.Show(message, AvaloniaHintTheme.Error)));
        AddActivity($"开始下载: {entry.Title}", $"{entry.Target} -> {targetPath}");
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

    private static string GetDownloadCatalogRouteTitle(LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadOptiFine => "OptiFine",
            LauncherFrontendSubpageKey.DownloadForge => "Forge",
            LauncherFrontendSubpageKey.DownloadNeoForge => "NeoForge",
            LauncherFrontendSubpageKey.DownloadCleanroom => "Cleanroom",
            LauncherFrontendSubpageKey.DownloadFabric => "Fabric",
            LauncherFrontendSubpageKey.DownloadQuilt => "Quilt",
            LauncherFrontendSubpageKey.DownloadLiteLoader => "LiteLoader",
            LauncherFrontendSubpageKey.DownloadLabyMod => "LabyMod",
            LauncherFrontendSubpageKey.DownloadLegacyFabric => "Legacy Fabric",
            _ => "文件"
        };
    }

    private ActionCommand CreateDownloadFavoriteCommand(FrontendDownloadCatalogEntry entry)
    {
        if (FrontendCommunityProjectService.TryParseCompDetailTarget(entry.Target, out var projectId))
        {
            return new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title));
        }

        return CreateOpenTargetCommand($"打开收藏夹条目: {entry.Title}", entry.Target!, entry.Target!);
    }

    private ActionCommand CreateOpenTargetCommand(string title, string target, string detail)
    {
        return new ActionCommand(() =>
        {
            if (_shellActionService.TryOpenExternalTarget(target, out var error))
            {
                AddActivity(title, detail);
            }
            else
            {
                AddFailureActivity($"{title} 失败", error ?? detail);
            }
        });
    }

}
