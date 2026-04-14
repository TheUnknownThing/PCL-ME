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
        var currentTitle = string.IsNullOrWhiteSpace(request.CurrentPageTitleOverride)
            ? currentPage.Title
            : request.CurrentPageTitleOverride!;
        var sidebarGroupPage = currentPage.SidebarGroupPage ?? request.CurrentRoute.Page;
        var sidebarGroup = Catalog.SidebarGroups.FirstOrDefault(group => group.Page == sidebarGroupPage);
        var showsBackButton = currentPage.Kind != LauncherFrontendPageKind.TopLevel;
        var topLevelEntries = Catalog.TopLevelPages.Select(page => new LauncherFrontendNavigationEntry(
            page.Page.ToString(),
            page.Title,
            page.Summary,
            new LauncherFrontendRoute(page.Page),
            IsSelected: request.CurrentRoute.Page == page.Page)).ToArray();
        var sidebarEntries = sidebarGroup?.Items.Select(item => new LauncherFrontendNavigationEntry(
            item.Subpage.ToString(),
            item.Title,
            item.Summary,
            new LauncherFrontendRoute(sidebarGroup.Page, item.Subpage),
            IsSelected: request.CurrentRoute.Page == sidebarGroup.Page && request.CurrentRoute.Subpage == item.Subpage)).ToArray() ??
            Array.Empty<LauncherFrontendNavigationEntry>();
        var selectedSidebarEntry = sidebarEntries.FirstOrDefault(entry => entry.IsSelected);
        var backTarget = ResolveBackTarget(
            request,
            currentPage,
            showsBackButton,
            topLevelEntries);

        return new LauncherFrontendNavigationView(
            request.CurrentRoute,
            currentTitle,
            new LauncherFrontendPageSurface(
                request.CurrentRoute,
                currentPage.Kind,
                currentTitle,
                currentPage.Summary,
                sidebarGroup?.Title,
                selectedSidebarEntry?.Title,
                selectedSidebarEntry?.Summary,
                sidebarEntries.Length > 0),
            BuildBreadcrumbs(currentPage, currentTitle, selectedSidebarEntry, topLevelEntries),
            backTarget,
            showsBackButton,
            topLevelEntries,
            sidebarEntries,
            [
                new LauncherFrontendUtilityEntry(
                    "back",
                    "返回",
                    backTarget?.Route ?? new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                    IsVisible: showsBackButton,
                    IsSelected: false),
                new LauncherFrontendUtilityEntry(
                    "task-manager",
                    "任务管理",
                    new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
                    IsVisible: request.HasRunningTasks || request.CurrentRoute.Page == LauncherFrontendPageKey.TaskManager,
                    IsSelected: request.CurrentRoute.Page == LauncherFrontendPageKey.TaskManager),
                new LauncherFrontendUtilityEntry(
                    "game-log",
                    "实时日志",
                    new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog),
                    IsVisible: request.HasGameLogs || request.CurrentRoute.Page == LauncherFrontendPageKey.GameLog,
                    IsSelected: request.CurrentRoute.Page == LauncherFrontendPageKey.GameLog)
            ]);
    }

    public static string? GetSubpageTitle(LauncherFrontendRoute route)
    {
        var sidebarGroup = Catalog.SidebarGroups.FirstOrDefault(group => group.Page == route.Page);
        return sidebarGroup?.Items.FirstOrDefault(item => item.Subpage == route.Subpage)?.Title;
    }

    private static LauncherFrontendNavigationCatalog CreateCatalog()
    {
        return new LauncherFrontendNavigationCatalog(
            [
                new LauncherFrontendTopLevelPage(
                    LauncherFrontendPageKey.Launch,
                    "启动",
                    "实例选择、登录与启动主流程。"),
                new LauncherFrontendTopLevelPage(
                    LauncherFrontendPageKey.Download,
                    "下载",
                    "安装 Minecraft、模组与资源。"),
                new LauncherFrontendTopLevelPage(
                    LauncherFrontendPageKey.Setup,
                    "设置",
                    "启动器、Java 与游戏设置。"),
                new LauncherFrontendTopLevelPage(
                    LauncherFrontendPageKey.Tools,
                    "工具",
                    "帮助与实验功能。")
            ],
            [
                new LauncherFrontendSidebarGroup(
                    LauncherFrontendPageKey.Download,
                    "下载分区",
                    [
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadInstall, "自动安装", "安装 Minecraft 与整合包。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadMod, "Mod", "浏览并安装 Mod。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadPack, "整合包", "浏览整合包资源。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadDataPack, "数据包", "安装数据包。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadResourcePack, "资源包", "安装资源包。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadShader, "光影包", "安装光影资源。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadWorld, "存档", "导入地图与存档。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadCompFavorites, "收藏夹", "查看收藏的资源条目。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadClient, "Minecraft", "选择并安装 Minecraft 版本。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadOptiFine, "OptiFine", "安装 OptiFine。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadForge, "Forge", "安装 Forge。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadNeoForge, "NeoForge", "安装 NeoForge。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadCleanroom, "Cleanroom", "安装 Cleanroom。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadFabric, "Fabric", "安装 Fabric。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadLegacyFabric, "Legacy Fabric", "安装 Legacy Fabric。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadQuilt, "Quilt", "安装 Quilt。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadLiteLoader, "LiteLoader", "安装 LiteLoader。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.DownloadLabyMod, "LabyMod", "安装 LabyMod。")
                    ]),
                new LauncherFrontendSidebarGroup(
                    LauncherFrontendPageKey.Setup,
                    "设置分区",
                    [
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupLaunch, "启动", "启动参数与窗口策略。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupUI, "界面", "主题、动画与外观。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupGameManage, "游戏管理", "实例与文件管理策略。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupAbout, "关于", "版本与项目信息。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupLog, "日志", "日志与诊断开关。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupFeedback, "反馈", "问题反馈入口。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupUpdate, "更新", "更新通道与检查策略。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupJava, "Java", "Java 运行时配置。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.SetupLauncherMisc, "启动器杂项", "零散启动器行为设置。")
                    ]),
                new LauncherFrontendSidebarGroup(
                    LauncherFrontendPageKey.Tools,
                    "工具分区",
                    [
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.ToolsTest, "测试", "实验或调试工具。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.ToolsLauncherHelp, "帮助", "查看帮助与说明。")
                    ]),
                new LauncherFrontendSidebarGroup(
                    LauncherFrontendPageKey.InstanceSetup,
                    "实例设置分区",
                    [
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionOverall, "概览", "实例总览。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionSetup, "设置", "实例专属设置。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionInstall, "修改", "修改 Minecraft 版本与组件。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionExport, "导出", "导出实例或整合包。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionWorld, "世界", "查看世界与存档。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionScreenshot, "截图", "浏览截图。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionMod, "Mod", "管理启用的 Mod。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionResourcePack, "资源包", "管理资源包。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionShader, "光影包", "管理光影资源。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionSchematic, "投影", "管理投影与建筑文件。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionServer, "服务器", "管理服务器入口。")
                    ]),
                new LauncherFrontendSidebarGroup(
                    LauncherFrontendPageKey.VersionSaves,
                    "存档管理分区",
                    [
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionSavesInfo, "概览", "查看当前存档信息。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionSavesBackup, "备份", "备份与恢复存档。"),
                        new LauncherFrontendSidebarItem(LauncherFrontendSubpageKey.VersionSavesDatapack, "数据包", "管理当前存档的数据包。")
                    ])
            ],
            [
                new LauncherFrontendSecondaryPage(LauncherFrontendPageKey.InstanceSelect, "实例选择", "在启动前选择实例。", LauncherFrontendPageKind.Secondary),
                new LauncherFrontendSecondaryPage(LauncherFrontendPageKey.TaskManager, "任务管理", "查看下载与后台任务。", LauncherFrontendPageKind.Utility),
                new LauncherFrontendSecondaryPage(LauncherFrontendPageKey.InstanceSetup, "实例设置", "查看实例详情与资源管理。", LauncherFrontendPageKind.Secondary, SidebarGroupPage: LauncherFrontendPageKey.InstanceSetup),
                new LauncherFrontendSecondaryPage(LauncherFrontendPageKey.CompDetail, "资源下载", "查看资源工程详情。", LauncherFrontendPageKind.Detail, SidebarGroupPage: LauncherFrontendPageKey.Download),
                new LauncherFrontendSecondaryPage(LauncherFrontendPageKey.HelpDetail, "帮助详情", "查看帮助条目详情。", LauncherFrontendPageKind.Detail, SidebarGroupPage: LauncherFrontendPageKey.Tools),
                new LauncherFrontendSecondaryPage(LauncherFrontendPageKey.GameLog, "实时日志", "查看启动日志与输出。", LauncherFrontendPageKind.Utility),
                new LauncherFrontendSecondaryPage(LauncherFrontendPageKey.VersionSaves, "存档管理", "查看单个存档的详细页面。", LauncherFrontendPageKind.Secondary, SidebarGroupPage: LauncherFrontendPageKey.VersionSaves)
            ]
        );
    }

    private static LauncherFrontendPageResolution ResolvePage(LauncherFrontendPageKey page)
    {
        var topLevel = Catalog.TopLevelPages.FirstOrDefault(item => item.Page == page);
        if (topLevel is not null)
        {
            return new LauncherFrontendPageResolution(
                topLevel.Page,
                topLevel.Title,
                topLevel.Summary,
                LauncherFrontendPageKind.TopLevel,
                SidebarGroupPage: topLevel.Page);
        }

        var secondary = Catalog.SecondaryPages.FirstOrDefault(item => item.Page == page);
        if (secondary is not null)
        {
            return new LauncherFrontendPageResolution(
                secondary.Page,
                secondary.Title,
                secondary.Summary,
                secondary.Kind,
                secondary.SidebarGroupPage);
        }

        throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown frontend page.");
    }

    private sealed record LauncherFrontendPageResolution(
        LauncherFrontendPageKey Page,
        string Title,
        string Summary,
        LauncherFrontendPageKind Kind,
        LauncherFrontendPageKey? SidebarGroupPage = null);

    private static IReadOnlyList<LauncherFrontendBreadcrumb> BuildBreadcrumbs(
        LauncherFrontendPageResolution currentPage,
        string currentTitle,
        LauncherFrontendNavigationEntry? selectedSidebarEntry,
        IReadOnlyList<LauncherFrontendNavigationEntry> topLevelEntries)
    {
        var breadcrumbs = new List<LauncherFrontendBreadcrumb>();
        if (currentPage.Kind == LauncherFrontendPageKind.TopLevel)
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                currentTitle,
                new LauncherFrontendRoute(currentPage.Page)));
        }
        else
        {
            var fallbackRoute = currentPage.SidebarGroupPage is null
                ? null
                : new LauncherFrontendRoute(currentPage.SidebarGroupPage.Value);
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(currentTitle, fallbackRoute));
        }

        if (selectedSidebarEntry is not null)
        {
            breadcrumbs.Add(new LauncherFrontendBreadcrumb(
                selectedSidebarEntry.Title,
                selectedSidebarEntry.Route));
        }

        return breadcrumbs;
    }

    private static LauncherFrontendBackTarget? ResolveBackTarget(
        LauncherFrontendNavigationViewRequest request,
        LauncherFrontendPageResolution currentPage,
        bool showsBackButton,
        IReadOnlyList<LauncherFrontendNavigationEntry> topLevelEntries)
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
            route,
            $"返回到 {GetRouteLabel(route, topLevelEntries)}");
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

    private static string GetRouteLabel(
        LauncherFrontendRoute route,
        IReadOnlyList<LauncherFrontendNavigationEntry> topLevelEntries)
    {
        var subpageTitle = GetSubpageTitle(route);
        if (!string.IsNullOrWhiteSpace(subpageTitle))
        {
            return subpageTitle!;
        }

        return topLevelEntries.FirstOrDefault(entry => entry.Route.Page == route.Page)?.Title
               ?? ResolvePage(route.Page).Title;
    }
}

public static class LauncherFrontendShellService
{
    public static LauncherFrontendShellPlan BuildPlan(LauncherFrontendShellRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startupPlan = LauncherStartupWorkflowService.BuildPlan(request.StartupWorkflowRequest);
        var consent = LauncherStartupConsentService.Evaluate(request.StartupConsentRequest);

        return new LauncherFrontendShellPlan(
            startupPlan,
            consent,
            LauncherFrontendPromptService.BuildStartupPromptQueue(startupPlan, consent),
            LauncherFrontendNavigationService.GetCatalog(),
            LauncherFrontendNavigationService.BuildView(request.Navigation));
    }
}

public sealed record LauncherFrontendShellRequest(
    LauncherStartupWorkflowRequest StartupWorkflowRequest,
    LauncherStartupConsentRequest StartupConsentRequest,
    LauncherFrontendNavigationViewRequest Navigation);

public sealed record LauncherFrontendShellPlan(
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
    LauncherFrontendPageKey Page,
    string Title,
    string Summary);

public sealed record LauncherFrontendSidebarGroup(
    LauncherFrontendPageKey Page,
    string Title,
    IReadOnlyList<LauncherFrontendSidebarItem> Items);

public sealed record LauncherFrontendSidebarItem(
    LauncherFrontendSubpageKey Subpage,
    string Title,
    string Summary);

public sealed record LauncherFrontendSecondaryPage(
    LauncherFrontendPageKey Page,
    string Title,
    string Summary,
    LauncherFrontendPageKind Kind,
    LauncherFrontendPageKey? SidebarGroupPage = null);

public sealed record LauncherFrontendNavigationViewRequest(
    LauncherFrontendRoute CurrentRoute,
    int BackstackDepth = 0,
    bool HasRunningTasks = false,
    bool HasGameLogs = false,
    string? CurrentPageTitleOverride = null,
    LauncherFrontendRoute? ParentRoute = null);

public sealed record LauncherFrontendNavigationView(
    LauncherFrontendRoute CurrentRoute,
    string CurrentPageTitle,
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
    string Title,
    string Summary,
    string? SidebarGroupTitle,
    string? SidebarItemTitle,
    string? SidebarItemSummary,
    bool HasSidebar);

public sealed record LauncherFrontendBreadcrumb(
    string Title,
    LauncherFrontendRoute? Route);

public sealed record LauncherFrontendBackTarget(
    LauncherFrontendBackTargetKind Kind,
    LauncherFrontendRoute? Route,
    string Label);

public sealed record LauncherFrontendNavigationEntry(
    string Id,
    string Title,
    string Summary,
    LauncherFrontendRoute Route,
    bool IsSelected);

public sealed record LauncherFrontendUtilityEntry(
    string Id,
    string Title,
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
