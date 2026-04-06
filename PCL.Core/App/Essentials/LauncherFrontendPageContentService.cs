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
            LauncherFrontendPageKey.Launch =>
                BuildLaunchContent(request, promptTotal, selectedLane?.Title),
            LauncherFrontendPageKey.InstanceSelect =>
                BuildInstanceSelectContent(request, promptTotal, selectedLane?.Title),
            LauncherFrontendPageKey.Download =>
                BuildDownloadContent(request, promptTotal, visibleUtilityCount, selectedLane?.Title),
            LauncherFrontendPageKey.CompDetail =>
                BuildCompDetailContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendPageKey.HomePageMarket =>
                BuildHomePageMarketContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendPageKey.Setup =>
                BuildSetupContent(request, promptTotal),
            LauncherFrontendPageKey.Tools =>
                BuildToolsContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendPageKey.HelpDetail =>
                BuildHelpDetailContent(request, promptTotal, visibleUtilityCount),
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

    private static LauncherFrontendPageContent BuildInstanceSelectContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        string? selectedLaneTitle)
    {
        var surface = request.Navigation.CurrentPage;

        return new LauncherFrontendPageContent(
            "Launch secondary integration",
            "Instance selection now lives on its own launch-secondary route while still reusing the launch-family prompt and readiness context.",
            [
                new LauncherFrontendPageFact("Surface", surface.Title),
                new LauncherFrontendPageFact("Back target", request.Navigation.BackTarget?.Label ?? "None"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString()),
                new LauncherFrontendPageFact("Focused prompt lane", selectedLaneTitle ?? "None")
            ],
            [
                new LauncherFrontendPageSection(
                    "Navigation",
                    "Launch-family route glue",
                    [
                        "The shell can enter instance selection directly without falling back to the generic compatibility surface.",
                        "Back navigation still resolves through the launch flow when no explicit history is available.",
                        "Prompt inbox state remains shared with the launch family instead of being rebuilt in the detail page."
                    ])
            ]);
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
        return request.Navigation.CurrentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.DownloadInstall => BuildDownloadInstallContent(request, promptTotal, visibleUtilityCount, selectedLaneTitle),
            LauncherFrontendSubpageKey.DownloadMod
                or LauncherFrontendSubpageKey.DownloadPack
                or LauncherFrontendSubpageKey.DownloadDataPack
                or LauncherFrontendSubpageKey.DownloadResourcePack
                or LauncherFrontendSubpageKey.DownloadShader
                or LauncherFrontendSubpageKey.DownloadWorld => BuildDownloadResourceContent(request, promptTotal, visibleUtilityCount, selectedLaneTitle),
            _ => BuildGenericDownloadContent(request, promptTotal, visibleUtilityCount, selectedLaneTitle)
        };
    }

    private static LauncherFrontendPageContent BuildDownloadInstallContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount,
        string? selectedLaneTitle)
    {
        var surface = request.Navigation.CurrentPage;

        return new LauncherFrontendPageContent(
            "自动安装页面",
            "自动安装页已经切换到更接近原版选择态的顶部命名卡、告警提示和组件选择卡结构。",
            [
                new LauncherFrontendPageFact("当前分区", surface.SidebarItemTitle ?? surface.Title),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Sidebar routes", request.Navigation.SidebarEntries.Count.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "顶部",
                    "安装方案命名",
                    [
                        "保留了顶部返回按钮、图标与名称输入框组合，而不是退回普通标题栏。",
                        "红色和黄色提示条继续作为独立块出现在名称卡后方。",
                        $"Focused prompt lane: {selectedLaneTitle ?? "None"}"
                    ]),
                new LauncherFrontendPageSection(
                    "组件",
                    "安装器选项卡",
                    [
                        "Forge、Fabric、Quilt、OptiFine 等组件继续按原页面一张张卡片排列。",
                        "每张卡继续保留右上角清除操作和当前版本信息区，而不是合并成单一资源列表。",
                        "这页优先复制的是旧版选择面结构，不是旧版安装策略实现。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "后续所需合同",
                    [
                        "更细的加载器版本、兼容性提示和自动推荐结果仍需要后端合同输入。",
                        "前端已经准备好承载这些数据，并保持原版控件层级。",
                        "安装规划与冲突判断仍应继续停留在壳层之外。"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildDownloadResourceContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount,
        string? selectedLaneTitle)
    {
        var surface = request.Navigation.CurrentPage;
        var routeTitle = surface.SidebarItemTitle ?? surface.Title;

        return new LauncherFrontendPageContent(
            $"{routeTitle} 页面",
            "社区资源页已经切换为更接近原版 PageComp 的搜索框、筛选卡、结果列表与分页卡结构，而不是继续停留在下载摘要卡上。",
            [
                new LauncherFrontendPageFact("当前分区", routeTitle),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Sidebar routes", request.Navigation.SidebarEntries.Count.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "筛选",
                    "搜索与条件卡",
                    [
                        "顶部保留了原版资源搜索框，以及来源、标签、排序方式、版本、加载器这组两行筛选布局。",
                        "Mod、整合包、数据包、资源包、光影包和世界页都继续共享这一套 PageComp 风格结构。",
                        $"Focused prompt lane: {selectedLaneTitle ?? "None"}"
                    ]),
                new LauncherFrontendPageSection(
                    "列表",
                    "资源结果卡",
                    [
                        "结果区继续使用独立资源列表卡和底部分页卡，而不是退回到概览摘要块。",
                        "整合包页也继续保留了额外的安装入口，而不是把它和普通资源页合并。",
                        "资源行中的图标、说明与详情按钮继续按旧版下载页层级摆放。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "前端与后端的分工",
                    [
                        "前端负责复制原版筛选与列表层级，后端仍应提供真实资源数据与安装规划合同。",
                        "筛选交互已经可以在新壳层中验证，而不需要把旧的 WPF 页面逻辑重新搬回来。",
                        "后续可以继续把真实搜索结果直接填进这套已复制的外壳。"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildGenericDownloadContent(
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

    private static LauncherFrontendPageContent BuildCompDetailContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return new LauncherFrontendPageContent(
            "Download detail integration",
            "Project detail now resolves as an explicit download-family detail route, so the shell keeps download navigation and route-local metadata instead of falling back to the generic download summary.",
            [
                new LauncherFrontendPageFact("Surface", request.Navigation.CurrentPage.Title),
                new LauncherFrontendPageFact("Sidebar group", request.Navigation.CurrentPage.SidebarGroupTitle ?? "None"),
                new LauncherFrontendPageFact("Back target", request.Navigation.BackTarget?.Label ?? "None"),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Integration",
                    "Download-family routing",
                    [
                        "Detail routes now stay attached to the download family for shell navigation and back-target resolution.",
                        "The shared contract remains responsible only for route identity and surrounding shell chrome.",
                        $"Queued prompts: {promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildHomePageMarketContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return new LauncherFrontendPageContent(
            "Market integration surface",
            "Home page market now resolves through a dedicated detail route while staying attached to the download family for surrounding shell navigation.",
            [
                new LauncherFrontendPageFact("Surface", request.Navigation.CurrentPage.Title),
                new LauncherFrontendPageFact("Sidebar group", request.Navigation.CurrentPage.SidebarGroupTitle ?? "None"),
                new LauncherFrontendPageFact("Back target", request.Navigation.BackTarget?.Label ?? "None"),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Integration",
                    "Market route glue",
                    [
                        "The shell can now enter the market route without reusing the generic download migration content.",
                        "Normal download navigation stays available around the dedicated market surface.",
                        $"Queued prompts: {promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return request.Navigation.CurrentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.SetupLaunch => BuildSetupLaunchContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupAbout => BuildSetupAboutContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupFeedback => BuildSetupFeedbackContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupGameManage => BuildSetupGameManageContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupGameLink => BuildSetupGameLinkContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupJava => BuildSetupJavaContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupLauncherMisc => BuildSetupLauncherMiscContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupLog => BuildSetupLogContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupUI => BuildSetupUiContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupUpdate => BuildSetupUpdateContent(request, promptTotal),
            _ => BuildGenericSetupContent(request, promptTotal)
        };
    }

    private static LauncherFrontendPageContent BuildSetupLaunchContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var launch = request.Launch;

        return new LauncherFrontendPageContent(
            "启动页面",
            "启动参数、内存和高级启动选项已经可以按原版 PageSetupLaunch 的三段结构进入新前端。",
            [
                new LauncherFrontendPageFact("当前分区", "启动"),
                new LauncherFrontendPageFact("Recommended runtime", launch?.JavaRuntimeLabel ?? "No Java summary"),
                new LauncherFrontendPageFact("Resolution baseline", launch?.ResolutionLabel ?? "No resolution summary"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "启动选项",
                    "基础启动参数",
                    [
                        "保留了默认版本隔离、窗口标题、自定义信息、启动器可见性、进程优先级和窗口大小这些原始表单行。",
                        "正版验证方式和 IP 协议偏好也继续停留在同一张卡里，而不是被拆成新的抽象设置页。",
                        "这页适合继续复用组合框、文本框和紧凑表单栅格。"
                    ]),
                new LauncherFrontendPageSection(
                    "内存",
                    "游戏内存",
                    [
                        "保留了自动配置 / 自定义 的切换、内存优化复选项和底部内存分配展示条。",
                        "Java 位数告警和过高内存提示继续作为独立提示块存在，而不是退回通用说明文本。",
                        "真正的内存建议策略仍然应来自后端或运行时边界。"
                    ]),
                new LauncherFrontendPageSection(
                    "高级",
                    "高级启动选项",
                    [
                        "渲染器、JVM 参数头部、游戏参数尾部和启动前执行命令继续按原版高级卡片组织。",
                        "Java Launch Wrapper、RetroWrapper 和显卡偏好也继续保持原始复选框语义。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
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

    private static LauncherFrontendPageContent BuildSetupGameManageContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return new LauncherFrontendPageContent(
            "游戏管理页面",
            "下载源、线程限制、社区资源命名和辅助功能都可以按原版三张设置卡的结构进入新前端。",
            [
                new LauncherFrontendPageFact("当前分区", "游戏管理"),
                new LauncherFrontendPageFact("下载分组", "游戏资源 / 社区资源 / 辅助功能"),
                new LauncherFrontendPageFact("主要控件", "下拉框、滑块、复选框"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "下载",
                    "游戏资源获取行为",
                    [
                        "保留了文件下载源、版本列表源、线程数、速度限制和安装行为的原始分组。",
                        "目标文件夹仍然保持成一段只读提示，而不是在新前端里重新发明设置流程。",
                        "左侧附加按钮会在 Spike 中把这些字段恢复到默认演示值。"
                    ]),
                new LauncherFrontendPageSection(
                    "社区",
                    "社区资源获取行为",
                    [
                        "文件名格式、Mod 管理样式和 Quilt 显示选项继续按原页面拆到单独卡片。",
                        "这种页面很适合持续验证表单控件复制，而不需要引入新的视觉语言。",
                        "真正的下载与安装策略仍应停留在后端边界之外。"
                    ]),
                new LauncherFrontendPageSection(
                    "辅助",
                    "提示与语言",
                    [
                        "正式版 / 测试版更新提示、自动中文和剪贴板识别继续保留原始布局层级。",
                        "这页补上后，设置分组里已经有多个不再依赖通用摘要卡的右侧页面。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupJavaContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var launch = request.Launch;

        return new LauncherFrontendPageContent(
            "Java 页面",
            "Java 列表已经可以按原版的添加入口加运行时列表结构进入新前端，而不是停留在设置摘要卡片。",
            [
                new LauncherFrontendPageFact("当前分区", "Java"),
                new LauncherFrontendPageFact("Recommended runtime", launch?.JavaRuntimeLabel ?? "No Java summary"),
                new LauncherFrontendPageFact("Download target", launch?.JavaDownloadTarget ?? "No download target"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "列表",
                    "Java 选择",
                    [
                        "保留了顶部添加按钮、自动选择条目和运行时列表这三个关键层级。",
                        "每个 Java 项继续保留路径、副标签以及打开 / 详情 / 启用状态切换入口。",
                        "这一页更接近原版的工具型列表，而不是新的通用设置表单。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "前端该做什么",
                    [
                        "前端只负责展示 Java 运行时清单和收集选择意图。",
                        "真实扫描、启用状态持久化和 Java 工作流策略仍然属于后端或运行时适配层。",
                        "刷新列表动作在 Spike 中仍然只是演示用的壳层行为。"
                    ]),
                new LauncherFrontendPageSection(
                    "迁移",
                    "为什么现在做这页",
                    [
                        "它能验证列表型设置页、按钮条和更细的行内操作布局。",
                        "也让设置页不再只有卡片式表单，还覆盖了实例化资源列表这类常见页面。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupLauncherMiscContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return new LauncherFrontendPageContent(
            "启动器杂项页面",
            "系统、网络和调试三组原版设置卡已经可以继续按 PageSetupLauncherMisc 的结构进入新前端。",
            [
                new LauncherFrontendPageFact("当前分区", "启动器杂项"),
                new LauncherFrontendPageFact("卡片分组", "系统 / 网络 / 调试"),
                new LauncherFrontendPageFact("主要控件", "下拉框、滑块、单选框、复选框"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "系统",
                    "公告、帧率与日志",
                    [
                        "保留了启动器公告、最高动画帧率、实时日志行数和导入导出设置按钮的原始顺序。",
                        "系统卡片中的复选框和底部按钮继续沿用原来的紧凑排布。",
                        "这类页面很适合继续收敛到原版控件密度，而不是引入新的设置页风格。"
                    ]),
                new LauncherFrontendPageSection(
                    "网络",
                    "DoH 与 HTTP 代理",
                    [
                        "网络卡片继续保留 DoH 复选框、三段 HTTP 代理模式和自定义代理输入区。",
                        "自定义代理区域只在选中对应模式时显示，和原页面保持一致。",
                        "应用代理按钮仍然只是前端意图，真实网络与配置写入边界不应回流到页面内部。"
                    ]),
                new LauncherFrontendPageSection(
                    "调试",
                    "折叠的调试选项",
                    [
                        "调试选项继续以默认折叠的卡片出现，保留动画速度和三个复选框。",
                        "这也让新前端继续验证折叠卡片和更密集表单的组合方式。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildSetupUiContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return new LauncherFrontendPageContent(
            "界面页面",
            "个性化界面页已经切换为更接近原版 PageSetupUI 的多卡片设置结构，而不是通用迁移摘要。",
            [
                new LauncherFrontendPageFact("当前分区", "界面"),
                new LauncherFrontendPageFact("卡片分组", "基础 / 字体 / 背景 / 标题栏 / 主页 / 功能隐藏"),
                new LauncherFrontendPageFact("主要控件", "滑块、单选框、复选框、组合框"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "基础",
                    "主题与高级材质",
                    [
                        "保留了不透明度、主题组合框、基础复选项和高级材质区域的主要结构。",
                        "顶部快照版提示也继续保留为独立提示块，而不是被吞进普通说明文字。",
                        "这页是当前设置分组中最密集的页面之一，适合持续检验控件复用。"
                    ]),
                new LauncherFrontendPageSection(
                    "扩展",
                    "字体、背景、标题栏与主页",
                    [
                        "字体、背景内容、背景音乐、标题栏和主页都继续按原版拆成独立卡片。",
                        "标题栏与主页仍然保留单选模式切换以及对应的条件区域。",
                        "按钮文案和分组顺序尽量贴近旧版，而不是重新命名为更抽象的通用配置项。"
                    ]),
                new LauncherFrontendPageSection(
                    "隐藏",
                    "功能隐藏",
                    [
                        "功能隐藏页块继续保留主页面、设置子页、工具子页、实例设置和特定功能这些分组。",
                        "未来如果要接真实配置值，应直接通过前端合同注入而不是重建旧页面事件流。",
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
            LauncherFrontendSubpageKey.ToolsGameLink => BuildToolsGameLinkContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendSubpageKey.ToolsLauncherHelp => BuildToolsHelpContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendSubpageKey.ToolsTest => BuildToolsTestContent(request, promptTotal, visibleUtilityCount),
            _ => BuildGenericToolsContent(request, promptTotal, visibleUtilityCount)
        };
    }

    private static LauncherFrontendPageContent BuildToolsGameLinkContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return new LauncherFrontendPageContent(
            "联机大厅页面",
            "工具页的联机大厅已经可以按原版的说明卡、加入大厅卡和创建大厅卡结构进入新前端。",
            [
                new LauncherFrontendPageFact("当前分区", "联机大厅"),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Back target", request.Navigation.BackTarget?.Label ?? "None"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "入口",
                    "大厅说明与条款",
                    [
                        "保留了顶部状态提示、联机大厅说明卡和条款确认按钮。",
                        "隐私政策与 Natayark 条款继续作为可点击列表项存在，而不是被压缩成纯文本说明。",
                        "登录、同意条款和打开外部网页都应保持为显式前端意图。"
                    ]),
                new LauncherFrontendPageSection(
                    "操作",
                    "加入与创建大厅",
                    [
                        "加入大厅卡继续保留输入框与加入 / 粘贴 / 清除按钮组合。",
                        "创建大厅卡继续保留世界选择、刷新和手动输入端口的操作排布。",
                        "NAT 类型与 Natayark 账户状态块也继续停留在右上角工具位。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "迁移规则",
                    [
                        "联机页面复制的是旧版结构与操作层级，不是旧版网络事件流。",
                        "真实大厅创建、状态同步和 EasyTier 运行时仍应停留在后端或外部组件边界。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
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
                        "帮助详情已经通过独立 HelpDetail 路由接入，所以列表和详情可以在同一工具家族内来回切换。",
                        "这也是复制 MySearchBox / MyCard 风格的低风险入口。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "后续所需合同",
                    [
                        "真实帮助条目、分类与详情正文继续通过各自的帮助内容合同输入。",
                        "共享壳层只负责列表路由、详情切换和工具页外层导航。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildHelpDetailContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return new LauncherFrontendPageContent(
            "Help detail integration",
            "Help detail now resolves as an explicit tools-family detail route, so normal tools navigation and back-target behavior stay intact around the dedicated detail surface.",
            [
                new LauncherFrontendPageFact("Surface", request.Navigation.CurrentPage.Title),
                new LauncherFrontendPageFact("Sidebar group", request.Navigation.CurrentPage.SidebarGroupTitle ?? "None"),
                new LauncherFrontendPageFact("Back target", request.Navigation.BackTarget?.Label ?? "None"),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "Integration",
                    "Tools-family routing",
                    [
                        "The shell keeps the help detail route separate from the generic tools compatibility view.",
                        "That preserves normal tools navigation while the dedicated detail surface renders the parsed help content.",
                        $"Queued prompts: {promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildToolsTestContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return new LauncherFrontendPageContent(
            "测试页面",
            "百宝箱、下载自定义文件、皮肤工具和成就生成器已经可以按原版 PageToolsTest 的卡片顺序进入新前端。",
            [
                new LauncherFrontendPageFact("当前分区", "测试"),
                new LauncherFrontendPageFact("Visible utilities", visibleUtilityCount.ToString()),
                new LauncherFrontendPageFact("Back target", request.Navigation.BackTarget?.Label ?? "None"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "工具",
                    "百宝箱",
                    [
                        "保留了顶部 WrapPanel 按钮区，包括内存优化、清理垃圾、崩溃测试和创建快捷方式这些入口。",
                        "危险操作继续保留为红色按钮，而不是和普通工具混成统一样式。",
                        "这些按钮仍然只代表显式意图，不应把旧版执行逻辑直接带回前端。"
                    ]),
                new LauncherFrontendPageSection(
                    "表单",
                    "下载与皮肤工具",
                    [
                        "下载自定义文件卡继续保留下载地址、User-Agent、保存目录和文件名四行表单。",
                        "正版皮肤下载与头像生成器也继续按原页面顺序保留在后续卡片中。",
                        "瞅眼服务器控件可以先保留卡位，等专属组件迁移时再替换成真正内容。"
                    ]),
                new LauncherFrontendPageSection(
                    "生成器",
                    "成就图片与头像",
                    [
                        "自定义成就图片生成器继续保留四个输入项和预览 / 保存按钮组合。",
                        "头像生成器也继续保留尺寸选择、选皮肤按钮和预览容器。",
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
            "Task manager integration surface",
            "Task manager now resolves as a dedicated utility route for live task-state shell wiring instead of a generic migration placeholder.",
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
                    "Integration",
                    "Utility-route glue",
                    [
                        "The shared contract now only carries utility-route chrome and surrounding workflow context.",
                        "Live task collection rendering stays inside the dedicated task-manager surface implementation.",
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
            "Game log integration surface",
            "Game log now resolves as a dedicated utility route, while collection and crash export policy remain backend responsibilities.",
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
                    "Integration",
                    "Utility-route glue",
                    [
                        "The shared contract keeps only the route shell and surrounding recovery context.",
                        "Live output and recent-file rendering stay inside the dedicated game-log surface implementation.",
                        "That boundary keeps the log surface replaceable across shells."
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildInstanceContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        if (request.Navigation.CurrentRoute.Subpage == LauncherFrontendSubpageKey.VersionOverall)
        {
            return BuildInstanceOverviewContent(request, promptTotal);
        }

        if (request.Navigation.CurrentRoute.Subpage == LauncherFrontendSubpageKey.VersionSetup)
        {
            return BuildInstanceSetupContent(request, promptTotal);
        }

        if (request.Navigation.CurrentRoute.Subpage == LauncherFrontendSubpageKey.VersionExport)
        {
            return BuildInstanceExportContent(request, promptTotal);
        }

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

    private static LauncherFrontendPageContent BuildInstanceExportContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return new LauncherFrontendPageContent(
            "实例导出页面",
            "实例导出页已经切换到更接近原版的命名卡、导出内容清单、高级选项和底部导出动作结构，而不是继续停留在实例摘要卡上。",
            [
                new LauncherFrontendPageFact("当前分区", "导出"),
                new LauncherFrontendPageFact("Page kind", request.Navigation.CurrentPage.Kind.ToString()),
                new LauncherFrontendPageFact("Sidebar routes", request.Navigation.SidebarEntries.Count.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "命名",
                    "整合包基础信息",
                    [
                        "顶部继续保留整合包名称与整合包版本这组双输入布局。",
                        "Modrinth 相关的 OptiFine 提示也继续作为独立提示条出现，而不是被折叠成普通说明文字。",
                        "这页优先复制的是原版导出流程外壳，不是导出实现本身。"
                    ]),
                new LauncherFrontendPageSection(
                    "清单",
                    "导出内容与高级选项",
                    [
                        "主体继续保留大量复选框组成的导出内容清单，包括 Mod、资源包、光影包、存档和 PCL 程序等分组。",
                        "高级选项区也继续保留打包资源文件、Modrinth 上传模式和配置导入导出按钮。",
                        "底部导出按钮继续作为独立的大动作入口，而不是改造成普通卡片内按钮。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "迁移规则",
                    [
                        "前端负责复制原版表单层级和勾选流程，真实文件匹配和打包策略仍应留在后端或运行时边界。",
                        "配置导入导出也应继续作为显式壳层意图，而不是把旧页面代码直接搬回来。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildInstanceSetupContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return new LauncherFrontendPageContent(
            "实例设置页面",
            "实例设置页已经切换到更接近原版的蓝色提示、启动选项、游戏内存、服务器、高级启动选项和底部跳转按钮结构，而不是继续停留在实例摘要卡上。",
            [
                new LauncherFrontendPageFact("当前分区", "设置"),
                new LauncherFrontendPageFact("Page kind", request.Navigation.CurrentPage.Kind.ToString()),
                new LauncherFrontendPageFact("Sidebar routes", request.Navigation.SidebarEntries.Count.ToString()),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "基础",
                    "实例专属启动设置",
                    [
                        "顶部继续保留实例专属设置的蓝色提示条，以及启动选项和游戏内存两张主要设置卡。",
                        "版本隔离、窗口标题、自定义信息、实例 Java 和内存分配继续按原版表单顺序排列。",
                        "内存区也继续保留提示条、单选切换和底部内存展示条，而不是退回普通摘要文本。"
                    ]),
                new LauncherFrontendPageSection(
                    "扩展",
                    "服务器与高级启动选项",
                    [
                        "服务器卡继续保留限制验证方式、第三方验证信息、自动进入服务器和底部三枚动作按钮。",
                        "高级启动选项卡也继续保留渲染器、JVM 参数、游戏参数、Classpath、启动前执行命令和复选框列表。",
                        "底部“全局设置”按钮继续作为独立跳转动作存在，而不是隐藏到卡片内。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "迁移规则",
                    [
                        "前端负责复制原版实例设置表单层级，真实实例覆盖、验证方式和启动策略仍应由后端或运行时边界负责。",
                        "这些输入项最终应绑定到明确的实例级合同，而不是回到旧的 WPF 事件流。",
                        $"当前可见提示数：{promptTotal}"
                    ])
            ]);
    }

    private static LauncherFrontendPageContent BuildInstanceOverviewContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var launch = request.Launch;

        return new LauncherFrontendPageContent(
            "实例概览页面",
            "实例概览页已经切换到更接近原版的展示卡、实例信息、个性化、快捷方式和高级管理五段结构，而不是继续停留在概览摘要卡上。",
            [
                new LauncherFrontendPageFact("当前分区", "概览"),
                new LauncherFrontendPageFact("Identity surface", launch?.SelectedIdentityLabel ?? "No identity summary"),
                new LauncherFrontendPageFact("Java", launch?.JavaRuntimeLabel ?? "No Java summary"),
                new LauncherFrontendPageFact("Queued prompts", promptTotal.ToString())
            ],
            [
                new LauncherFrontendPageSection(
                    "展示",
                    "实例主卡与信息卡",
                    [
                        "顶部继续保留实例展示卡和实例信息卡，而不是把概览页压缩成一组事实清单。",
                        "个性化区也继续保留图标、分类和三枚主操作按钮的原始层级。",
                        "这页适合继续复用 MyCard、MyComboBox 和 MyButton 的布局关系。"
                    ]),
                new LauncherFrontendPageSection(
                    "操作",
                    "快捷方式与高级管理",
                    [
                        "快捷方式继续保留实例文件夹、存档文件夹、Mod 文件夹三枚按钮。",
                        "高级管理区也继续保持导出脚本、测试游戏、补全文件、重置、删除实例和修补核心的按钮顺序。",
                        "危险操作仍然保持独立的强调按钮，而不是和普通工具混成一套新样式。"
                    ]),
                new LauncherFrontendPageSection(
                    "边界",
                    "迁移规则",
                    [
                        "概览页复制的是旧版实例管理外壳，不是旧版实例修改逻辑。",
                        "真实实例元数据、文件检查和脚本导出仍应通过后端合同或显式壳层意图提供。",
                        $"当前可见提示数：{promptTotal}"
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
