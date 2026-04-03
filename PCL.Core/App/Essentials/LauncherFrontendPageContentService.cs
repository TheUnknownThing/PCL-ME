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
        return request.Navigation.CurrentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.SetupAbout => BuildSetupAboutContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupFeedback => BuildSetupFeedbackContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupGameLink => BuildSetupGameLinkContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupLog => BuildSetupLogContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupUpdate => BuildSetupUpdateContent(request, promptTotal),
            _ => BuildGenericSetupContent(request, promptTotal)
        };
    }

    private static LauncherFrontendPageContent BuildGenericSetupContent(
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

    private static LauncherFrontendPageContent BuildSetupAboutContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var startup = request.StartupPlan;

        return new LauncherFrontendPageContent(
            "关于页面",
            "版本信息、社区团队与特别鸣谢已经可以通过可移植路由与页面内容合同进入新前端。",
            [
                new LauncherFrontendPageFact("当前分区", "关于"),
                new LauncherFrontendPageFact("Update channel", startup.Bootstrap.DefaultUpdateChannel.ToString()),
                new LauncherFrontendPageFact("Config keys", startup.Bootstrap.ConfigKeysToLoad.Count.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "关于",
                    "项目与团队",
                    [
                        "保留原版的头像、介绍文案与右侧按钮布局。",
                        "关于页适合优先复制，因为它主要是只读展示内容。",
                        "这能验证 MyCard / MyListItem 风格在 Avalonia 中的复用方式。"
                    ]),
                new LauncherFrontendPageSection(
                    "鸣谢",
                    "特别鸣谢",
                    [
                        "保留原版的名单顺序、图片位置和操作按钮文案。",
                        "优先复用已有头像资源而不是替换成新的通用占位图。",
                        "这一页不需要把任何项目元数据逻辑重新塞回前端。"
                    ]),
                new LauncherFrontendPageSection(
                    "迁移",
                    "为什么先复制这里",
                    [
                        "只读页面可以先验证视觉复制，再逐步替换为更真实的数据来源。",
                        "前端仍只负责展示与外部意图触发，不拥有项目策略。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupFeedbackContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return new LauncherFrontendPageContent(
            "反馈页面",
            "反馈入口、状态分栏和折叠卡片结构可以先在新前端中复制出来，再逐步接真实来源。",
            [
                new LauncherFrontendPageFact("当前分区", "反馈"),
                new LauncherFrontendPageFact("Consent prompts", request.Consent.Prompts.Count.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString()),
                new LauncherFrontendPageFact("Page kind", request.Navigation.CurrentPage.Kind.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "提交",
                    "反馈入口",
                    [
                        "顶部保留原版说明文案和单个高亮按钮入口。",
                        "是否跳转到 GitHub 仍应表现为显式前端意图。",
                        "反馈页本身不应拥有网络或工单同步策略。"
                    ]),
                new LauncherFrontendPageSection(
                    "状态",
                    "分栏与折叠卡片",
                    [
                        "保留“正在处理 / 等待处理 / 已完成”等状态卡片结构。",
                        "默认折叠行为也可以在前端层模拟，而不必借用旧窗口事件流。",
                        "这能帮助新前端验证更多 MyCard 风格页面。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "迁移规则",
                    [
                        "复制原版布局优先于设计一个更通用但更抽象的新反馈页。",
                        "如果未来需要真实反馈数据，应通过新合同输入而不是页面内抓取。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupUpdateContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var startup = request.StartupPlan;

        return new LauncherFrontendPageContent(
            "更新页面",
            "更新通道、自动检查策略和版本卡片已经可以用更接近原版 PageSetupUpdate 的布局进入新前端。",
            [
                new LauncherFrontendPageFact("当前分区", "更新"),
                new LauncherFrontendPageFact("Update channel", startup.Bootstrap.DefaultUpdateChannel.ToString()),
                new LauncherFrontendPageFact("Config keys", startup.Bootstrap.ConfigKeysToLoad.Count.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "设置",
                    "更新通道与策略",
                    [
                        "保留了原版页面顶部的三行设置布局：更新通道、自动更新设置和 Mirror 酱 CDK。",
                        "优先继续沿用原来的控件密度，而不是把它改造成更通用的设置表单。",
                        "刷新动作仍然只是前端意图，真正的更新策略不应重新塞回壳层。"
                    ]),
                new LauncherFrontendPageSection(
                    "状态",
                    "版本卡片",
                    [
                        "可用更新与当前已是最新版本这两种状态继续按原页面拆成不同卡片。",
                        "更新摘要保留原版的大标题、说明文字和底部详情入口。",
                        "完整的版本数据以后应来自更具体的更新合同，而不是由页面自己推断。"
                    ]),
                new LauncherFrontendPageSection(
                    "迁移",
                    "为什么先复制这里",
                    [
                        "这是一个典型的设置页右侧面板，适合继续验证 MyCard / MyButton 风格的复制方式。",
                        "它还顺带覆盖了组合框、文本框、状态卡和次级文本按钮这些常见控件。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupGameLinkContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return new LauncherFrontendPageContent(
            "游戏联机页面",
            "EasyTier 设置面板已经可以按原版表单结构进入新前端，而不是停留在通用摘要卡片上。",
            [
                new LauncherFrontendPageFact("当前分区", "游戏联机"),
                new LauncherFrontendPageFact("协议偏好", "TCP / UDP"),
                new LauncherFrontendPageFact("网络提示", "需要重启大厅后生效"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "EasyTier",
                    "联机设置表单",
                    [
                        "保留了顶部黄色提示条、用户名输入框、协议偏好下拉框和四个复选项。",
                        "这一页继续沿用原版的紧凑表单密度，而不是变成另一种新的设置布局。",
                        "左侧附加按钮会在 Spike 中把这些字段重置到默认演示值。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "前端该做什么",
                    [
                        "联机策略与真实网络行为仍然应该由后端或外部组件负责。",
                        "前端只需要渲染这些字段、收集输入并触发意图。",
                        "隐藏的网络测试区块可以等真实测试合同更明确后再继续复制。"
                    ]),
                new LauncherFrontendPageSection(
                    "迁移",
                    "为什么现在做这页",
                    [
                        "它是一个比界面页更紧凑的设置表单，适合继续打磨组合框、文本框和复选框的复制方式。",
                        "这也让设置分组不再只剩更新页一个已复制的右侧面板。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupLogContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var crash = request.Crash;

        return new LauncherFrontendPageContent(
            "日志页面",
            "日志操作区和日志列表区可以直接迁移为前端工具面，而日志收集与导出规划继续停留在后端。",
            [
                new LauncherFrontendPageFact("当前分区", "日志"),
                new LauncherFrontendPageFact("Crash archive", crash?.SuggestedArchiveName ?? "Not provided"),
                new LauncherFrontendPageFact("Launcher log", FormatBool(crash?.IncludesLauncherLog)),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "操作",
                    "日志操作",
                    [
                        "保留导出日志、导出全部日志、打开目录与清理历史日志的按钮分组。",
                        "这些按钮应触发明确的壳层意图，而不是隐式调用旧页面代码。",
                        "日志页是很适合迁移的低风险工具页。"
                    ]),
                new LauncherFrontendPageSection(
                    "数据",
                    "日志与导出",
                    [
                        $"Suggested archive: {crash?.SuggestedArchiveName ?? "No archive suggestion"}",
                        $"Launcher log path: {FormatPath(crash?.LauncherLogPath)}",
                        "列表内容将来可以接到真实日志目录或运行态源。",
                        "目前前端只需要准备承载原始布局的容器。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "职责分离",
                    [
                        "前端拥有按钮布局、列表渲染和目录打开意图。",
                        "后端拥有日志收集、归档规划和崩溃恢复信息。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildToolsContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return request.Navigation.CurrentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.ToolsLauncherHelp => BuildToolsHelpContent(request, promptTotal, visibleUtilityCount),
            _ => BuildGenericToolsContent(request, promptTotal, visibleUtilityCount)
        };
    }

    private static LauncherFrontendPageContent BuildGenericToolsContent(
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

    private static LauncherFrontendPageContent BuildToolsHelpContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return new LauncherFrontendPageContent(
            "帮助页面",
            "帮助页已经适合直接复制搜索框、结果卡片与帮助列表结构，后续再接更细的帮助条目合同。",
            [
                new LauncherFrontendPageFact("当前分区", "帮助"),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Back target", request.Navigation.BackTarget?.Label ?? "None"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "搜索",
                    "搜索帮助",
                    [
                        "顶部保留原版的单行搜索入口。",
                        "搜索结果区和默认列表区可以共存，由前端负责切换。",
                        "搜索交互不需要旧窗口生命周期参与。"
                    ]),
                new LauncherFrontendPageSection(
                    "列表",
                    "帮助卡片",
                    [
                        "帮助条目继续使用卡片和列表项结构，而不是退回通用摘要面板。",
                        "详情页路由应保持显式，这样后续可以平滑接入 HelpDetail。",
                        "这也是复制 MySearchBox / MyCard 风格的低风险入口。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "后续所需合同",
                    [
                        "真实帮助条目、分类与详情正文仍需要更细的后端输入合同。",
                        "在合同到位前，前端可以先验证页面结构与搜索交互。",
                        $"当前可见提示数：{promptTotal}"
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
