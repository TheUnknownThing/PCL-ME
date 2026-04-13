using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Desktop.Controls;

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
        RefreshDownloadCatalogSurface();
        RefreshDownloadResourceSurface();
        RefreshDownloadFavoriteSurface();
        RefreshInstanceOverviewSurface();
        RefreshInstanceSetupSurface();
        RefreshInstanceExportSurface();
        RefreshInstanceInstallSurface();
        RefreshInstanceContentSurfaces();
        ReplaceItems(SurfaceFacts, pageContent.Facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
        ReplaceItems(SurfaceSections, pageContent.Sections.Select((section, index) => CreateSurfaceSection(section, index)));
        RaiseCollectionStateProperties();

        SelectPromptLane(_selectedPromptLane, updateActivity: false);
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

        _routeHistory.Add(_currentRoute);
        _currentRoute = route;
        if (route.Page == LauncherFrontendPageKey.Setup)
        {
            ReloadSetupComposition();
        }
        else if (route.Page == LauncherFrontendPageKey.InstanceSetup)
        {
            ReloadInstanceComposition();
        }

        RefreshShell(activityMessage);
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
            var previousRoute = _routeHistory[^1];
            _routeHistory.RemoveAt(_routeHistory.Count - 1);
            _currentRoute = previousRoute;
            if (_currentRoute.Page == LauncherFrontendPageKey.Setup)
            {
                ReloadSetupComposition();
            }
            else if (_currentRoute.Page == LauncherFrontendPageKey.InstanceSetup)
            {
                ReloadInstanceComposition();
            }

            RefreshShell("Returned to the previous shell route.");
            if (_currentRoute.Page == LauncherFrontendPageKey.Setup && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUpdate)
            {
                _ = CheckForLauncherUpdatesAsync(forceRefresh: false);
            }
            return;
        }

        if (_currentNavigation.BackTarget?.Route is { } backRoute)
        {
            _currentRoute = backRoute;
            if (_currentRoute.Page == LauncherFrontendPageKey.Setup)
            {
                ReloadSetupComposition();
            }
            else if (_currentRoute.Page == LauncherFrontendPageKey.InstanceSetup)
            {
                ReloadInstanceComposition();
            }

            RefreshShell($"Followed shell back target to {backRoute.Page}.");
            if (_currentRoute.Page == LauncherFrontendPageKey.Setup && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUpdate)
            {
                _ = CheckForLauncherUpdatesAsync(forceRefresh: false);
            }
        }
    }

    private void RaiseShellStateProperties()
    {
        RaisePropertyChanged(nameof(IsLaunchRoute));
        RaisePropertyChanged(nameof(IsStandardShellRoute));
        RaisePropertyChanged(nameof(IsSetupLaunchSurface));
        RaisePropertyChanged(nameof(IsSetupAboutSurface));
        RaisePropertyChanged(nameof(IsSetupFeedbackSurface));
        RaisePropertyChanged(nameof(IsSetupLogSurface));
        RaisePropertyChanged(nameof(IsSetupUpdateSurface));
        RaisePropertyChanged(nameof(IsSetupGameLinkSurface));
        RaisePropertyChanged(nameof(IsSetupGameManageSurface));
        RaisePropertyChanged(nameof(IsSetupLauncherMiscSurface));
        RaisePropertyChanged(nameof(IsSetupJavaSurface));
        RaisePropertyChanged(nameof(IsSetupUiSurface));
        RaisePropertyChanged(nameof(IsDownloadInstallSurface));
        RaisePropertyChanged(nameof(IsDownloadCatalogSurface));
        RaisePropertyChanged(nameof(IsDownloadResourceSurface));
        RaisePropertyChanged(nameof(IsDownloadFavoritesSurface));
        RaisePropertyChanged(nameof(IsToolsGameLinkSurface));
        RaisePropertyChanged(nameof(IsToolsHelpSurface));
        RaisePropertyChanged(nameof(IsToolsTestSurface));
        RaisePropertyChanged(nameof(IsInstanceOverviewSurface));
        RaisePropertyChanged(nameof(IsInstanceSetupSurface));
        RaisePropertyChanged(nameof(IsInstanceExportSurface));
        RaisePropertyChanged(nameof(IsInstanceInstallSurface));
        RaisePropertyChanged(nameof(IsInstanceWorldSurface));
        RaisePropertyChanged(nameof(IsInstanceScreenshotSurface));
        RaisePropertyChanged(nameof(IsInstanceServerSurface));
        RaisePropertyChanged(nameof(IsInstanceResourceSurface));
        RaisePropertyChanged(nameof(IsGenericShellSurface));
        RaisePropertyChanged(nameof(ShowTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowInnerNavigation));
        RaisePropertyChanged(nameof(TitleBarLabel));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
        RaiseUpdateSurfaceProperties();
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
        RaisePropertyChanged(nameof(HasAboutProjectEntries));
        RaisePropertyChanged(nameof(HasAboutAcknowledgementEntries));
        RaisePropertyChanged(nameof(HasFeedbackSections));
        RaisePropertyChanged(nameof(HasDownloadResourceEntries));
        RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
        RaisePropertyChanged(nameof(HasDownloadFavoriteSections));
        RaisePropertyChanged(nameof(HasNoDownloadFavoriteSections));
        RaisePropertyChanged(nameof(HasHelpTopicGroups));
        RaisePropertyChanged(nameof(HasNoHelpTopicGroups));
        RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
        RaisePropertyChanged(nameof(HasInstanceOverviewInfoEntries));
        RaisePropertyChanged(nameof(HasInstanceExportOptionGroups));
        RaisePropertyChanged(nameof(HasInstanceWorldEntries));
        RaisePropertyChanged(nameof(HasNoInstanceWorldEntries));
        RaisePropertyChanged(nameof(HasInstanceScreenshotEntries));
        RaisePropertyChanged(nameof(HasNoInstanceScreenshotEntries));
        RaisePropertyChanged(nameof(HasInstanceServerEntries));
        RaisePropertyChanged(nameof(HasNoInstanceServerEntries));
        RaisePropertyChanged(nameof(HasInstanceResourceEntries));
        RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
        RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
        RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
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
