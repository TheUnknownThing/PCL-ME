using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.App.Essentials;

public static class LauncherFrontendPageContentService
{
    public static LauncherFrontendPageContent Build(LauncherFrontendPageContentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Navigation);
        ArgumentNullException.ThrowIfNull(request.StartupPlan);
        ArgumentNullException.ThrowIfNull(request.Consent);
        ArgumentNullException.ThrowIfNull(request.PromptLanes);

        var promptTotal = request.PromptLanes.Sum(lane => lane.Count);
        var selectedLane = request.PromptLanes.FirstOrDefault(lane => lane.IsSelected);
        var visibleUtilityCount = request.Navigation.UtilityEntries.Count(entry => entry.IsVisible);

        return request.Navigation.CurrentRoute.Page switch
        {
            LauncherFrontendPageKey.Launch or LauncherFrontendPageKey.InstanceSelect =>
                BuildLaunchContent(request, promptTotal, selectedLane?.Title),
            LauncherFrontendPageKey.Download or LauncherFrontendPageKey.CompDetail or LauncherFrontendPageKey.HomePageMarket =>
                BuildDownloadContent(request, promptTotal, visibleUtilityCount, selectedLane?.Title),
            LauncherFrontendPageKey.Setup =>
                BuildSetupContent(request, promptTotal),
            LauncherFrontendPageKey.Tools or LauncherFrontendPageKey.HelpDetail =>
                BuildToolsContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendPageKey.TaskManager =>
                BuildTaskManagerContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendPageKey.GameLog =>
                BuildGameLogContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendPageKey.InstanceSetup =>
                BuildInstanceContent(request, promptTotal),
            LauncherFrontendPageKey.VersionSaves =>
                BuildSavesContent(request, promptTotal),
            _ => BuildGenericContent(request, promptTotal, visibleUtilityCount)
        };
    }

    private static LauncherFrontendPageContent BuildLaunchContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        string? selectedLaneTitle)
    {
        var launch = request.Launch;
        var title = request.Navigation.CurrentPage.SidebarItemTitle ?? request.Navigation.CurrentPage.Title;

        return new LauncherFrontendPageContent(
            "Launch migration surface",
            "Account, Java, and prerun state are coming from portable launch plans so the replacement frontend can render readiness without rebuilding launcher policy.",
            [
                new LauncherFrontendPageFact("Surface", title),
                new LauncherFrontendPageFact("Login", launch?.LoginProviderLabel ?? "Not provided"),
                new LauncherFrontendPageFact("Java", launch?.JavaRuntimeLabel ?? "Not provided"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Readiness",
                    "Launch readiness",
                    [
                        $"Scenario: {launch?.ScenarioLabel ?? "Unknown scenario"}",
                        $"Identity surface: {launch?.SelectedIdentityLabel ?? "Profile/auth contract still pending"}",
                        $"Login workflow steps: {launch?.LoginStepCount.ToString() ?? "n/a"}",
                        $"Focused prompt lane: {selectedLaneTitle ?? "None"}"
                    ]),
                new LauncherFrontendPageSection(
                    "Runtime",
                    "Java and resolution",
                    [
                        $"Runtime target: {launch?.JavaRuntimeLabel ?? "No Java summary"}",
                        $"Download prompt target: {launch?.JavaDownloadTarget ?? "No download prompt queued"}",
                        $"Resolution plan: {launch?.ResolutionLabel ?? "No resolution summary"}",
                        $"Classpath entries: {launch?.ClasspathEntryCount.ToString() ?? "n/a"} | Replacement values: {launch?.ReplacementValueCount.ToString() ?? "n/a"}"
                    ]),
                new LauncherFrontendPageSection(
                    "Files",
                    "Prerun files",
                    [
                        $"options.txt target: {FormatPath(launch?.OptionsTargetFilePath)}",
                        $"launcher_profiles write: {FormatBool(launch?.WritesLauncherProfiles)}",
                        $"Script export: {FormatPath(launch?.ScriptExportPath) ?? "No export requested"}",
                        $"Natives directory: {FormatPath(launch?.NativesDirectory)}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildDownloadContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount,
        string? selectedLaneTitle)
    {
        var surface = request.Navigation.CurrentPage;

        return new LauncherFrontendPageContent(
            "Download migration surface",
            "The shell can already route download subpages from the portable catalog. The next backend seam is route-specific catalog and install-plan data.",
            [
                new LauncherFrontendPageFact("Surface", surface.SidebarItemTitle ?? surface.Title),
                new LauncherFrontendPageFact("Sidebar routes", request.Navigation.SidebarEntries.Count.ToString()),
                new LauncherFrontendPageFact("Utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Route",
                    "Current install surface",
                    [
                        $"Top-level page: {surface.Title}",
                        $"Selected subpage: {surface.SidebarItemTitle ?? "Default"}",
                        $"Subpage summary: {surface.SidebarItemSummary ?? "No subpage summary"}",
                        $"Focused prompt lane: {selectedLaneTitle ?? "None"}"
                    ]),
                new LauncherFrontendPageSection(
                    "Migration",
                    "What this frontend already proves",
                    [
                        "Route selection, breadcrumbs, and back behavior are portable.",
                        "Sidebar composition no longer depends on WPF-only page wiring.",
                        "Prompt inbox and utility surfaces stay outside install policy."
                    ]),
                new LauncherFrontendPageSection(
                    "Next seam",
                    "Backend data still needed",
                    [
                        "Search, category filters, and resource cards should come from backend-facing contracts.",
                        "Install planning should stay outside the desktop shell.",
                        "Selection state should eventually bind to real download/auth surfaces, not fixture-only summaries."
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var launch = request.Launch;
        var startup = request.StartupPlan;

        return new LauncherFrontendPageContent(
            "Settings migration surface",
            "Bootstrap defaults, disclosure prompts, and Java-facing settings can be surfaced without pulling the old window lifecycle into the new frontend.",
            [
                new LauncherFrontendPageFact("Update channel", startup.Bootstrap.DefaultUpdateChannel.ToString()),
                new LauncherFrontendPageFact("Config keys", startup.Bootstrap.ConfigKeysToLoad.Count.ToString()),
                new LauncherFrontendPageFact("Consent prompts", request.Consent.Prompts.Count.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Startup",
                    "Startup bootstrap",
                    [
                        $"Directories to create: {startup.Bootstrap.DirectoriesToCreate.Count}",
                        $"Legacy logs to clean: {startup.Bootstrap.LegacyLogFilesToDelete.Count}",
                        $"Immediate command: {startup.ImmediateCommand.Kind}",
                        $"Environment warning: {startup.Bootstrap.EnvironmentWarningMessage ?? "None"}"
                    ]),
                new LauncherFrontendPageSection(
                    "Consent",
                    "Consent and disclosure",
                    [
                        $"Consent prompts: {request.Consent.Prompts.Count}",
                        $"Splash enabled: {FormatBool(startup.Visual.ShouldShowSplashScreen)}",
                        "Telemetry, EULA, and special-build notices are modeled as prompt contracts.",
                        "The frontend only needs to render choices and emit intents."
                    ]),
                new LauncherFrontendPageSection(
                    "Runtime",
                    "Java-facing settings seam",
                    [
                        $"Recommended runtime: {launch?.JavaRuntimeLabel ?? "No Java summary"}",
                        $"Download target: {launch?.JavaDownloadTarget ?? "No download target"}",
                        $"Resolution baseline: {launch?.ResolutionLabel ?? "No resolution summary"}",
                        $"Prerun options target: {FormatPath(launch?.OptionsTargetFilePath)}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildToolsContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        var crash = request.Crash;
        var surface = request.Navigation.CurrentPage;

        return new LauncherFrontendPageContent(
            "Tools migration surface",
            "Help, diagnostics, and experiments can stay lightweight frontend shells while OS actions and recovery planning remain explicit intents.",
            [
                new LauncherFrontendPageFact("Surface", surface.SidebarItemTitle ?? surface.Title),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Crash archive", crash?.SuggestedArchiveName ?? "Not provided"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Diagnostics",
                    "Frontend utility surfaces",
                    [
                        $"Back target: {request.Navigation.BackTarget?.Label ?? "None"}",
                        $"Has sidebar group: {FormatBool(surface.HasSidebar)}",
                        $"Current page kind: {surface.Kind}",
                        "Task manager and log views remain utility pages, not policy owners."
                    ]),
                new LauncherFrontendPageSection(
                    "Recovery",
                    "Crash and export handoff",
                    [
                        $"Suggested archive: {crash?.SuggestedArchiveName ?? "No archive suggestion"}",
                        $"Crash source files: {crash?.SourceFileCount.ToString() ?? "n/a"}",
                        $"Launcher log included: {FormatBool(crash?.IncludesLauncherLog)}",
                        $"Launcher log path: {FormatPath(crash?.LauncherLogPath)}"
                    ]),
                new LauncherFrontendPageSection(
                    "Migration",
                    "Frontend ownership boundaries",
                    [
                        "Desktop UI owns composition, navigation, and user intent collection.",
                        "Backend services still own diagnostics, export planning, and workflow policy.",
                        "Tool pages are a good place to harden those seams before broader cutover."
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildTaskManagerContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        var launch = request.Launch;
        var crash = request.Crash;

        return new LauncherFrontendPageContent(
            "Task manager migration surface",
            "Background work can now be visualized as explicit launch and recovery summaries rather than hidden window-thread state.",
            [
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Login steps", launch?.LoginStepCount.ToString() ?? "n/a"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString()),
                new LauncherFrontendPageFact("Crash files", crash?.SourceFileCount.ToString() ?? "n/a")
            ],
            [
                new LauncherFrontendPageSection(
                    "Launch",
                    "Launch workflow signals",
                    [
                        $"Scenario: {launch?.ScenarioLabel ?? "Unknown scenario"}",
                        $"Runtime target: {launch?.JavaRuntimeLabel ?? "No Java summary"}",
                        $"Completion note: {launch?.CompletionMessage ?? "No completion summary"}",
                        $"Script export requested: {FormatBool(launch?.HasScriptExport)}"
                    ]),
                new LauncherFrontendPageSection(
                    "Transfers",
                    "File and runtime work",
                    [
                        $"Classpath entries: {launch?.ClasspathEntryCount.ToString() ?? "n/a"}",
                        $"Replacement values: {launch?.ReplacementValueCount.ToString() ?? "n/a"}",
                        $"Natives directory: {FormatPath(launch?.NativesDirectory)}",
                        $"options.txt target: {FormatPath(launch?.OptionsTargetFilePath)}"
                    ]),
                new LauncherFrontendPageSection(
                    "Review",
                    "Shell review artifacts",
                    [
                        "This utility page should eventually bind to live task collections.",
                        "For now it can already review portable workflow summaries and artifact destinations.",
                        $"Crash archive suggestion: {crash?.SuggestedArchiveName ?? "No crash archive summary"}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildGameLogContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        var crash = request.Crash;
        var launch = request.Launch;

        return new LauncherFrontendPageContent(
            "Game log migration surface",
            "Live log viewing is a frontend utility surface. Collection, crash preparation, and export policy remain backend responsibilities.",
            [
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Crash archive", crash?.SuggestedArchiveName ?? "Not provided"),
                new LauncherFrontendPageFact("Launcher log", FormatBool(crash?.IncludesLauncherLog)),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Routing",
                    "Log surface entry",
                    [
                        $"Prompt route available: {request.Navigation.UtilityEntries.Any(entry => entry.Id == "game-log" && entry.IsVisible)}",
                        $"Back target: {request.Navigation.BackTarget?.Label ?? "None"}",
                        $"Launch completion note: {launch?.CompletionMessage ?? "No completion note"}",
                        "Prompt actions can route the shell here without copying WPF navigation glue."
                    ]),
                new LauncherFrontendPageSection(
                    "Recovery",
                    "Crash review handoff",
                    [
                        $"Crash source files: {crash?.SourceFileCount.ToString() ?? "n/a"}",
                        $"Launcher log path: {FormatPath(crash?.LauncherLogPath)}",
                        $"Suggested archive: {crash?.SuggestedArchiveName ?? "No archive suggestion"}",
                        "Export remains an explicit shell intent instead of a hidden side effect."
                    ]),
                new LauncherFrontendPageSection(
                    "Boundary",
                    "What remains portable",
                    [
                        "The frontend owns display, filters, and reveal actions.",
                        "The backend owns collection, crash classification, and export planning.",
                        "That boundary keeps the log surface replaceable across shells."
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildInstanceContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var launch = request.Launch;
        var surface = request.Navigation.CurrentPage;

        return new LauncherFrontendPageContent(
            "Instance migration surface",
            "Instance pages can now render around portable launch artifacts while the backend continues owning file mutations and launch semantics.",
            [
                new LauncherFrontendPageFact("Subpage", surface.SidebarItemTitle ?? "Overview"),
                new LauncherFrontendPageFact("Classpath", launch?.ClasspathEntryCount.ToString() ?? "n/a"),
                new LauncherFrontendPageFact("Replacement values", launch?.ReplacementValueCount.ToString() ?? "n/a"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Instance",
                    "Current instance route",
                    [
                        $"Selected area: {surface.SidebarItemTitle ?? "Overview"}",
                        $"Sidebar group: {surface.SidebarGroupTitle ?? "None"}",
                        $"Subpage summary: {surface.SidebarItemSummary ?? "No summary"}",
                        $"Identity surface: {launch?.SelectedIdentityLabel ?? "No identity summary"}"
                    ]),
                new LauncherFrontendPageSection(
                    "Artifacts",
                    "Launch-adjacent file work",
                    [
                        $"Natives directory: {FormatPath(launch?.NativesDirectory)}",
                        $"options.txt target: {FormatPath(launch?.OptionsTargetFilePath)}",
                        $"launcher_profiles write: {FormatBool(launch?.WritesLauncherProfiles)}",
                        $"Script export path: {FormatPath(launch?.ScriptExportPath)}"
                    ]),
                new LauncherFrontendPageSection(
                    "Migration",
                    "Next contract to harden",
                    [
                        "Per-page instance data still needs dedicated backend-facing presentation contracts.",
                        "The shell already proves subpage routing, prompts, and utility navigation around that data.",
                        "This is a safe place to add detail pages without borrowing WPF behavior."
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSavesContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var crash = request.Crash;
        var surface = request.Navigation.CurrentPage;

        return new LauncherFrontendPageContent(
            "Saves migration surface",
            "Save-management routes can share the same portable shell patterns as the rest of the frontend while waiting for dedicated world-management contracts.",
            [
                new LauncherFrontendPageFact("Subpage", surface.SidebarItemTitle ?? "Overview"),
                new LauncherFrontendPageFact("Sidebar group", surface.SidebarGroupTitle ?? "None"),
                new LauncherFrontendPageFact("Crash archive", crash?.SuggestedArchiveName ?? "Not provided"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Route",
                    "Current world-management surface",
                    [
                        $"Selected area: {surface.SidebarItemTitle ?? "Overview"}",
                        $"Summary: {surface.SidebarItemSummary ?? "No summary"}",
                        $"Back target: {request.Navigation.BackTarget?.Label ?? "None"}",
                        "Breadcrumbs and route hierarchy already work without WPF page state."
                    ]),
                new LauncherFrontendPageSection(
                    "Recovery",
                    "Adjacent export and diagnostics seams",
                    [
                        $"Suggested crash archive: {crash?.SuggestedArchiveName ?? "No archive suggestion"}",
                        $"Crash source files: {crash?.SourceFileCount.ToString() ?? "n/a"}",
                        $"Launcher log path: {FormatPath(crash?.LauncherLogPath)}",
                        "The save surface can reuse the same explicit recovery intents as other utility pages."
                    ]),
                new LauncherFrontendPageSection(
                    "Gap",
                    "Data still needed for full migration",
                    [
                        "World metadata, backup history, and datapack state need dedicated page contracts.",
                        "Those contracts should stay backend-driven rather than reconstructed from old page code.",
                        "The desktop shell is ready to consume them once they exist."
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildGenericContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        var surface = request.Navigation.CurrentPage;

        return new LauncherFrontendPageContent(
            "Portable page surface",
            "This page is already participating in the portable shell, but it still needs a more specific backend-facing presentation contract.",
            [
                new LauncherFrontendPageFact("Surface", surface.SidebarItemTitle ?? surface.Title),
                new LauncherFrontendPageFact("Page kind", surface.Kind.ToString()),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Route",
                    "Shell composition",
                    [
                        $"Current page: {surface.Title}",
                        $"Sidebar item: {surface.SidebarItemTitle ?? "Default"}",
                        $"Back target: {request.Navigation.BackTarget?.Label ?? "None"}",
                        $"Breadcrumb count: {request.Navigation.Breadcrumbs.Count}"
                    ]),
                new LauncherFrontendPageSection(
                    "Migration",
                    "Why this page is still safe to build now",
                    [
                        "The shell already owns page composition, routing, and prompt rendering.",
                        "Portable backend services already own startup and launcher workflow policy.",
                        "The remaining work is mostly page-specific presentation contracts."
                    ])
            ]);
    }

    private static string FormatBool(bool? value)
    {
        return value switch
        {
            true => "Yes",
            false => "No",
            null => "n/a"
        };
    }

    private static string? FormatPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);
    }
}

public sealed record LauncherFrontendPageContentRequest(
    LauncherFrontendNavigationView Navigation,
    LauncherStartupWorkflowPlan StartupPlan,
    LauncherStartupConsentResult Consent,
    IReadOnlyList<LauncherFrontendPromptLaneSummary> PromptLanes,
    LauncherFrontendLaunchSurfaceData? Launch = null,
    LauncherFrontendCrashSurfaceData? Crash = null);

public sealed record LauncherFrontendPromptLaneSummary(
    string Id,
    string Title,
    string Summary,
    int Count,
    bool IsSelected);

public sealed record LauncherFrontendLaunchSurfaceData(
    string ScenarioLabel,
    string LoginProviderLabel,
    string SelectedIdentityLabel,
    int LoginStepCount,
    string JavaRuntimeLabel,
    string? JavaDownloadTarget,
    string ResolutionLabel,
    int ClasspathEntryCount,
    int ReplacementValueCount,
    string NativesDirectory,
    string OptionsTargetFilePath,
    bool WritesLauncherProfiles,
    bool HasScriptExport,
    string? ScriptExportPath,
    string CompletionMessage);

public sealed record LauncherFrontendCrashSurfaceData(
    string SuggestedArchiveName,
    int SourceFileCount,
    bool IncludesLauncherLog,
    string? LauncherLogPath);

public sealed record LauncherFrontendPageContent(
    string Eyebrow,
    string Summary,
    IReadOnlyList<LauncherFrontendPageFact> Facts,
    IReadOnlyList<LauncherFrontendPageSection> Sections);

public sealed record LauncherFrontendPageFact(
    string Label,
    string Value);

public sealed record LauncherFrontendPageSection(
    string Eyebrow,
    string Title,
    IReadOnlyList<string> Lines);
