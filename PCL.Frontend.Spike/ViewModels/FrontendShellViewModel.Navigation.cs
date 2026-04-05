using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.ViewModels.ShellPanes;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void RefreshShell(string activityMessage)
    {
        var shellPlan = BuildShellPlan();
        var pageContent = BuildPageContent(shellPlan);
        _currentNavigation = shellPlan.Navigation;

        Eyebrow = pageContent.Eyebrow;
        Title = shellPlan.Navigation.CurrentPage.Title;
        Description = pageContent.Summary;
        Status = $"Immediate command: {shellPlan.StartupPlan.ImmediateCommand.Kind} | Splash: {(shellPlan.StartupPlan.Visual.ShouldShowSplashScreen ? "on" : "off")} | Backstack depth: {_routeHistory.Count}";
        BreadcrumbTrail = string.Join(" / ", shellPlan.Navigation.Breadcrumbs.Select(crumb => crumb.Title));
        SurfaceMeta = $"{shellPlan.Navigation.CurrentPage.Kind} surface • {(shellPlan.Navigation.CurrentPage.SidebarGroupTitle ?? "No sidebar group")} • {(shellPlan.Navigation.ShowsBackButton ? shellPlan.Navigation.BackTarget?.Label ?? "Back available" : "Top-level route")}";
        CanGoBack = shellPlan.Navigation.ShowsBackButton;

        ReplaceItems(TopLevelEntries, shellPlan.Navigation.TopLevelEntries.Select(entry => CreateNavigationEntry(entry, NavigationVisualStyle.TopLevel)));
        ReplaceItems(SidebarEntries, shellPlan.Navigation.SidebarEntries.Select(entry => CreateNavigationEntry(entry, NavigationVisualStyle.Sidebar)));
        ReplaceItems(SidebarSections, BuildSidebarSections(shellPlan.Navigation));
        ReplaceItems(UtilityEntries, shellPlan.Navigation.UtilityEntries.Where(entry => entry.IsVisible).Select(CreateUtilityEntry));
        ReplaceItems(SurfaceFacts, pageContent.Facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
        ReplaceItems(SurfaceSections, pageContent.Sections.Select((section, index) => CreateSurfaceSection(section, index)));

        SelectPromptLane(_selectedPromptLane, updateActivity: false, raiseCollectionState: false);
        RefreshStandardShellPanes();
        RefreshActiveRightPaneSurface();
        RaiseCollectionStateProperties();
        AddActivity(activityMessage, $"{shellPlan.Navigation.CurrentPage.Title} • {shellPlan.Navigation.CurrentPage.Route.Page}/{shellPlan.Navigation.CurrentPage.Route.Subpage}");
        RaiseShellStateProperties();
    }

    private NavigationEntryViewModel CreateNavigationEntry(LauncherFrontendNavigationEntry entry, NavigationVisualStyle style)
    {
        var (iconPath, iconScale) = GetNavigationIcon(entry.Title);
        return new NavigationEntryViewModel(
            entry.Title,
            entry.Summary,
            style == NavigationVisualStyle.Sidebar ? entry.Route.Subpage.ToString() : entry.Route.Page.ToString(),
            entry.IsSelected,
            iconPath,
            iconScale,
            GetNavigationPalette(entry.IsSelected, style),
            new ActionCommand(() => NavigateTo(entry.Route, $"Navigated to {entry.Title} from the {(style == NavigationVisualStyle.Sidebar ? "sidebar" : "top bar")}."))
        );
    }

    private NavigationEntryViewModel CreateUtilityEntry(LauncherFrontendUtilityEntry entry)
    {
        var meta = entry.Id switch
        {
            "back" => "返",
            "task-manager" => "任",
            "game-log" => "志",
            _ => entry.Route.Page.ToString()
        };

        return new NavigationEntryViewModel(
            entry.Title,
            entry.IsSelected ? "Utility surface is active in the shell." : "Pinned shell utility surface.",
            meta,
            entry.IsSelected,
            GetUtilityIcon(entry.Id),
            1.0,
            GetNavigationPalette(entry.IsSelected, NavigationVisualStyle.Utility),
            new ActionCommand(() => NavigateTo(entry.Route, $"Opened utility surface {entry.Title}.")));
    }

    private IEnumerable<SidebarSectionViewModel> BuildSidebarSections(LauncherFrontendNavigationView navigation)
    {
        if (navigation.SidebarEntries.Count == 0)
        {
            return [];
        }

        return navigation.SidebarEntries
            .GroupBy(entry => GetSidebarSectionTitle(navigation.CurrentRoute.Page, entry.Route.Subpage))
            .Select(group => new SidebarSectionViewModel(
                group.Key,
                !string.IsNullOrWhiteSpace(group.Key),
                group.Select(entry =>
                {
                    var (iconPath, iconScale) = GetSidebarIcon(entry.Route.Page, entry.Route.Subpage, entry.Title);
                    var accessory = GetSidebarAccessory(entry.Route.Page, entry.Route.Subpage, entry.Title);
                    return new SidebarListItemViewModel(
                        entry.Title,
                        entry.Summary,
                        entry.IsSelected,
                        iconPath,
                        iconScale,
                        new ActionCommand(() => NavigateTo(entry.Route, $"Navigated to {entry.Title} from the launcher-style left pane.")),
                        accessory.ToolTip,
                        accessory.IconPath,
                        accessory.Command is null
                            ? null
                            : new ActionCommand(() => ApplySidebarAccessory(entry.Title, accessory.ActionLabel, accessory.Command)));
                }).ToArray()))
            .ToArray();
    }

    private LauncherFrontendShellPlan BuildShellPlan()
    {
        var request = _shellComposition.NavigationRequest with
        {
            CurrentRoute = _currentRoute,
            BackstackDepth = _routeHistory.Count
        };
        return LauncherFrontendShellService.BuildPlan(new LauncherFrontendShellRequest(
            _shellComposition.StartupWorkflowRequest,
            _shellComposition.StartupConsentRequest,
            request));
    }

    private void NavigateTo(LauncherFrontendRoute route, string activityMessage)
    {
        if (route == _currentRoute)
        {
            AddActivity("Stayed on the current route.", $"{route.Page}/{route.Subpage}");
            return;
        }

        var previousIsLaunchRoute = IsLaunchRoute;
        var previousLeftPaneKey = CurrentStandardLeftPaneDescriptor?.Key;
        var previousRightPaneKey = CurrentStandardRightPaneDescriptor?.Key;

        _routeHistory.Add(_currentRoute);
        _currentRoute = route;
        if (route.Page == LauncherFrontendPageKey.Setup)
        {
            ReloadSetupComposition(initializeAllSurfaces: false);
        }
        else if (route.Page == LauncherFrontendPageKey.Tools)
        {
            ReloadToolsComposition();
        }
        else if (route.Page == LauncherFrontendPageKey.InstanceSetup)
        {
            ReloadInstanceComposition(reloadDependentCompositions: false, initializeAllSurfaces: false);
        }
        else if (route.Page == LauncherFrontendPageKey.VersionSaves)
        {
            ReloadVersionSavesComposition();
            ReloadDownloadComposition();
        }

        RefreshShell(activityMessage);
        RequestNavigationTransition(
            ShellNavigationTransitionDirection.Forward,
            previousIsLaunchRoute,
            previousLeftPaneKey,
            previousRightPaneKey);
        if (route.Page == LauncherFrontendPageKey.Setup && route.Subpage == LauncherFrontendSubpageKey.SetupUpdate)
        {
            _ = CheckForLauncherUpdatesAsync(forceRefresh: false);
        }
    }

    private void NavigateBack()
    {
        if (_currentNavigation is null)
        {
            return;
        }

        if (_routeHistory.Count > 0)
        {
            var previousIsLaunchRoute = IsLaunchRoute;
            var previousLeftPaneKey = CurrentStandardLeftPaneDescriptor?.Key;
            var previousRightPaneKey = CurrentStandardRightPaneDescriptor?.Key;

            var previousRoute = _routeHistory[^1];
            _routeHistory.RemoveAt(_routeHistory.Count - 1);
            _currentRoute = previousRoute;
            if (_currentRoute.Page == LauncherFrontendPageKey.Setup)
            {
                ReloadSetupComposition(initializeAllSurfaces: false);
            }
            else if (_currentRoute.Page == LauncherFrontendPageKey.Tools)
            {
                ReloadToolsComposition();
            }
            else if (_currentRoute.Page == LauncherFrontendPageKey.InstanceSetup)
            {
                ReloadInstanceComposition(reloadDependentCompositions: false, initializeAllSurfaces: false);
            }
            else if (_currentRoute.Page == LauncherFrontendPageKey.VersionSaves)
            {
                ReloadVersionSavesComposition();
                ReloadDownloadComposition();
            }

            RefreshShell("Returned to the previous shell route.");
            RequestNavigationTransition(
                ShellNavigationTransitionDirection.Backward,
                previousIsLaunchRoute,
                previousLeftPaneKey,
                previousRightPaneKey);
            if (_currentRoute.Page == LauncherFrontendPageKey.Setup && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUpdate)
            {
                _ = CheckForLauncherUpdatesAsync(forceRefresh: false);
            }
            return;
        }

        if (_currentNavigation.BackTarget?.Route is { } backRoute)
        {
            var previousIsLaunchRoute = IsLaunchRoute;
            var previousLeftPaneKey = CurrentStandardLeftPaneDescriptor?.Key;
            var previousRightPaneKey = CurrentStandardRightPaneDescriptor?.Key;

            _currentRoute = backRoute;
            if (_currentRoute.Page == LauncherFrontendPageKey.Setup)
            {
                ReloadSetupComposition(initializeAllSurfaces: false);
            }
            else if (_currentRoute.Page == LauncherFrontendPageKey.Tools)
            {
                ReloadToolsComposition();
            }
            else if (_currentRoute.Page == LauncherFrontendPageKey.InstanceSetup)
            {
                ReloadInstanceComposition(reloadDependentCompositions: false, initializeAllSurfaces: false);
            }
            else if (_currentRoute.Page == LauncherFrontendPageKey.VersionSaves)
            {
                ReloadVersionSavesComposition();
                ReloadDownloadComposition();
            }

            RefreshShell($"Followed shell back target to {backRoute.Page}.");
            RequestNavigationTransition(
                ShellNavigationTransitionDirection.Backward,
                previousIsLaunchRoute,
                previousLeftPaneKey,
                previousRightPaneKey);
            if (_currentRoute.Page == LauncherFrontendPageKey.Setup && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUpdate)
            {
                _ = CheckForLauncherUpdatesAsync(forceRefresh: false);
            }
        }
    }

    private void RequestNavigationTransition(
        ShellNavigationTransitionDirection direction,
        bool previousIsLaunchRoute,
        string? previousLeftPaneKey,
        string? previousRightPaneKey)
    {
        var animateLeftPane = previousIsLaunchRoute != IsLaunchRoute
            || !string.Equals(previousLeftPaneKey, CurrentStandardLeftPaneDescriptor?.Key, StringComparison.Ordinal);
        var animateRightPane = previousIsLaunchRoute != IsLaunchRoute
            || !string.Equals(previousRightPaneKey, CurrentStandardRightPaneDescriptor?.Key, StringComparison.Ordinal);

        NavigationTransitionRequested?.Invoke(
            this,
            new ShellNavigationTransitionEventArgs(direction, IsLaunchRoute, animateLeftPane, animateRightPane));
    }

    private void RefreshActiveRightPaneSurface()
    {
        switch (CurrentStandardRightPaneDescriptor?.Kind)
        {
            case StandardShellRightPaneKind.DownloadCatalog:
                RefreshDownloadCatalogSurface();
                break;
            case StandardShellRightPaneKind.DownloadResource:
                RefreshDownloadResourceSurface();
                break;
            case StandardShellRightPaneKind.DownloadFavorites:
                RefreshDownloadFavoriteSurface();
                break;
            case StandardShellRightPaneKind.VersionSaveInfo:
            case StandardShellRightPaneKind.VersionSaveBackup:
            case StandardShellRightPaneKind.VersionSaveDatapack:
                RefreshVersionSaveSurfaces();
                break;
            case StandardShellRightPaneKind.InstanceOverview:
                RefreshInstanceOverviewSurface();
                break;
            case StandardShellRightPaneKind.InstanceSetup:
                RefreshInstanceSetupSurface();
                break;
            case StandardShellRightPaneKind.InstanceExport:
                RefreshInstanceExportSurface();
                break;
            case StandardShellRightPaneKind.InstanceInstall:
                RefreshInstanceInstallSurface();
                break;
            case StandardShellRightPaneKind.InstanceWorld:
            case StandardShellRightPaneKind.InstanceScreenshot:
            case StandardShellRightPaneKind.InstanceServer:
            case StandardShellRightPaneKind.InstanceResource:
                RefreshInstanceContentSurfaces();
                break;
        }
    }

    private void RaiseShellStateProperties()
    {
        RaisePropertyChanged(nameof(IsLaunchRoute));
        RaisePropertyChanged(nameof(IsStandardShellRoute));
        RaisePropertyChanged(nameof(ShowTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowInnerNavigation));
        RaisePropertyChanged(nameof(TitleBarLabel));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupUpdate))
        {
            RaiseUpdateSurfaceProperties();
        }
    }

    private void RaiseCollectionStateProperties()
    {
        RaisePropertyChanged(nameof(HasSidebarEntries));
        RaisePropertyChanged(nameof(HasSidebarSections));
        RaisePropertyChanged(nameof(HasNoSidebarSections));
        RaisePropertyChanged(nameof(HasSurfaceFacts));
        RaisePropertyChanged(nameof(HasSurfaceSections));
        RaisePropertyChanged(nameof(HasUtilityEntries));
        RaisePropertyChanged(nameof(HasActivityEntries));

        switch (CurrentStandardRightPaneDescriptor?.Kind)
        {
            case StandardShellRightPaneKind.SetupAbout:
                RaisePropertyChanged(nameof(HasAboutProjectEntries));
                RaisePropertyChanged(nameof(HasAboutAcknowledgementEntries));
                break;
            case StandardShellRightPaneKind.SetupFeedback:
                RaisePropertyChanged(nameof(HasFeedbackSections));
                break;
            case StandardShellRightPaneKind.SetupJava:
                RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
                break;
            case StandardShellRightPaneKind.DownloadResource:
                RaisePropertyChanged(nameof(HasDownloadResourceEntries));
                RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
                break;
            case StandardShellRightPaneKind.DownloadFavorites:
                RaisePropertyChanged(nameof(HasDownloadFavoriteSections));
                RaisePropertyChanged(nameof(HasNoDownloadFavoriteSections));
                break;
            case StandardShellRightPaneKind.VersionSaveInfo:
                RaisePropertyChanged(nameof(HasVersionSaveInfoEntries));
                RaisePropertyChanged(nameof(HasVersionSaveSettingEntries));
                break;
            case StandardShellRightPaneKind.VersionSaveBackup:
                RaisePropertyChanged(nameof(HasVersionSaveBackupEntries));
                RaisePropertyChanged(nameof(HasNoVersionSaveBackupEntries));
                break;
            case StandardShellRightPaneKind.VersionSaveDatapack:
                RaisePropertyChanged(nameof(HasVersionSaveDatapackEntries));
                RaisePropertyChanged(nameof(HasNoVersionSaveDatapackEntries));
                break;
            case StandardShellRightPaneKind.ToolsHelp:
                RaisePropertyChanged(nameof(HasHelpTopicGroups));
                RaisePropertyChanged(nameof(HasNoHelpTopicGroups));
                break;
            case StandardShellRightPaneKind.InstanceOverview:
                RaisePropertyChanged(nameof(HasInstanceOverviewInfoEntries));
                break;
            case StandardShellRightPaneKind.InstanceExport:
                RaisePropertyChanged(nameof(HasInstanceExportOptionGroups));
                break;
            case StandardShellRightPaneKind.InstanceWorld:
                RaisePropertyChanged(nameof(HasInstanceWorldEntries));
                RaisePropertyChanged(nameof(HasNoInstanceWorldEntries));
                break;
            case StandardShellRightPaneKind.InstanceScreenshot:
                RaisePropertyChanged(nameof(HasInstanceScreenshotEntries));
                RaisePropertyChanged(nameof(HasNoInstanceScreenshotEntries));
                break;
            case StandardShellRightPaneKind.InstanceServer:
                RaisePropertyChanged(nameof(HasInstanceServerEntries));
                RaisePropertyChanged(nameof(HasNoInstanceServerEntries));
                break;
            case StandardShellRightPaneKind.InstanceResource:
                RaisePropertyChanged(nameof(HasInstanceResourceEntries));
                RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
                RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
                RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
                RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
                break;
        }
    }

    private void RaiseUpdateSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowAvailableUpdateCard));
        RaisePropertyChanged(nameof(ShowCurrentVersionCard));
        RaisePropertyChanged(nameof(ShowOptionalUpdateCard));
        RaisePropertyChanged(nameof(AvailableUpdateName));
        RaisePropertyChanged(nameof(AvailableUpdatePublisher));
        RaisePropertyChanged(nameof(AvailableUpdateSummary));
        RaisePropertyChanged(nameof(CurrentVersionName));
        RaisePropertyChanged(nameof(CurrentVersionDescription));
    }
}
