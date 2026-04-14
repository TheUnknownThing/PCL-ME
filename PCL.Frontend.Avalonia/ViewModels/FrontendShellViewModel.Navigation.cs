using System.IO;
using System.Collections.ObjectModel;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void RefreshShell(string activityMessage)
    {
        var shellPlan = BuildShellPlan();
        var sidebarNavigation = ResolveSidebarNavigation(shellPlan.Navigation);
        var pageContent = BuildPageContent(shellPlan);
        RefreshCurrentDedicatedGenericRouteSurface();
        var surfaceFacts = pageContent.Facts;
        var surfaceSections = pageContent.Sections;
        var eyebrow = pageContent.Eyebrow;
        var description = pageContent.Summary;
        _currentNavigation = shellPlan.Navigation;

        if (TryBuildDedicatedGenericRouteMetadata(out var dedicatedMetadata))
        {
            eyebrow = dedicatedMetadata.Eyebrow;
            description = dedicatedMetadata.Description;
            surfaceFacts = dedicatedMetadata.Facts;
            surfaceSections = [];
        }

        Eyebrow = eyebrow;
        Title = shellPlan.Navigation.CurrentPage.Title;
        Description = description;
        Status = $"Immediate command: {shellPlan.StartupPlan.ImmediateCommand.Kind} | Splash: {(shellPlan.StartupPlan.Visual.ShouldShowSplashScreen ? "on" : "off")} | Navigation depth: {_routeAncestors.Count}";
        BreadcrumbTrail = string.Join(" / ", shellPlan.Navigation.Breadcrumbs.Select(crumb => crumb.Title));
        SurfaceMeta = $"{shellPlan.Navigation.CurrentPage.Kind} surface • {(shellPlan.Navigation.CurrentPage.SidebarGroupTitle ?? "No sidebar group")} • {(shellPlan.Navigation.ShowsBackButton ? shellPlan.Navigation.BackTarget?.Label ?? "Back available" : "Top-level route")}";
        CanGoBack = shellPlan.Navigation.ShowsBackButton && shellPlan.Navigation.BackTarget is not null;
        CanGoHome = ResolveHomeRoute() is not null;

        ReplaceNavigationEntriesIfChanged(TopLevelEntries, shellPlan.Navigation.TopLevelEntries, NavigationVisualStyle.TopLevel);
        ReplaceNavigationEntriesIfChanged(SidebarEntries, sidebarNavigation.SidebarEntries, NavigationVisualStyle.Sidebar);
        ReplaceSidebarSectionsIfChanged(sidebarNavigation);
        ReplaceUtilityEntriesIfChanged(shellPlan.Navigation.UtilityEntries.Where(entry => entry.IsVisible).ToArray());
        ReplaceSurfaceFactsIfChanged(surfaceFacts);
        ReplaceSurfaceSectionsIfChanged(surfaceSections);

        SelectPromptLane(_selectedPromptLane, updateActivity: false, raiseCollectionState: false);
        RefreshStandardShellPanes();
        RefreshActiveRightPaneSurface();
        RaiseCollectionStateProperties();
        AddActivity(activityMessage, $"{shellPlan.Navigation.CurrentPage.Title} • {shellPlan.Navigation.CurrentPage.Route.Page}/{shellPlan.Navigation.CurrentPage.Route.Subpage}");
        RaiseShellStateProperties();
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
        return FrontendUiVisibilityService.FilterNavigationView(
            LauncherFrontendNavigationService.BuildView(request),
            GetUiVisibilityPreferences());
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
        var (iconPath, iconScale) = GetNavigationIcon(entry.Title);
        return new NavigationEntryViewModel(
            entry.Title,
            entry.Summary,
            style == NavigationVisualStyle.Sidebar ? entry.Route.Subpage.ToString() : entry.Route.Page.ToString(),
            entry.IsSelected,
            iconPath,
            iconScale,
            GetNavigationPalette(entry.IsSelected, style),
            new ActionCommand(() => NavigateTo(
                entry.Route,
                $"Navigated to {entry.Title} from the {(style == NavigationVisualStyle.Sidebar ? "sidebar" : "top bar")}.",
                style == NavigationVisualStyle.Sidebar
                    ? RouteNavigationBehavior.Lateral
                    : RouteNavigationBehavior.Reset),
                () => style != NavigationVisualStyle.TopLevel || IsTopLevelNavigationInteractive)
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
            entry.Id == "back"
                ? new ActionCommand(NavigateBack, () => CanGoBack)
                : new ActionCommand(() => NavigateTo(entry.Route, $"Opened utility surface {entry.Title}.", RouteNavigationBehavior.Child)));
    }

    private IEnumerable<SidebarSectionViewModel> BuildSidebarSections(LauncherFrontendNavigationView navigation)
    {
        if (navigation.SidebarEntries.Count == 0)
        {
            return [];
        }

        var itemIndex = 0;
        return navigation.SidebarEntries
            .GroupBy(entry => GetSidebarSectionTitle(navigation.CurrentRoute.Page, entry.Route.Subpage))
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
                        var (iconPath, iconScale) = GetSidebarIcon(entry.Route.Page, entry.Route.Subpage, entry.Title);
                        var accessory = GetSidebarAccessory(entry.Route.Page, entry.Route.Subpage, entry.Title);
                        return new SidebarListItemViewModel(
                            entry.Title,
                            entry.Summary,
                            entry.IsSelected,
                            iconPath,
                            iconScale,
                            itemIndex++ * 28,
                            new ActionCommand(() => NavigateTo(entry.Route, $"Navigated to {entry.Title} from the launcher-style left pane.", RouteNavigationBehavior.Lateral)),
                            accessory.ToolTip,
                            accessory.IconPath,
                            accessory.Command is null
                                ? null
                                : new ActionCommand(() => ApplySidebarAccessory(entry.Title, accessory.ActionLabel, accessory.Command)));
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
            .GroupBy(entry => GetSidebarSectionTitle(navigation.CurrentRoute.Page, entry.Route.Subpage))
            .Select(group => new SidebarSectionSnapshot(
                group.Key,
                !string.IsNullOrWhiteSpace(group.Key),
                group.Select(entry =>
                {
                    var (iconPath, iconScale) = GetSidebarIcon(entry.Route.Page, entry.Route.Subpage, entry.Title);
                    var accessory = GetSidebarAccessory(entry.Route.Page, entry.Route.Subpage, entry.Title);
                    return new SidebarItemSnapshot(
                        entry.Title,
                        entry.Summary,
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
        var (iconPath, iconScale) = GetNavigationIcon(entry.Title);
        var meta = style == NavigationVisualStyle.Sidebar
            ? entry.Route.Subpage.ToString()
            : entry.Route.Page.ToString();

        return current.Title == entry.Title
            && current.Summary == entry.Summary
            && current.Meta == meta
            && current.IsSelected == entry.IsSelected
            && current.IconPath == iconPath
            && current.IconScale.Equals(iconScale);
    }

    private bool MatchesUtilityEntry(NavigationEntryViewModel current, LauncherFrontendUtilityEntry entry)
    {
        var meta = entry.Id switch
        {
            "back" => "返",
            "task-manager" => "任",
            "game-log" => "志",
            _ => entry.Route.Page.ToString()
        };

        return current.Title == entry.Title
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

    private LauncherFrontendShellPlan BuildShellPlan()
    {
        var normalizedCurrentRoute = NormalizeRoute(_currentRoute);
        if (normalizedCurrentRoute != _currentRoute)
        {
            _currentRoute = normalizedCurrentRoute;
        }

        var plan = LauncherFrontendShellService.BuildPlan(new LauncherFrontendShellRequest(
            _shellComposition.StartupWorkflowRequest,
            _shellComposition.StartupConsentRequest,
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
        return _shellComposition.NavigationRequest with
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
        var runtimePaths = _shellActionService.RuntimePaths;
        var platformAdapter = _shellActionService.PlatformAdapter;

        return HasLaunchLogLines
            || FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                runtimePaths.LauncherAppDataDirectory,
                platformAdapter).Any(File.Exists)
            || FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                Path.Combine(runtimePaths.ExecutableDirectory, "PCL"),
                platformAdapter).Any(File.Exists)
            || Directory.Exists(Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log"))
            || Directory.Exists(Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log"));
    }

    private void RefreshDynamicUtilityEntries()
    {
        var navigation = LauncherFrontendNavigationService.BuildView(BuildCurrentNavigationRequest());
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
            AddActivity("Stayed on the current route.", $"{route.Page}/{route.Subpage}");
            return;
        }

        ApplyRouteNavigation(route, behavior);
        ChangeRoute(route, activityMessage, ShellNavigationTransitionDirection.Forward);
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

            ChangeRoute(backRoute, $"Followed shell back target to {backRoute.Page}.", ShellNavigationTransitionDirection.Backward);
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
        ChangeRoute(homeRoute, $"已返回到 {homeRoute.Page}。", ShellNavigationTransitionDirection.Backward);
    }

    private void ChangeRoute(
        LauncherFrontendRoute route,
        string activityMessage,
        ShellNavigationTransitionDirection direction)
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
        RefreshShell(activityMessage);
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
            var requiredLoadMode = ResolveInstanceCompositionLoadMode(route);
            if (previousRoute.Page != LauncherFrontendPageKey.InstanceSetup
                || !HasSufficientInstanceCompositionLoadMode(requiredLoadMode))
            {
                ReloadInstanceComposition(
                    requiredLoadMode,
                    reloadDependentCompositions: false,
                    initializeAllSurfaces: false);
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
            case StandardShellRightPaneKind.Generic:
                RefreshCurrentDedicatedGenericRouteSurface();
                break;
            case StandardShellRightPaneKind.TaskManager:
                RefreshTaskManagerSurface();
                break;
            case StandardShellRightPaneKind.DownloadInstall:
                RefreshDownloadInstallSurface();
                break;
            case StandardShellRightPaneKind.DownloadCatalog:
                RefreshDownloadCatalogSurface();
                break;
            case StandardShellRightPaneKind.DownloadResource:
                RefreshDownloadResourceSurface();
                break;
            case StandardShellRightPaneKind.DownloadFavorites:
                RefreshDownloadFavoriteSurface();
                break;
            case StandardShellRightPaneKind.InstanceSelection:
                RefreshInstanceSelectionSurface();
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
        RaisePropertyChanged(nameof(ShowStandardShellLeftPane));
        RaisePropertyChanged(nameof(StandardShellLeftPaneWidth));
        RaisePropertyChanged(nameof(CurrentShellLeftPaneWidth));
        RaisePropertyChanged(nameof(TitleBarLabel));
        RaisePropertyChanged(nameof(ShowUiFeatureHiddenCard));
        RaisePropertyChanged(nameof(UiFeatureHiddenCardHeader));
        RaisePropertyChanged(nameof(ShowLaunchInstanceManagementButtons));
        RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
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
                RaisePropertyChanged(nameof(HasHelpSearchResults));
                RaisePropertyChanged(nameof(HasNoHelpSearchResults));
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
