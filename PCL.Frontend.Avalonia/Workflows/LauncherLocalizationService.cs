using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class LauncherLocalizationService
{
    public static string DescribeLauncherStatus(
        LauncherFrontendPlan shellPlan,
        int navigationDepth,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(shellPlan);
        ArgumentNullException.ThrowIfNull(i18n);

        return i18n.T(
            "shell.navigation.status.line",
            CreateArgs(
                ("command", ResolveImmediateCommandLabel(shellPlan.StartupPlan.ImmediateCommand.Kind, i18n)),
                ("splash", ResolveSplashStateLabel(shellPlan.StartupPlan.Visual.ShouldShowSplashScreen, i18n)),
                ("depth", navigationDepth)));
    }

    public static string DescribeSurfaceMeta(
        LauncherFrontendNavigationView navigation,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(i18n);

        var sidebarGroup = ResolveSidebarGroupTitle(navigation.CurrentPage.Route.Page, i18n)
            ?? i18n.T("shell.navigation.surface_meta.no_sidebar_group");
        var navigationState = navigation.ShowsBackButton
            ? navigation.BackTarget is { Route: { } route }
                ? ResolveBackTargetLabel(route, i18n)
                : i18n.T("shell.navigation.surface_meta.back_available")
            : i18n.T("shell.navigation.surface_meta.top_level_route");

        return i18n.T(
            "shell.navigation.surface_meta.line",
            CreateArgs(
                ("kind", ResolveSurfaceKindLabel(navigation.CurrentPage.Kind, i18n)),
                ("sidebar_group", sidebarGroup),
                ("navigation_state", navigationState)));
    }

    public static string DescribeNavigationActivity(
        string target,
        string source,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(i18n);

        return i18n.T(
            "shell.navigation.activities.navigated",
            CreateArgs(
                ("target", target),
                ("source", i18n.T("shell.navigation.activities.sources." + source))));
    }

    public static string DescribeUtilitySummary(bool isSelected, II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(i18n);

        return i18n.T(
            isSelected
                ? "shell.navigation.utilities.active_summary"
                : "shell.navigation.utilities.pinned_summary");
    }

    public static string DescribeOpenedUtilitySurface(string target, II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(i18n);

        return i18n.T("shell.navigation.activities.opened_utility", CreateArgs(("target", target)));
    }

    public static string DescribeStayedOnCurrentRoute(II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(i18n);

        return i18n.T("shell.navigation.activities.stayed_current");
    }

    public static string DescribeFollowedBackTarget(LauncherFrontendRoute route, II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(i18n);

        return i18n.T(
            "shell.navigation.activities.followed_back_target",
            CreateArgs(("target", ResolveRouteLabel(route, i18n))));
    }

    public static string DescribeReturnedHome(LauncherFrontendRoute route, II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(i18n);

        return i18n.T(
            "shell.navigation.activities.returned_home",
            CreateArgs(("target", ResolveRouteLabel(route, i18n))));
    }

    public static LauncherFrontendNavigationView LocalizeNavigationView(
        LauncherFrontendNavigationView navigation,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(i18n);

        var breadcrumbs = BuildBreadcrumbs(navigation, i18n);

        return navigation with { Breadcrumbs = breadcrumbs };
    }

    public static LauncherFrontendPageContent BuildPageContent(
        LauncherFrontendPlan shellPlan,
        LauncherFrontendNavigationView navigation,
        IReadOnlyList<LauncherFrontendPromptLaneSummary> promptLanes,
        LauncherFrontendLaunchSurfaceData? launch,
        LauncherFrontendCrashSurfaceData? crash,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(shellPlan);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(promptLanes);
        ArgumentNullException.ThrowIfNull(i18n);

        var promptTotal = promptLanes.Sum(lane => lane.Count);
        var selectedLaneTitle = promptLanes.FirstOrDefault(lane => lane.IsSelected)?.Title;
        var facts = new List<LauncherFrontendPageFact>
        {
            CreateFact(i18n, "shell.page_content.facts.current_page", ResolvePageTitle(navigation.CurrentRoute.Page, i18n)),
            CreateFact(i18n, "shell.page_content.facts.current_section", ResolveRouteLabel(navigation.CurrentRoute, i18n)),
            CreateFact(i18n, "shell.page_content.facts.prompt_count", promptTotal.ToString()),
            CreateFact(i18n, "shell.page_content.facts.active_prompt_lane", selectedLaneTitle ?? i18n.T("shell.page_content.values.none")),
            CreateFact(i18n, "shell.page_content.facts.page_kind", navigation.CurrentPage.Kind.ToString())
        };

        if (navigation.BackTarget is not null)
        {
            var backTargetLabel = ResolveBackTargetLabel(navigation.BackTarget.Route, i18n);
            facts.Add(CreateFact(i18n, "shell.page_content.facts.back_target", backTargetLabel));
        }

        AddRouteSpecificFacts(facts, launch, crash, navigation.CurrentRoute.Page, i18n);

        var lines = new List<string>
        {
            ResolveRouteSummary(navigation.CurrentRoute, i18n)
        };

        if (!string.IsNullOrWhiteSpace(selectedLaneTitle))
        {
            lines.Add(i18n.T(
                "shell.page_content.lines.active_prompt_lane",
                CreateArgs(("lane", selectedLaneTitle))));
        }

        AddRouteSpecificLines(lines, launch, crash, navigation.CurrentRoute.Page, i18n);

        var routeLabel = ResolveRouteLabel(navigation.CurrentRoute, i18n);
        return new LauncherFrontendPageContent(
            i18n.T("shell.page_content.eyebrows." + ResolvePageKindEyebrowKey(navigation.CurrentPage.Kind)),
            ResolveRouteSummary(navigation.CurrentRoute, i18n),
            facts,
            [
                new LauncherFrontendPageSection(
                    i18n.T("shell.page_content.sections.overview.eyebrow"),
                    routeLabel,
                    lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray())
            ]);
    }

    public static string ResolveRouteLabel(LauncherFrontendRoute route, II18nService i18n)
    {
        return route.Subpage != LauncherFrontendSubpageKey.Default
            ? ResolveSubpageTitle(route.Subpage, i18n)
            : ResolvePageTitle(route.Page, i18n);
    }

    private static string ResolvePageKindEyebrowKey(LauncherFrontendPageKind kind)
    {
        return kind switch
        {
            LauncherFrontendPageKind.TopLevel => "top_level",
            LauncherFrontendPageKind.Secondary => "secondary",
            LauncherFrontendPageKind.Detail => "detail",
            LauncherFrontendPageKind.Utility => "utility",
            _ => "top_level"
        };
    }

    public static string ResolveRouteSummary(LauncherFrontendRoute route, II18nService i18n)
    {
        return route.Subpage != LauncherFrontendSubpageKey.Default
            ? ResolveSubpageSummary(route.Subpage, i18n)
            : ResolvePageSummary(route.Page, i18n);
    }

    public static string ResolveTitleBarLabel(LauncherFrontendNavigationView navigation, II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(i18n);

        return navigation.CurrentRoute.Subpage != LauncherFrontendSubpageKey.Default
            ? ResolveSubpageTitle(navigation.CurrentRoute.Subpage, i18n)
            : ResolvePageTitle(navigation.CurrentRoute.Page, i18n);
    }

    public static string ResolvePageTitle(LauncherFrontendPageKey page, II18nService i18n)
    {
        return i18n.T("shell.navigation.pages." + page.ToString().ToSnakeCase() + ".title");
    }

    public static string ResolvePageSummary(LauncherFrontendPageKey page, II18nService i18n)
    {
        return i18n.T("shell.navigation.pages." + page.ToString().ToSnakeCase() + ".summary");
    }

    public static string ResolveSubpageTitle(LauncherFrontendSubpageKey subpage, II18nService i18n)
    {
        return subpage == LauncherFrontendSubpageKey.Default
            ? string.Empty
            : i18n.T("shell.navigation.subpages." + subpage.ToString().ToSnakeCase() + ".title");
    }

    public static string ResolveSubpageSummary(LauncherFrontendSubpageKey subpage, II18nService i18n)
    {
        return subpage == LauncherFrontendSubpageKey.Default
            ? string.Empty
            : i18n.T("shell.navigation.subpages." + subpage.ToString().ToSnakeCase() + ".summary");
    }

    public static string? ResolveSidebarGroupTitle(LauncherFrontendPageKey page, II18nService i18n)
    {
        var key = page switch
        {
            LauncherFrontendPageKey.Download or LauncherFrontendPageKey.CompDetail => "download",
            LauncherFrontendPageKey.Setup => "setup",
            LauncherFrontendPageKey.Tools or LauncherFrontendPageKey.HelpDetail => "tools",
            LauncherFrontendPageKey.InstanceSetup => "instance_setup",
            LauncherFrontendPageKey.VersionSaves => "version_saves",
            _ => null
        };

        return key is null ? null : i18n.T("shell.navigation.sidebar_groups." + key);
    }

    public static string ResolveUtilityTitle(string id, II18nService i18n)
    {
        return i18n.T("shell.navigation.utilities." + id.Replace('-', '_'));
    }

    public static string ResolveBackTargetLabel(LauncherFrontendRoute? route, II18nService i18n)
    {
        return route is null
            ? string.Empty
            : i18n.T(
                "shell.navigation.utilities.back_target",
                CreateArgs(("target", ResolveRouteLabel(route, i18n))));
    }

    private static IReadOnlyList<LauncherFrontendBreadcrumb> BuildBreadcrumbs(
        LauncherFrontendNavigationView navigation,
        II18nService i18n)
    {
        var breadcrumbs = new List<LauncherFrontendBreadcrumb>();
        if (navigation.CurrentPage.Kind == LauncherFrontendPageKind.TopLevel)
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                ResolvePageTitle(navigation.CurrentPage.Route.Page, i18n),
                new LauncherFrontendRoute(navigation.CurrentPage.Route.Page)));
        }
        else
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                ResolvePageTitle(navigation.CurrentPage.Route.Page, i18n),
                navigation.Breadcrumbs.FirstOrDefault()?.Route));
        }

        var selectedSidebarEntry = navigation.SidebarEntries.FirstOrDefault(entry => entry.IsSelected);
        if (selectedSidebarEntry is not null)
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                ResolveRouteLabel(selectedSidebarEntry.Route, i18n),
                selectedSidebarEntry.Route));
        }

        return breadcrumbs;
    }

    private static LauncherFrontendPageFact CreateFact(II18nService i18n, string labelKey, string value)
    {
        return new LauncherFrontendPageFact(i18n.T(labelKey), value);
    }

    private static void AddRouteSpecificFacts(
        ICollection<LauncherFrontendPageFact> facts,
        LauncherFrontendLaunchSurfaceData? launch,
        LauncherFrontendCrashSurfaceData? crash,
        LauncherFrontendPageKey page,
        II18nService i18n)
    {
        if (page == LauncherFrontendPageKey.Launch && launch is not null)
        {
            facts.Add(CreateFact(i18n, "shell.page_content.facts.launch.identity", launch.SelectedIdentityLabel));
            facts.Add(CreateFact(i18n, "shell.page_content.facts.launch.java", launch.JavaRuntimeLabel));
            facts.Add(CreateFact(i18n, "shell.page_content.facts.launch.resolution", launch.ResolutionLabel));
            facts.Add(CreateFact(i18n, "shell.page_content.facts.launch.classpath_count", launch.ClasspathEntryCount.ToString()));
            if (!string.IsNullOrWhiteSpace(launch.JavaWarningMessage))
            {
                facts.Add(CreateFact(i18n, "shell.page_content.facts.launch.java_warning", launch.JavaWarningMessage));
            }
        }

        if (page == LauncherFrontendPageKey.GameLog && crash is not null)
        {
            facts.Add(CreateFact(i18n, "shell.page_content.facts.crash.archive_name", crash.SuggestedArchiveName));
            facts.Add(CreateFact(i18n, "shell.page_content.facts.crash.source_file_count", crash.SourceFileCount.ToString()));
        }
    }

    private static void AddRouteSpecificLines(
        ICollection<string> lines,
        LauncherFrontendLaunchSurfaceData? launch,
        LauncherFrontendCrashSurfaceData? crash,
        LauncherFrontendPageKey page,
        II18nService i18n)
    {
        if (page == LauncherFrontendPageKey.Launch && launch is not null)
        {
            lines.Add(i18n.T(
                "shell.page_content.lines.launch.runtime",
                CreateArgs(
                    ("java", launch.JavaRuntimeLabel),
                    ("resolution", launch.ResolutionLabel))));

            if (!string.IsNullOrWhiteSpace(launch.CompletionMessage))
            {
                lines.Add(i18n.T(
                    "shell.page_content.lines.launch.last_completion",
                    CreateArgs(("message", launch.CompletionMessage))));
            }
        }

        if (page == LauncherFrontendPageKey.GameLog && crash is not null)
        {
            lines.Add(i18n.T(
                "shell.page_content.lines.crash.export",
                CreateArgs(
                    ("archive_name", crash.SuggestedArchiveName),
                    ("file_count", crash.SourceFileCount))));
        }
    }

    private static string ResolveImmediateCommandLabel(LauncherStartupImmediateCommandKind kind, II18nService i18n)
    {
        return i18n.T("shell.navigation.status.commands." + kind.ToString().ToSnakeCase());
    }

    private static string ResolveSplashStateLabel(bool showsSplashScreen, II18nService i18n)
    {
        return i18n.T(
            showsSplashScreen
                ? "shell.navigation.status.splash_on"
                : "shell.navigation.status.splash_off");
    }

    private static string ResolveSurfaceKindLabel(LauncherFrontendPageKind kind, II18nService i18n)
    {
        return i18n.T("shell.navigation.surface_meta.kinds." + kind.ToString().ToSnakeCase());
    }

    private static IReadOnlyDictionary<string, object?> CreateArgs(params (string Name, object? Value)[] values)
    {
        return values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
    }

    private static string ToSnakeCase(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current))
            {
                if (index > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
