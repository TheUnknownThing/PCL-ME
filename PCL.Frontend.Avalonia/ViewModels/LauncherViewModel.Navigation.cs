using System.IO;
using System.Collections.ObjectModel;
using System.Threading;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private void RefreshLauncherState(string activityMessage)
    {
        RefreshLauncherStateCore(activityMessage, addActivity: true);
    }

    private void RefreshLauncherStateCore(string? activityMessage, bool addActivity)
    {
        var launcherPlan = BuildLauncherPlan();
        var localizedNavigation = LauncherLocalizationService.LocalizeNavigationView(launcherPlan.Navigation, _i18n);
        var sidebarNavigation = ResolveSidebarNavigation(localizedNavigation);
        var pageContent = BuildPageContent(launcherPlan);
        RefreshCurrentDedicatedGenericRouteSurface();
        var surfaceFacts = pageContent.Facts;
        var surfaceSections = pageContent.Sections;
        var eyebrow = pageContent.Eyebrow;
        var description = pageContent.Summary;
        _currentNavigation = localizedNavigation;

        if (TryBuildDedicatedGenericRouteMetadata(out var dedicatedMetadata))
        {
            eyebrow = dedicatedMetadata.Eyebrow;
            description = dedicatedMetadata.Description;
            surfaceFacts = dedicatedMetadata.Facts;
            surfaceSections = [];
        }

        Eyebrow = eyebrow;
        Title = LauncherLocalizationService.ResolvePageTitle(localizedNavigation.CurrentRoute.Page, _i18n);
        Description = description;
        Status = LauncherLocalizationService.DescribeLauncherStatus(launcherPlan, _routeAncestors.Count, _i18n);
        BreadcrumbTrail = string.Join(" / ", localizedNavigation.Breadcrumbs.Select(crumb => crumb.Title));
        SurfaceMeta = LauncherLocalizationService.DescribeSurfaceMeta(localizedNavigation, _i18n);
        CanGoBack = localizedNavigation.ShowsBackButton && localizedNavigation.BackTarget is not null;
        CanGoHome = ResolveHomeRoute() is not null;

        ReplaceNavigationEntriesIfChanged(TopLevelEntries, localizedNavigation.TopLevelEntries, NavigationVisualStyle.TopLevel);
        ReplaceNavigationEntriesIfChanged(SidebarEntries, sidebarNavigation.SidebarEntries, NavigationVisualStyle.Sidebar);
        ReplaceSidebarSectionsIfChanged(sidebarNavigation);
        ReplaceUtilityEntriesIfChanged(localizedNavigation.UtilityEntries.Where(entry => entry.IsVisible).ToArray());
        ReplaceSurfaceFactsIfChanged(surfaceFacts);
        ReplaceSurfaceSectionsIfChanged(surfaceSections);

        SelectPromptLane(_selectedPromptLane, updateActivity: false, raiseCollectionState: false);
        RefreshStandardPanes();
        RefreshActiveRightPaneSurface();
        RaiseCollectionStateProperties();
        if (addActivity && !string.IsNullOrWhiteSpace(activityMessage))
        {
            AddActivity(
                activityMessage,
                $"{LauncherLocalizationService.ResolvePageTitle(localizedNavigation.CurrentPage.Route.Page, _i18n)} • {localizedNavigation.CurrentPage.Route.Page}/{localizedNavigation.CurrentPage.Route.Subpage}");
        }

        RaiseLauncherStateProperties();
    }

    private string ResolveNavigationEntryTitle(LauncherFrontendNavigationEntry entry)
    {
        return LauncherLocalizationService.ResolveRouteLabel(entry.Route, _i18n);
    }

    private string ResolveNavigationEntrySummary(LauncherFrontendNavigationEntry entry)
    {
        return LauncherLocalizationService.ResolveRouteSummary(entry.Route, _i18n);
    }

    private string ResolveUtilityEntryTitle(LauncherFrontendUtilityEntry entry)
    {
        return LauncherLocalizationService.ResolveUtilityTitle(entry.Id, _i18n);
    }

    private LauncherFrontendNavigationView ResolveSidebarNavigation(LauncherFrontendNavigationView navigation)
    {
        var route = ResolveSidebarAnchorRoute(_currentRoute);
        if (route == _currentRoute)
        {
            return navigation;
        }

        var request = BuildCurrentNavigationRequest() with
        {
            CurrentRoute = route,
            BackstackDepth = 0,
            ParentRoute = ResolveDefaultParentRoute(route)
        };
        return LauncherLocalizationService.LocalizeNavigationView(
            FrontendUiVisibilityService.FilterNavigationView(
                LauncherFrontendNavigationService.BuildView(request),
                GetUiVisibilityPreferences()),
            _i18n);
    }

    private static LauncherFrontendRoute ResolveSidebarAnchorRoute(LauncherFrontendRoute route)
    {
        return route.Page switch
        {
            LauncherFrontendPageKey.HelpDetail => new LauncherFrontendRoute(
                LauncherFrontendPageKey.Tools,
                LauncherFrontendSubpageKey.ToolsLauncherHelp),
            _ => route
        };
    }

    private NavigationEntryViewModel CreateNavigationEntry(LauncherFrontendNavigationEntry entry, NavigationVisualStyle style)
    {
        var title = ResolveNavigationEntryTitle(entry);
        var summary = ResolveNavigationEntrySummary(entry);
        var (iconPath, iconScale) = GetNavigationIcon(entry.Route, title);
        return new NavigationEntryViewModel(
            title,
            summary,
            style == NavigationVisualStyle.Sidebar ? entry.Route.Subpage.ToString() : entry.Route.Page.ToString(),
            entry.IsSelected,
            iconPath,
            iconScale,
            GetNavigationPalette(entry.IsSelected, style),
            new ActionCommand(() => NavigateTo(
                entry.Route,
                LauncherLocalizationService.DescribeNavigationActivity(
                    title,
                    style == NavigationVisualStyle.Sidebar ? "sidebar" : "top_bar",
                    _i18n),
                style == NavigationVisualStyle.Sidebar
                    ? RouteNavigationBehavior.Lateral
                    : RouteNavigationBehavior.Reset),
                () => style != NavigationVisualStyle.TopLevel || IsTopLevelNavigationInteractive)
        );
    }

    private NavigationEntryViewModel CreateUtilityEntry(LauncherFrontendUtilityEntry entry)
    {
        var title = ResolveUtilityEntryTitle(entry);
        var meta = entry.Id switch
        {
            "back" => "B",
            "task-manager" => "T",
            "game-log" => "L",
            _ => entry.Route.Page.ToString()
        };

        return new NavigationEntryViewModel(
            title,
            LauncherLocalizationService.DescribeUtilitySummary(entry.IsSelected, _i18n),
            meta,
            entry.IsSelected,
            GetUtilityIcon(entry.Id),
            1.0,
            GetNavigationPalette(entry.IsSelected, NavigationVisualStyle.Utility),
            entry.Id == "back"
                ? new ActionCommand(NavigateBack, () => CanGoBack)
                : new ActionCommand(() => NavigateTo(
                    entry.Route,
                    LauncherLocalizationService.DescribeOpenedUtilitySurface(title, _i18n),
                    RouteNavigationBehavior.Child)));
    }

    private IEnumerable<SidebarSectionViewModel> BuildSidebarSections(LauncherFrontendNavigationView navigation)
    {
        if (navigation.SidebarEntries.Count == 0)
        {
            return [];
        }

        var itemIndex = 0;
        return navigation.SidebarEntries
            .GroupBy(entry => GetSidebarSectionTitle(navigation.CurrentRoute.Page, entry.Route.Subpage, _i18n))
            .Select(group =>
            {
                var hasTitle = !string.IsNullOrWhiteSpace(group.Key);
                var enterDelay = hasTitle ? itemIndex++ * 28 : itemIndex * 28;
                return new SidebarSectionViewModel(
                    group.Key,
                    hasTitle,
                    enterDelay,
                    group.Select(entry =>
                    {
                        var title = ResolveNavigationEntryTitle(entry);
                        var summary = ResolveNavigationEntrySummary(entry);
                        var (iconPath, iconScale) = GetSidebarIcon(entry.Route.Page, entry.Route.Subpage, title);
                        var accessory = GetSidebarAccessory(entry.Route.Page, entry.Route.Subpage, title, _i18n);
                        return new SidebarListItemViewModel(
                            title,
                            summary,
                            entry.IsSelected,
                            iconPath,
                            iconScale,
                            itemIndex++ * 28,
                            new ActionCommand(() => NavigateTo(
                                entry.Route,
                                LauncherLocalizationService.DescribeNavigationActivity(title, "left_pane", _i18n),
                                RouteNavigationBehavior.Lateral)),
                            accessory.ToolTip,
                            accessory.IconPath,
                            accessory.Command is null
                                ? null
                                : new ActionCommand(() => ApplySidebarAccessory(title, accessory.ActionLabel, accessory.Command)));
                    }).ToArray());
            })
            .ToArray();
    }

    private void ReplaceNavigationEntriesIfChanged(
        ObservableCollection<NavigationEntryViewModel> collection,
        IReadOnlyList<LauncherFrontendNavigationEntry> entries,
        NavigationVisualStyle style)
    {
        if (collection.Count == entries.Count)
        {
            var matches = true;
            for (var index = 0; index < entries.Count; index++)
            {
                if (!MatchesNavigationEntry(collection[index], entries[index], style))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return;
            }
        }

        ReplaceItems(collection, entries.Select(entry => CreateNavigationEntry(entry, style)));
        if (style == NavigationVisualStyle.TopLevel)
        {
            NotifyTopLevelNavigationCanExecuteChanged();
        }
    }

    private void ReplaceUtilityEntriesIfChanged(IReadOnlyList<LauncherFrontendUtilityEntry> entries)
    {
        if (UtilityEntries.Count == entries.Count)
        {
            var matches = true;
            for (var index = 0; index < entries.Count; index++)
            {
                if (!MatchesUtilityEntry(UtilityEntries[index], entries[index]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return;
            }
        }

        ReplaceItems(UtilityEntries, entries.Select(CreateUtilityEntry));
    }

    private void ReplaceSidebarSectionsIfChanged(LauncherFrontendNavigationView navigation)
    {
        var snapshots = BuildSidebarSectionSnapshots(navigation);
        if (SidebarSections.Count == snapshots.Length)
        {
            if (CanUpdateSidebarSectionsSelectionInPlace(SidebarSections, snapshots))
            {
                UpdateSidebarSectionSelection(SidebarSections, snapshots);
                return;
            }

            var matches = true;
            for (var index = 0; index < snapshots.Length; index++)
            {
                if (!MatchesSidebarSection(SidebarSections[index], snapshots[index]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return;
            }
        }

        ReplaceItems(SidebarSections, BuildSidebarSections(navigation));
    }

    private static bool CanUpdateSidebarSectionsSelectionInPlace(
        ObservableCollection<SidebarSectionViewModel> currentSections,
        SidebarSectionSnapshot[] snapshots)
    {
        if (currentSections.Count != snapshots.Length)
        {
            return false;
        }

        for (var index = 0; index < snapshots.Length; index++)
        {
            var currentSection = currentSections[index];
            var snapshot = snapshots[index];
            if (currentSection.Title != snapshot.Title
                || currentSection.HasTitle != snapshot.HasTitle
                || currentSection.Items.Count != snapshot.Items.Length)
            {
                return false;
            }

            for (var itemIndex = 0; itemIndex < snapshot.Items.Length; itemIndex++)
            {
                var currentItem = currentSection.Items[itemIndex];
                var snapshotItem = snapshot.Items[itemIndex];
                if (currentItem.Title != snapshotItem.Title
                    || currentItem.Summary != snapshotItem.Summary
                    || currentItem.IconPath != snapshotItem.IconPath
                    || !currentItem.IconScale.Equals(snapshotItem.IconScale)
                    || currentItem.AccessoryToolTip != snapshotItem.AccessoryToolTip
                    || currentItem.AccessoryIconPath != snapshotItem.AccessoryIconPath
                    || (currentItem.AccessoryCommand is not null) != snapshotItem.HasAccessoryCommand)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void UpdateSidebarSectionSelection(
        ObservableCollection<SidebarSectionViewModel> currentSections,
        SidebarSectionSnapshot[] snapshots)
    {
        for (var index = 0; index < snapshots.Length; index++)
        {
            var currentSection = currentSections[index];
            var snapshot = snapshots[index];
            for (var itemIndex = 0; itemIndex < snapshot.Items.Length; itemIndex++)
            {
                currentSection.Items[itemIndex].IsSelected = snapshot.Items[itemIndex].IsSelected;
            }
        }
    }

    private void ReplaceSurfaceFactsIfChanged(IReadOnlyList<LauncherFrontendPageFact> facts)
    {
        if (SurfaceFacts.Count == facts.Count)
        {
            var matches = true;
            for (var index = 0; index < facts.Count; index++)
            {
                if (SurfaceFacts[index].Label != facts[index].Label
                    || SurfaceFacts[index].Value != facts[index].Value)
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return;
            }
        }

        ReplaceItems(SurfaceFacts, facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
    }

    private void ReplaceSurfaceSectionsIfChanged(IReadOnlyList<LauncherFrontendPageSection> sections)
    {
        if (SurfaceSections.Count == sections.Count)
        {
            var matches = true;
            for (var index = 0; index < sections.Count; index++)
            {
                if (!MatchesSurfaceSection(SurfaceSections[index], sections[index]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return;
            }
        }

        ReplaceItems(SurfaceSections, sections.Select((section, index) => CreateSurfaceSection(section, index)));
    }

    private SidebarSectionSnapshot[] BuildSidebarSectionSnapshots(LauncherFrontendNavigationView navigation)
    {
        if (navigation.SidebarEntries.Count == 0)
        {
            return [];
        }

        return navigation.SidebarEntries
            .GroupBy(entry => GetSidebarSectionTitle(navigation.CurrentRoute.Page, entry.Route.Subpage, _i18n))
            .Select(group => new SidebarSectionSnapshot(
                group.Key,
                !string.IsNullOrWhiteSpace(group.Key),
                group.Select(entry =>
                {
                    var title = ResolveNavigationEntryTitle(entry);
                    var summary = ResolveNavigationEntrySummary(entry);
                    var (iconPath, iconScale) = GetSidebarIcon(entry.Route.Page, entry.Route.Subpage, title);
                    var accessory = GetSidebarAccessory(entry.Route.Page, entry.Route.Subpage, title, _i18n);
                    return new SidebarItemSnapshot(
                        title,
                        summary,
                        entry.IsSelected,
                        iconPath,
                        iconScale,
                        accessory.ToolTip,
                        accessory.IconPath,
                        accessory.Command is not null);
                }).ToArray()))
            .ToArray();
    }

    private bool MatchesNavigationEntry(
        NavigationEntryViewModel current,
        LauncherFrontendNavigationEntry entry,
        NavigationVisualStyle style)
    {
        var title = ResolveNavigationEntryTitle(entry);
        var summary = ResolveNavigationEntrySummary(entry);
        var (iconPath, iconScale) = GetNavigationIcon(entry.Route, title);
        var meta = style == NavigationVisualStyle.Sidebar
            ? entry.Route.Subpage.ToString()
            : entry.Route.Page.ToString();

        return current.Title == title
            && current.Summary == summary
            && current.Meta == meta
            && current.IsSelected == entry.IsSelected
            && current.IconPath == iconPath
            && current.IconScale.Equals(iconScale);
    }

    private bool MatchesUtilityEntry(NavigationEntryViewModel current, LauncherFrontendUtilityEntry entry)
    {
        var title = ResolveUtilityEntryTitle(entry);
        var meta = entry.Id switch
        {
            "back" => "B",
            "task-manager" => "T",
            "game-log" => "L",
            _ => entry.Route.Page.ToString()
        };

        return current.Title == title
            && current.Meta == meta
            && current.IsSelected == entry.IsSelected
            && current.IconPath == GetUtilityIcon(entry.Id);
    }

    private static bool MatchesSidebarSection(SidebarSectionViewModel current, SidebarSectionSnapshot snapshot)
    {
        if (current.Title != snapshot.Title
            || current.HasTitle != snapshot.HasTitle
            || current.Items.Count != snapshot.Items.Length)
        {
            return false;
        }

        for (var index = 0; index < snapshot.Items.Length; index++)
        {
            var currentItem = current.Items[index];
            var snapshotItem = snapshot.Items[index];
            if (currentItem.Title != snapshotItem.Title
                || currentItem.Summary != snapshotItem.Summary
                || currentItem.IsSelected != snapshotItem.IsSelected
                || currentItem.IconPath != snapshotItem.IconPath
                || !currentItem.IconScale.Equals(snapshotItem.IconScale)
                || currentItem.AccessoryToolTip != snapshotItem.AccessoryToolTip
                || currentItem.AccessoryIconPath != snapshotItem.AccessoryIconPath
                || (currentItem.AccessoryCommand is not null) != snapshotItem.HasAccessoryCommand)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesSurfaceSection(SurfaceSectionViewModel current, LauncherFrontendPageSection section)
    {
        if (current.Eyebrow != section.Eyebrow
            || current.Title != section.Title
            || current.Lines.Count != section.Lines.Count)
        {
            return false;
        }

        for (var index = 0; index < section.Lines.Count; index++)
        {
            if (current.Lines[index].Text != section.Lines[index])
            {
                return false;
            }
        }

        return true;
    }

    private sealed record SidebarSectionSnapshot(
        string Title,
        bool HasTitle,
        SidebarItemSnapshot[] Items);

    private sealed record SidebarItemSnapshot(
        string Title,
        string Summary,
        bool IsSelected,
        string IconPath,
        double IconScale,
        string AccessoryToolTip,
        string AccessoryIconPath,
        bool HasAccessoryCommand);

    private LauncherFrontendPlan BuildLauncherPlan()
    {
        var normalizedCurrentRoute = NormalizeRoute(_currentRoute);
        if (normalizedCurrentRoute != _currentRoute)
        {
            _currentRoute = normalizedCurrentRoute;
        }

        var plan = LauncherFrontendPlanService.BuildPlan(new LauncherFrontendPlanRequest(
            _launcherComposition.StartupWorkflowRequest,
            _launcherComposition.StartupConsentRequest,
            BuildCurrentNavigationRequest()));

        return plan with
        {
            Navigation = FrontendUiVisibilityService.FilterNavigationView(
                plan.Navigation,
                GetUiVisibilityPreferences())
        };
    }

    private LauncherFrontendNavigationViewRequest BuildCurrentNavigationRequest()
    {
        return _launcherComposition.NavigationRequest with
        {
            CurrentRoute = _currentRoute,
            BackstackDepth = _routeAncestors.Count,
            ParentRoute = ResolveParentRoute(),
            HasRunningTasks = LauncherFrontendRuntimeStateService.HasRunningTasks(),
            HasGameLogs = HasNavigationGameLogs()
        };
    }

    private bool HasNavigationGameLogs()
    {
        var runtimePaths = _launcherActionService.RuntimePaths;
        var platformAdapter = _launcherActionService.PlatformAdapter;

        return HasLaunchLogLines
            || FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                runtimePaths.LauncherAppDataDirectory,
                platformAdapter).Any(File.Exists)
            || FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                runtimePaths.DataDirectory,
                platformAdapter).Any(File.Exists)
            || Directory.Exists(Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log"))
            || Directory.Exists(Path.Combine(runtimePaths.DataDirectory, "Log"));
    }

    private void RefreshDynamicUtilityEntries()
    {
        var navigation = LauncherLocalizationService.LocalizeNavigationView(
            LauncherFrontendNavigationService.BuildView(BuildCurrentNavigationRequest()),
            _i18n);
        ReplaceUtilityEntriesIfChanged(navigation.UtilityEntries.Where(entry => entry.IsVisible).ToArray());
        RaisePropertyChanged(nameof(HasUtilityEntries));
        RaisePropertyChanged(nameof(ShowBottomRightUtilityEntryButtons));
        RaisePropertyChanged(nameof(ShowBottomRightExtraButtons));
    }

    private void NavigateTo(
        LauncherFrontendRoute route,
        string activityMessage,
        RouteNavigationBehavior behavior = RouteNavigationBehavior.Automatic)
    {
        route = NormalizeRoute(route);
        if (route == _currentRoute)
        {
            AddActivity(LauncherLocalizationService.DescribeStayedOnCurrentRoute(_i18n), $"{route.Page}/{route.Subpage}");
            return;
        }

        ApplyRouteNavigation(route, behavior);
        ChangeRoute(route, activityMessage, NavigationTransitionDirection.Forward);
    }

    private LauncherFrontendRoute NormalizeRoute(LauncherFrontendRoute route)
    {
        var normalized = route switch
        {
            { Page: LauncherFrontendPageKey.Download, Subpage: LauncherFrontendSubpageKey.Default } =>
                new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadInstall),
            { Page: LauncherFrontendPageKey.Setup, Subpage: LauncherFrontendSubpageKey.SetupLink } =>
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
            { Page: LauncherFrontendPageKey.Tools, Subpage: LauncherFrontendSubpageKey.Default } =>
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsTest),
            { Page: LauncherFrontendPageKey.InstanceSetup, Subpage: LauncherFrontendSubpageKey.VersionModDisabled } =>
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod),
            _ => route
        };

        return FrontendUiVisibilityService.NormalizeRoute(normalized, GetUiVisibilityPreferences());
    }

    private void ApplyRouteNavigation(LauncherFrontendRoute route, RouteNavigationBehavior behavior)
    {
        switch (ResolveRouteNavigationBehavior(route, behavior))
        {
            case RouteNavigationBehavior.Child:
                _routeAncestors.Add(_currentRoute);
                break;
            case RouteNavigationBehavior.Lateral:
                break;
            case RouteNavigationBehavior.Reset:
                _routeAncestors.Clear();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unknown route navigation behavior.");
        }
    }

    private RouteNavigationBehavior ResolveRouteNavigationBehavior(
        LauncherFrontendRoute route,
        RouteNavigationBehavior behavior)
    {
        if (behavior != RouteNavigationBehavior.Automatic)
        {
            return behavior;
        }

        if (route.Page == _currentRoute.Page)
        {
            return RouteNavigationBehavior.Lateral;
        }

        return IsTopLevelRoute(route)
            ? RouteNavigationBehavior.Reset
            : RouteNavigationBehavior.Child;
    }

    private void NavigateBack()
    {
        if (TryNavigateBackWithinCommunityProjectDetail())
        {
            return;
        }

        if (_currentNavigation is null)
        {
            return;
        }

        if (_currentNavigation.BackTarget?.Route is { } backRoute)
        {
            if (_routeAncestors.Count > 0)
            {
                _routeAncestors.RemoveAt(_routeAncestors.Count - 1);
            }

            ChangeRoute(
                backRoute,
                LauncherLocalizationService.DescribeFollowedBackTarget(backRoute, _i18n),
                NavigationTransitionDirection.Backward);
        }
    }

    private void NavigateHome()
    {
        var resolvedHomeRoute = ResolveHomeRoute();
        if (resolvedHomeRoute is null)
        {
            return;
        }

        var homeRoute = NormalizeRoute(resolvedHomeRoute);
        if (homeRoute == _currentRoute)
        {
            return;
        }

        _routeAncestors.Clear();
        ResetCommunityProjectNavigationStack();
        ChangeRoute(
            homeRoute,
            LauncherLocalizationService.DescribeReturnedHome(homeRoute, _i18n),
            NavigationTransitionDirection.Backward);
    }

    private void ChangeRoute(
        LauncherFrontendRoute route,
        string activityMessage,
        NavigationTransitionDirection direction)
    {
        route = NormalizeRoute(route);
        var previousIsLaunchRoute = IsLaunchRoute;
        var previousLeftPaneKey = CurrentStandardLeftPaneDescriptor?.Key;
        var previousRightPaneKey = CurrentStandardRightPaneDescriptor?.Key;

        if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail
            && route.Page != LauncherFrontendPageKey.CompDetail)
        {
            ResetCommunityProjectNavigationStack();
        }

        var previousRoute = _currentRoute;
        _currentRoute = route;
        ReloadRouteCompositions(previousRoute, route);
        RefreshLauncherState(activityMessage);
        RequestNavigationTransition(
            direction,
            previousIsLaunchRoute,
            previousLeftPaneKey,
            previousRightPaneKey);
        if (route.Page == LauncherFrontendPageKey.Setup && route.Subpage == LauncherFrontendSubpageKey.SetupUpdate)
        {
            _ = CheckForLauncherUpdatesAsync(forceRefresh: false);
        }

        QueueClipboardCommunityLinkProbe(route);
    }

    private void ReloadRouteCompositions(LauncherFrontendRoute previousRoute, LauncherFrontendRoute route)
    {
        if (route.Page != LauncherFrontendPageKey.InstanceSetup || !IsInstanceResourceSubpage(route.Subpage))
        {
            SetInstanceResourceLoading(false);
        }

        if (route.Page == LauncherFrontendPageKey.Setup)
        {
            ReloadActiveSetupSurface(applyAppearance: false);
        }
        else if (route.Page == LauncherFrontendPageKey.Tools)
        {
            ReloadActiveToolsSurface();
        }
        else if (route.Page == LauncherFrontendPageKey.InstanceSetup)
        {
            var requiredLoadMode = ResolveInstanceCompositionLoadMode(route);
            if (previousRoute.Page != LauncherFrontendPageKey.InstanceSetup
                || !HasSufficientInstanceCompositionLoadMode(requiredLoadMode))
            {
                if (ShouldShowInstanceResourceLoadingForRoute(route))
                {
                    QueueSelectedInstanceStateRefresh(Interlocked.Increment(ref _instanceSelectionRefreshVersion));
                }
                else
                {
                    ReloadInstanceComposition(
                        requiredLoadMode,
                        reloadDependentCompositions: false,
                        initializeAllSurfaces: false);
                }
            }
        }
        else if (route.Page == LauncherFrontendPageKey.VersionSaves)
        {
            ReloadVersionSavesComposition();
            ReloadDownloadComposition();
        }
    }

    private LauncherFrontendRoute? ResolveParentRoute()
    {
        if (_routeAncestors.Count > 0)
        {
            return _routeAncestors[^1];
        }

        return ResolveDefaultParentRoute(_currentRoute);
    }

    private LauncherFrontendRoute? ResolveHomeRoute()
    {
        if (HasCommunityProjectNavigationHistory && _currentRoute.Page == LauncherFrontendPageKey.CompDetail)
        {
            return ResolveDefaultParentRoute(_currentRoute);
        }

        if (_routeAncestors.Count > 0)
        {
            return _routeAncestors[0];
        }

        return ResolveDefaultParentRoute(_currentRoute);
    }

    private LauncherFrontendRoute? ResolveDefaultParentRoute(LauncherFrontendRoute route)
    {
        return route.Page switch
        {
            LauncherFrontendPageKey.Launch => null,
            LauncherFrontendPageKey.Download => null,
            LauncherFrontendPageKey.Setup => null,
            LauncherFrontendPageKey.Tools => null,
            LauncherFrontendPageKey.InstanceSelect => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LauncherFrontendPageKey.TaskManager => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LauncherFrontendPageKey.InstanceSetup => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LauncherFrontendPageKey.CompDetail => new LauncherFrontendRoute(
                LauncherFrontendPageKey.Download,
                _selectedCommunityProjectOriginSubpage ?? LauncherFrontendSubpageKey.DownloadInstall),
            LauncherFrontendPageKey.HelpDetail => new LauncherFrontendRoute(
                LauncherFrontendPageKey.Tools,
                LauncherFrontendSubpageKey.ToolsLauncherHelp),
            LauncherFrontendPageKey.GameLog => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LauncherFrontendPageKey.VersionSaves => new LauncherFrontendRoute(
                LauncherFrontendPageKey.InstanceSetup,
                LauncherFrontendSubpageKey.VersionWorld),
            _ => null
        };
    }

    private static bool IsTopLevelRoute(LauncherFrontendRoute route)
    {
        return route.Page is LauncherFrontendPageKey.Launch
            or LauncherFrontendPageKey.Download
            or LauncherFrontendPageKey.Setup
            or LauncherFrontendPageKey.Tools;
    }

    private enum RouteNavigationBehavior
    {
        Automatic = 0,
        Child = 1,
        Lateral = 2,
        Reset = 3
    }

    private void RequestNavigationTransition(
        NavigationTransitionDirection direction,
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
            new NavigationTransitionEventArgs(direction, IsLaunchRoute, animateLeftPane, animateRightPane));
    }

    private void RefreshActiveRightPaneSurface()
    {
        switch (CurrentStandardRightPaneDescriptor?.Kind)
        {
            case StandardRightPaneKind.Generic:
                RefreshCurrentDedicatedGenericRouteSurface();
                break;
            case StandardRightPaneKind.TaskManager:
                RefreshTaskManagerSurface();
                break;
            case StandardRightPaneKind.DownloadInstall:
                RefreshDownloadInstallSurface();
                break;
            case StandardRightPaneKind.DownloadCatalog:
                RefreshDownloadCatalogSurface();
                break;
            case StandardRightPaneKind.DownloadResource:
                RefreshDownloadResourceSurface();
                break;
            case StandardRightPaneKind.DownloadFavorites:
                RefreshDownloadFavoriteSurface();
                break;
            case StandardRightPaneKind.InstanceSelection:
                RefreshInstanceSelectionSurface();
                break;
            case StandardRightPaneKind.VersionSaveInfo:
            case StandardRightPaneKind.VersionSaveBackup:
            case StandardRightPaneKind.VersionSaveDatapack:
                RefreshVersionSaveSurfaces();
                break;
            case StandardRightPaneKind.InstanceOverview:
                RefreshInstanceOverviewSurface();
                break;
            case StandardRightPaneKind.InstanceSetup:
                RefreshInstanceSetupSurface();
                break;
            case StandardRightPaneKind.InstanceExport:
                RefreshInstanceExportSurface();
                break;
            case StandardRightPaneKind.InstanceInstall:
                RefreshInstanceInstallSurface();
                break;
            case StandardRightPaneKind.InstanceWorld:
            case StandardRightPaneKind.InstanceScreenshot:
            case StandardRightPaneKind.InstanceServer:
            case StandardRightPaneKind.InstanceResource:
                RefreshInstanceContentSurfaces();
                break;
        }
    }

    private void RaiseLauncherStateProperties()
    {
        RaisePropertyChanged(nameof(IsLaunchRoute));
        RaisePropertyChanged(nameof(IsStandardRoute));
        RaisePropertyChanged(nameof(HasSharedRouteSurface));
        RaisePropertyChanged(nameof(ShowSharedRouteFallbackSurface));
        RaisePropertyChanged(nameof(ShowTaskManagerSurface));
        RaisePropertyChanged(nameof(ShowGameLogSurface));
        RaisePropertyChanged(nameof(ShowCompDetailSurface));
        RaisePropertyChanged(nameof(ShowHelpDetailSurface));
        RaisePropertyChanged(nameof(ShowTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowInnerNavigation));
        RaisePropertyChanged(nameof(ShowWindowBranding));
        RaisePropertyChanged(nameof(ShowCenteredTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowLeftAlignedTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowDefaultTitleBarBranding));
        RaisePropertyChanged(nameof(ShowTextTitleBarBranding));
        RaisePropertyChanged(nameof(ShowImageTitleBarBranding));
        RaisePropertyChanged(nameof(TitleBarCustomText));
        RaisePropertyChanged(nameof(TitleBarCustomLogoImage));
        RaisePropertyChanged(nameof(ShowWindowUtilityButtons));
        RaisePropertyChanged(nameof(HasRunningTaskManagerTasks));
        RaisePropertyChanged(nameof(ShowTaskManagerShortcutButton));
        RaisePropertyChanged(nameof(ShowBottomRightUtilityEntryButtons));
        RaisePropertyChanged(nameof(ShowBottomRightPromptQueueButton));
        RaisePropertyChanged(nameof(ShowBottomRightExtraButtons));
        RaisePropertyChanged(nameof(ShowMaximizeButton));
        RaisePropertyChanged(nameof(ShowStandardLeftPane));
        RaisePropertyChanged(nameof(StandardLeftPaneWidth));
        RaisePropertyChanged(nameof(CurrentLeftPaneWidth));
        RaisePropertyChanged(nameof(TitleBarLabel));
        RaisePropertyChanged(nameof(ShowUiFeatureHiddenCard));
        RaisePropertyChanged(nameof(UiFeatureHiddenCardHeader));
        RaisePropertyChanged(nameof(ShowLaunchInstanceManagementButtons));
        RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));

        if (IsCurrentStandardRightPane(StandardRightPaneKind.SetupUpdate))
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
        RaisePropertyChanged(nameof(ShowBottomRightUtilityEntryButtons));
        RaisePropertyChanged(nameof(HasActivityEntries));
        RaisePropertyChanged(nameof(HasInstanceSelectionFolders));
        RaisePropertyChanged(nameof(HasInstanceSelectionSearchBox));
        RaisePropertyChanged(nameof(HasInstanceSelectionEntries));
        RaisePropertyChanged(nameof(HasNoInstanceSelectionEntries));
        RaisePropertyChanged(nameof(HasTaskManagerEntries));
        RaisePropertyChanged(nameof(HasNoTaskManagerEntries));
        RaisePropertyChanged(nameof(HasGameLogFiles));
        RaisePropertyChanged(nameof(HasNoGameLogFiles));
        RaisePropertyChanged(nameof(HasHelpDetailSections));
        RaisePropertyChanged(nameof(HasNoHelpDetailSections));
        RaisePropertyChanged(nameof(HasCommunityProjectDescription));
        RaisePropertyChanged(nameof(HasCommunityProjectSections));
        RaisePropertyChanged(nameof(HasNoCommunityProjectSections));
        switch (CurrentStandardRightPaneDescriptor?.Kind)
        {
            case StandardRightPaneKind.SetupAbout:
                RaisePropertyChanged(nameof(HasAboutProjectEntries));
                RaisePropertyChanged(nameof(HasAboutAcknowledgementEntries));
                break;
            case StandardRightPaneKind.SetupFeedback:
                RaisePropertyChanged(nameof(HasFeedbackSections));
                break;
            case StandardRightPaneKind.SetupJava:
                RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
                break;
            case StandardRightPaneKind.DownloadResource:
                RaisePropertyChanged(nameof(HasDownloadResourceEntries));
                RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
                break;
            case StandardRightPaneKind.DownloadFavorites:
                RaisePropertyChanged(nameof(HasDownloadFavoriteSections));
                RaisePropertyChanged(nameof(HasNoDownloadFavoriteSections));
                break;
            case StandardRightPaneKind.VersionSaveInfo:
                RaisePropertyChanged(nameof(HasVersionSaveInfoEntries));
                RaisePropertyChanged(nameof(HasVersionSaveSettingEntries));
                break;
            case StandardRightPaneKind.VersionSaveBackup:
                RaisePropertyChanged(nameof(HasVersionSaveBackupEntries));
                RaisePropertyChanged(nameof(HasNoVersionSaveBackupEntries));
                break;
            case StandardRightPaneKind.VersionSaveDatapack:
                RaisePropertyChanged(nameof(HasVersionSaveDatapackEntries));
                RaisePropertyChanged(nameof(HasNoVersionSaveDatapackEntries));
                break;
            case StandardRightPaneKind.ToolsHelp:
                RaisePropertyChanged(nameof(HasHelpTopicGroups));
                RaisePropertyChanged(nameof(HasNoHelpTopicGroups));
                RaisePropertyChanged(nameof(HasHelpSearchResults));
                RaisePropertyChanged(nameof(HasNoHelpSearchResults));
                break;
            case StandardRightPaneKind.InstanceOverview:
                RaisePropertyChanged(nameof(HasInstanceOverviewInfoEntries));
                break;
            case StandardRightPaneKind.InstanceExport:
                RaisePropertyChanged(nameof(HasInstanceExportOptionGroups));
                break;
            case StandardRightPaneKind.InstanceWorld:
                RaisePropertyChanged(nameof(HasInstanceWorldEntries));
                RaisePropertyChanged(nameof(HasNoInstanceWorldEntries));
                break;
            case StandardRightPaneKind.InstanceScreenshot:
                RaisePropertyChanged(nameof(HasInstanceScreenshotEntries));
                RaisePropertyChanged(nameof(HasNoInstanceScreenshotEntries));
                break;
            case StandardRightPaneKind.InstanceServer:
                RaisePropertyChanged(nameof(HasInstanceServerEntries));
                RaisePropertyChanged(nameof(HasNoInstanceServerEntries));
                break;
            case StandardRightPaneKind.InstanceResource:
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
        RaisePropertyChanged(nameof(AvailableUpdateName));
        RaisePropertyChanged(nameof(AvailableUpdatePublisher));
        RaisePropertyChanged(nameof(AvailableUpdateSummary));
        RaisePropertyChanged(nameof(CurrentVersionName));
        RaisePropertyChanged(nameof(CurrentVersionDescription));
    }

    private void NotifyTopLevelNavigationInteractionChanged()
    {
        RaisePropertyChanged(nameof(IsTopLevelNavigationInteractive));
        NotifyTopLevelNavigationCanExecuteChanged();
    }

    private void NotifyTopLevelNavigationCanExecuteChanged()
    {
        foreach (var entry in TopLevelEntries)
        {
            entry.Command.NotifyCanExecuteChanged();
        }
    }
}
