using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendShellLocalizationService
{
    public static LauncherFrontendNavigationView LocalizeNavigationView(
        LauncherFrontendNavigationView navigation,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(i18n);

        var topLevelEntries = navigation.TopLevelEntries
            .Select(entry => entry with
            {
                Title = ResolveRouteLabel(entry.Route, i18n),
                Summary = ResolveRouteSummary(entry.Route, i18n)
            })
            .ToArray();

        var sidebarEntries = navigation.SidebarEntries
            .Select(entry => entry with
            {
                Title = ResolveRouteLabel(entry.Route, i18n),
                Summary = ResolveRouteSummary(entry.Route, i18n)
            })
            .ToArray();

        var currentPage = navigation.CurrentPage with
        {
            Title = ResolvePageTitle(navigation.CurrentPage.Route.Page, i18n),
            Summary = ResolvePageSummary(navigation.CurrentPage.Route.Page, i18n),
            SidebarGroupTitle = ResolveSidebarGroupTitle(navigation.CurrentPage.Route.Page, i18n),
            SidebarItemTitle = ResolveSubpageTitle(navigation.CurrentRoute.Subpage, i18n),
            SidebarItemSummary = ResolveSubpageSummary(navigation.CurrentRoute.Subpage, i18n)
        };

        var breadcrumbs = BuildBreadcrumbs(navigation, currentPage, sidebarEntries, i18n);
        var backTarget = navigation.BackTarget is { Route: { } route }
            ? navigation.BackTarget with
            {
                Label = i18n.T(
                    "shell.navigation.utilities.back_target",
                    CreateArgs(("target", ResolveRouteLabel(route, i18n))))
            }
            : navigation.BackTarget;

        var utilityEntries = navigation.UtilityEntries
            .Select(entry => entry with
            {
                Title = ResolveUtilityTitle(entry.Id, i18n)
            })
            .ToArray();

        return navigation with
        {
            CurrentPageTitle = currentPage.Title,
            CurrentPage = currentPage,
            Breadcrumbs = breadcrumbs,
            BackTarget = backTarget,
            TopLevelEntries = topLevelEntries,
            SidebarEntries = sidebarEntries,
            UtilityEntries = utilityEntries
        };
    }

    public static LauncherFrontendPageContent BuildPageContent(
        LauncherFrontendShellPlan shellPlan,
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
            facts.Add(CreateFact(i18n, "shell.page_content.facts.back_target", navigation.BackTarget.Label));
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
            i18n.T("shell.page_content.eyebrows." + navigation.CurrentPage.Kind.ToString().ToLowerInvariant()),
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

    public static string ResolveRouteSummary(LauncherFrontendRoute route, II18nService i18n)
    {
        return route.Subpage != LauncherFrontendSubpageKey.Default
            ? ResolveSubpageSummary(route.Subpage, i18n)
            : ResolvePageSummary(route.Page, i18n);
    }

    private static IReadOnlyList<LauncherFrontendBreadcrumb> BuildBreadcrumbs(
        LauncherFrontendNavigationView navigation,
        LauncherFrontendPageSurface currentPage,
        IReadOnlyList<LauncherFrontendNavigationEntry> sidebarEntries,
        II18nService i18n)
    {
        var breadcrumbs = new List<LauncherFrontendBreadcrumb>();
        if (currentPage.Kind == LauncherFrontendPageKind.TopLevel)
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                currentPage.Title,
                new LauncherFrontendRoute(currentPage.Route.Page)));
        }
        else
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                currentPage.Title,
                navigation.Breadcrumbs.FirstOrDefault()?.Route));
        }

        var selectedSidebarEntry = sidebarEntries.FirstOrDefault(entry => entry.IsSelected);
        if (selectedSidebarEntry is not null)
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                selectedSidebarEntry.Title,
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

    private static string ResolvePageTitle(LauncherFrontendPageKey page, II18nService i18n)
    {
        return i18n.T("shell.navigation.pages." + page.ToString().ToSnakeCase() + ".title");
    }

    private static string ResolvePageSummary(LauncherFrontendPageKey page, II18nService i18n)
    {
        return i18n.T("shell.navigation.pages." + page.ToString().ToSnakeCase() + ".summary");
    }

    private static string ResolveSubpageTitle(LauncherFrontendSubpageKey subpage, II18nService i18n)
    {
        return subpage == LauncherFrontendSubpageKey.Default
            ? string.Empty
            : i18n.T("shell.navigation.subpages." + subpage.ToString().ToSnakeCase() + ".title");
    }

    private static string ResolveSubpageSummary(LauncherFrontendSubpageKey subpage, II18nService i18n)
    {
        return subpage == LauncherFrontendSubpageKey.Default
            ? string.Empty
            : i18n.T("shell.navigation.subpages." + subpage.ToString().ToSnakeCase() + ".summary");
    }

    private static string? ResolveSidebarGroupTitle(LauncherFrontendPageKey page, II18nService i18n)
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

    private static string ResolveUtilityTitle(string id, II18nService i18n)
    {
        return i18n.T("shell.navigation.utilities." + id.Replace('-', '_'));
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
