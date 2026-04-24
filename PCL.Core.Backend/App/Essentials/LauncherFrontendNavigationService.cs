using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.App.Essentials;

public static class LauncherFrontendNavigationService
{
    private static readonly LauncherFrontendNavigationCatalog Catalog = CreateCatalog();

    public static LauncherFrontendNavigationCatalog GetCatalog()
    {
        return Catalog;
    }

    public static LauncherFrontendNavigationView BuildView(LauncherFrontendNavigationViewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentPage = ResolvePage(request.CurrentRoute.Page);
        var sidebarGroupPage = currentPage.SidebarGroupPage ?? request.CurrentRoute.Page;
        var sidebarGroup = Catalog.SidebarGroups.FirstOrDefault(group => group.Page == sidebarGroupPage);
        var showsBackButton = currentPage.Kind != LauncherFrontendPageKind.TopLevel;
        var topLevelEntries = Catalog.TopLevelPages.Select(page => new LauncherFrontendNavigationEntry(
            page.Page.ToString(),
            new LauncherFrontendRoute(page.Page),
            IsSelected: request.CurrentRoute.Page == page.Page)).ToArray();
        var sidebarEntries = sidebarGroup?.Items.Select(item => new LauncherFrontendNavigationEntry(
            item.Subpage.ToString(),
            new LauncherFrontendRoute(sidebarGroup.Page, item.Subpage),
            IsSelected: request.CurrentRoute.Page == sidebarGroup.Page && request.CurrentRoute.Subpage == item.Subpage)).ToArray() ??
            Array.Empty<LauncherFrontendNavigationEntry>();
        var selectedSidebarEntry = sidebarEntries.FirstOrDefault(entry => entry.IsSelected);
        var backTarget = ResolveBackTarget(
            request,
            currentPage,
            showsBackButton);

        return new LauncherFrontendNavigationView(
            request.CurrentRoute,
            new LauncherFrontendPageSurface(
                request.CurrentRoute,
                currentPage.Kind,
                sidebarEntries.Length > 0),
            BuildBreadcrumbs(currentPage, request.CurrentRoute, selectedSidebarEntry),
            backTarget,
            showsBackButton,
            topLevelEntries,
            sidebarEntries,
            [
                new LauncherFrontendUtilityEntry(
                    "back",
                    backTarget?.Route ?? new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                    IsVisible: showsBackButton,
                    IsSelected: false),
                new LauncherFrontendUtilityEntry(
                    "task-manager",
                    new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
                    IsVisible: request.HasRunningTasks || request.CurrentRoute.Page == LauncherFrontendPageKey.TaskManager,
                    IsSelected: request.CurrentRoute.Page == LauncherFrontendPageKey.TaskManager),
                new LauncherFrontendUtilityEntry(
                    "game-log",
                    new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog),
                    IsVisible: request.HasGameLogs || request.CurrentRoute.Page == LauncherFrontendPageKey.GameLog,
                    IsSelected: request.CurrentRoute.Page == LauncherFrontendPageKey.GameLog)
            ]);
    }

    private static LauncherFrontendNavigationCatalog CreateCatalog()
    {
        return new LauncherFrontendNavigationCatalog(
            [
                CreateTopLevelPage(LauncherFrontendPageKey.Launch),
                CreateTopLevelPage(LauncherFrontendPageKey.Download),
                CreateTopLevelPage(LauncherFrontendPageKey.Setup),
                CreateTopLevelPage(LauncherFrontendPageKey.Tools)
            ],
            [
                CreateSidebarGroup(
                    LauncherFrontendPageKey.Download,
                    LauncherFrontendSubpageKey.DownloadInstall,
                    LauncherFrontendSubpageKey.DownloadMod,
                    LauncherFrontendSubpageKey.DownloadPack,
                    LauncherFrontendSubpageKey.DownloadDataPack,
                    LauncherFrontendSubpageKey.DownloadResourcePack,
                    LauncherFrontendSubpageKey.DownloadShader,
                    LauncherFrontendSubpageKey.DownloadWorld,
                    LauncherFrontendSubpageKey.DownloadCompFavorites,
                    LauncherFrontendSubpageKey.DownloadClient,
                    LauncherFrontendSubpageKey.DownloadOptiFine,
                    LauncherFrontendSubpageKey.DownloadForge,
                    LauncherFrontendSubpageKey.DownloadNeoForge,
                    LauncherFrontendSubpageKey.DownloadCleanroom,
                    LauncherFrontendSubpageKey.DownloadFabric,
                    LauncherFrontendSubpageKey.DownloadLegacyFabric,
                    LauncherFrontendSubpageKey.DownloadQuilt,
                    LauncherFrontendSubpageKey.DownloadLiteLoader,
                    LauncherFrontendSubpageKey.DownloadLabyMod),
                CreateSidebarGroup(
                    LauncherFrontendPageKey.Setup,
                    LauncherFrontendSubpageKey.SetupLaunch,
                    LauncherFrontendSubpageKey.SetupUI,
                    LauncherFrontendSubpageKey.SetupGameManage,
                    LauncherFrontendSubpageKey.SetupAbout,
                    LauncherFrontendSubpageKey.SetupLog,
                    LauncherFrontendSubpageKey.SetupFeedback,
                    LauncherFrontendSubpageKey.SetupUpdate,
                    LauncherFrontendSubpageKey.SetupJava,
                    LauncherFrontendSubpageKey.SetupLauncherMisc),
                CreateSidebarGroup(
                    LauncherFrontendPageKey.Tools,
                    LauncherFrontendSubpageKey.ToolsTest,
                    LauncherFrontendSubpageKey.ToolsLauncherHelp),
                CreateSidebarGroup(
                    LauncherFrontendPageKey.InstanceSetup,
                    LauncherFrontendSubpageKey.VersionOverall,
                    LauncherFrontendSubpageKey.VersionSetup,
                    LauncherFrontendSubpageKey.VersionInstall,
                    LauncherFrontendSubpageKey.VersionExport,
                    LauncherFrontendSubpageKey.VersionWorld,
                    LauncherFrontendSubpageKey.VersionScreenshot,
                    LauncherFrontendSubpageKey.VersionMod,
                    LauncherFrontendSubpageKey.VersionResourcePack,
                    LauncherFrontendSubpageKey.VersionShader,
                    LauncherFrontendSubpageKey.VersionSchematic,
                    LauncherFrontendSubpageKey.VersionServer),
                CreateSidebarGroup(
                    LauncherFrontendPageKey.VersionSaves,
                    LauncherFrontendSubpageKey.VersionSavesInfo,
                    LauncherFrontendSubpageKey.VersionSavesBackup,
                    LauncherFrontendSubpageKey.VersionSavesDatapack)
            ],
            [
                CreateSecondaryPage(LauncherFrontendPageKey.InstanceSelect, LauncherFrontendPageKind.Secondary),
                CreateSecondaryPage(LauncherFrontendPageKey.TaskManager, LauncherFrontendPageKind.Utility),
                CreateSecondaryPage(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendPageKind.Secondary, LauncherFrontendPageKey.InstanceSetup),
                CreateSecondaryPage(LauncherFrontendPageKey.CompDetail, LauncherFrontendPageKind.Detail, LauncherFrontendPageKey.Download),
                CreateSecondaryPage(LauncherFrontendPageKey.HelpDetail, LauncherFrontendPageKind.Detail, LauncherFrontendPageKey.Tools),
                CreateSecondaryPage(LauncherFrontendPageKey.GameLog, LauncherFrontendPageKind.Utility),
                CreateSecondaryPage(LauncherFrontendPageKey.VersionSaves, LauncherFrontendPageKind.Secondary, LauncherFrontendPageKey.VersionSaves)
            ]
        );
    }

    private static LauncherFrontendTopLevelPage CreateTopLevelPage(LauncherFrontendPageKey page)
    {
        return new LauncherFrontendTopLevelPage(page);
    }

    private static LauncherFrontendSidebarGroup CreateSidebarGroup(
        LauncherFrontendPageKey page,
        params LauncherFrontendSubpageKey[] subpages)
    {
        return new LauncherFrontendSidebarGroup(
            page,
            subpages.Select(CreateSidebarItem).ToArray());
    }

    private static LauncherFrontendSidebarItem CreateSidebarItem(LauncherFrontendSubpageKey subpage)
    {
        return new LauncherFrontendSidebarItem(subpage);
    }

    private static LauncherFrontendSecondaryPage CreateSecondaryPage(
        LauncherFrontendPageKey page,
        LauncherFrontendPageKind kind,
        LauncherFrontendPageKey? sidebarGroupPage = null)
    {
        return new LauncherFrontendSecondaryPage(page, kind, sidebarGroupPage);
    }

    private static LauncherFrontendPageResolution ResolvePage(LauncherFrontendPageKey page)
    {
        var topLevel = Catalog.TopLevelPages.FirstOrDefault(item => item.Page == page);
        if (topLevel is not null)
        {
            return new LauncherFrontendPageResolution(
                topLevel.Page,
                LauncherFrontendPageKind.TopLevel,
                SidebarGroupPage: topLevel.Page);
        }

        var secondary = Catalog.SecondaryPages.FirstOrDefault(item => item.Page == page);
        if (secondary is not null)
        {
            return new LauncherFrontendPageResolution(
                secondary.Page,
                secondary.Kind,
                secondary.SidebarGroupPage);
        }

        throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown frontend page.");
    }

    private sealed record LauncherFrontendPageResolution(
        LauncherFrontendPageKey Page,
        LauncherFrontendPageKind Kind,
        LauncherFrontendPageKey? SidebarGroupPage = null);

    private static IReadOnlyList<LauncherFrontendBreadcrumb> BuildBreadcrumbs(
        LauncherFrontendPageResolution currentPage,
        LauncherFrontendRoute currentRoute,
        LauncherFrontendNavigationEntry? selectedSidebarEntry)
    {
        var breadcrumbs = new List<LauncherFrontendBreadcrumb>();
        if (currentPage.Kind == LauncherFrontendPageKind.TopLevel)
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                currentPage.Page.ToString(),
                new LauncherFrontendRoute(currentPage.Page)));
        }
        else
        {
            var fallbackRoute = currentPage.SidebarGroupPage is null
                ? null
                : new LauncherFrontendRoute(currentPage.SidebarGroupPage.Value);
            var breadcrumbTitle = currentRoute.Subpage != LauncherFrontendSubpageKey.Default
                ? currentRoute.Subpage.ToString()
                : currentPage.Page.ToString();
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(breadcrumbTitle, fallbackRoute));
        }

        if (selectedSidebarEntry is not null)
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                selectedSidebarEntry.Route.Subpage.ToString(),
                selectedSidebarEntry.Route));
        }

        return breadcrumbs;
    }

    private static LauncherFrontendBackTarget? ResolveBackTarget(
        LauncherFrontendNavigationViewRequest request,
        LauncherFrontendPageResolution currentPage,
        bool showsBackButton)
    {
        if (!showsBackButton)
        {
            return null;
        }

        var route = request.ParentRoute ?? ResolveDefaultBackRoute(request.CurrentRoute, currentPage);
        if (route is null)
        {
            return null;
        }

        return new LauncherFrontendBackTarget(
            LauncherFrontendBackTargetKind.Route,
            route);
    }

    private static LauncherFrontendRoute? ResolveDefaultBackRoute(
        LauncherFrontendRoute currentRoute,
        LauncherFrontendPageResolution currentPage)
    {
        return currentRoute.Page switch
        {
            LauncherFrontendPageKey.InstanceSelect => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LauncherFrontendPageKey.TaskManager => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LauncherFrontendPageKey.InstanceSetup => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LauncherFrontendPageKey.CompDetail => new LauncherFrontendRoute(
                LauncherFrontendPageKey.Download,
                LauncherFrontendSubpageKey.DownloadInstall),
            LauncherFrontendPageKey.HelpDetail => new LauncherFrontendRoute(
                LauncherFrontendPageKey.Tools,
                LauncherFrontendSubpageKey.ToolsLauncherHelp),
            LauncherFrontendPageKey.GameLog => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LauncherFrontendPageKey.VersionSaves => new LauncherFrontendRoute(
                LauncherFrontendPageKey.InstanceSetup,
                LauncherFrontendSubpageKey.VersionWorld),
            _ when currentPage.SidebarGroupPage is not null => new LauncherFrontendRoute(currentPage.SidebarGroupPage.Value),
            _ => new LauncherFrontendRoute(LauncherFrontendPageKey.Launch)
        };
    }

}

public static class LauncherFrontendPlanService
{
    public static LauncherFrontendPlan BuildPlan(LauncherFrontendPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startupPlan = LauncherStartupWorkflowService.BuildPlan(request.StartupWorkflowRequest);
        var consent = LauncherStartupConsentService.Evaluate(request.StartupConsentRequest);

        return new LauncherFrontendPlan(
            startupPlan,
            consent,
            LauncherFrontendPromptService.BuildStartupPromptQueue(startupPlan, consent),
            LauncherFrontendNavigationService.GetCatalog(),
            LauncherFrontendNavigationService.BuildView(request.Navigation));
    }
}

public sealed record LauncherFrontendPlanRequest(
    LauncherStartupWorkflowRequest StartupWorkflowRequest,
    LauncherStartupConsentRequest StartupConsentRequest,
    LauncherFrontendNavigationViewRequest Navigation);

public sealed record LauncherFrontendPlan(
    LauncherStartupWorkflowPlan StartupPlan,
    LauncherStartupConsentResult Consent,
    IReadOnlyList<LauncherFrontendPrompt> Prompts,
    LauncherFrontendNavigationCatalog Catalog,
    LauncherFrontendNavigationView Navigation);

public sealed record LauncherFrontendNavigationCatalog(
    IReadOnlyList<LauncherFrontendTopLevelPage> TopLevelPages,
    IReadOnlyList<LauncherFrontendSidebarGroup> SidebarGroups,
    IReadOnlyList<LauncherFrontendSecondaryPage> SecondaryPages);

public sealed record LauncherFrontendTopLevelPage(
    LauncherFrontendPageKey Page);

public sealed record LauncherFrontendSidebarGroup(
    LauncherFrontendPageKey Page,
    IReadOnlyList<LauncherFrontendSidebarItem> Items);

public sealed record LauncherFrontendSidebarItem(
    LauncherFrontendSubpageKey Subpage);

public sealed record LauncherFrontendSecondaryPage(
    LauncherFrontendPageKey Page,
    LauncherFrontendPageKind Kind,
    LauncherFrontendPageKey? SidebarGroupPage = null);

public sealed record LauncherFrontendNavigationViewRequest(
    LauncherFrontendRoute CurrentRoute,
    int BackstackDepth = 0,
    bool HasRunningTasks = false,
    bool HasGameLogs = false,
    LauncherFrontendRoute? ParentRoute = null);

public sealed record LauncherFrontendNavigationView(
    LauncherFrontendRoute CurrentRoute,
    LauncherFrontendPageSurface CurrentPage,
    IReadOnlyList<LauncherFrontendBreadcrumb> Breadcrumbs,
    LauncherFrontendBackTarget? BackTarget,
    bool ShowsBackButton,
    IReadOnlyList<LauncherFrontendNavigationEntry> TopLevelEntries,
    IReadOnlyList<LauncherFrontendNavigationEntry> SidebarEntries,
    IReadOnlyList<LauncherFrontendUtilityEntry> UtilityEntries);

public sealed record LauncherFrontendPageSurface(
    LauncherFrontendRoute Route,
    LauncherFrontendPageKind Kind,
    bool HasSidebar);

public sealed record LauncherFrontendBreadcrumb(
    string Title,
    LauncherFrontendRoute? Route);

public sealed record LauncherFrontendBackTarget(
    LauncherFrontendBackTargetKind Kind,
    LauncherFrontendRoute? Route);

public sealed record LauncherFrontendNavigationEntry(
    string Id,
    LauncherFrontendRoute Route,
    bool IsSelected);

public sealed record LauncherFrontendUtilityEntry(
    string Id,
    LauncherFrontendRoute Route,
    bool IsVisible,
    bool IsSelected);

public sealed record LauncherFrontendRoute(
    LauncherFrontendPageKey Page,
    LauncherFrontendSubpageKey Subpage = LauncherFrontendSubpageKey.Default);

public enum LauncherFrontendPageKind
{
    TopLevel = 0,
    Secondary = 1,
    Detail = 2,
    Utility = 3
}

public enum LauncherFrontendBackTargetKind
{
    History = 0,
    Route = 1
}

public enum LauncherFrontendPageKey
{
    Launch = 0,
    Download = 1,
    Setup = 2,
    Tools = 3,
    InstanceSelect = 5,
    TaskManager = 6,
    InstanceSetup = 7,
    CompDetail = 8,
    HelpDetail = 9,
    GameLog = 10,
    VersionSaves = 12
}

public enum LauncherFrontendSubpageKey
{
    Default = 0,
    DownloadInstall = 1,
    DownloadMod = 2,
    DownloadPack = 3,
    DownloadDataPack = 4,
    DownloadResourcePack = 5,
    DownloadShader = 6,
    DownloadWorld = 7,
    DownloadCompFavorites = 8,
    DownloadClient = 9,
    DownloadOptiFine = 10,
    DownloadForge = 11,
    DownloadNeoForge = 12,
    DownloadCleanroom = 13,
    DownloadFabric = 14,
    DownloadQuilt = 15,
    DownloadLiteLoader = 16,
    DownloadLabyMod = 17,
    DownloadLegacyFabric = 18,
    SetupLaunch = 101,
    SetupUI = 102,
    SetupGameManage = 103,
    SetupLink = 104,
    SetupAbout = 105,
    SetupLog = 106,
    SetupFeedback = 107,
    SetupUpdate = 108,
    SetupJava = 109,
    SetupLauncherMisc = 110,
    ToolsLauncherHelp = 201,
    ToolsTest = 202,
    VersionOverall = 301,
    VersionSetup = 302,
    VersionExport = 303,
    VersionWorld = 304,
    VersionScreenshot = 305,
    VersionMod = 306,
    VersionModDisabled = 307,
    VersionResourcePack = 308,
    VersionShader = 309,
    VersionSchematic = 310,
    VersionInstall = 311,
    VersionServer = 312,
    VersionSavesInfo = 401,
    VersionSavesBackup = 402,
    VersionSavesDatapack = 403
}
