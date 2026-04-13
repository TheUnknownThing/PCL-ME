using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        var selectedLaneTitle = request.PromptLanes.FirstOrDefault(lane => lane.IsSelected)?.Title;
        var visibleUtilityCount = request.Navigation.UtilityEntries.Count(entry => entry.IsVisible);

        return request.Navigation.CurrentRoute.Page switch
        {
            LauncherFrontendPageKey.Launch =>
                BuildLaunchContent(request, promptTotal, selectedLaneTitle),
            LauncherFrontendPageKey.InstanceSelect =>
                BuildInstanceSelectContent(request, promptTotal, selectedLaneTitle),
            LauncherFrontendPageKey.Download =>
                BuildDownloadContent(request, promptTotal, visibleUtilityCount, selectedLaneTitle),
            LauncherFrontendPageKey.CompDetail =>
                BuildCompDetailContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendPageKey.HomePageMarket =>
                BuildHomePageMarketContent(request, promptTotal, visibleUtilityCount),
            LauncherFrontendPageKey.Setup =>
                BuildSetupContent(request, promptTotal),
            LauncherFrontendPageKey.Tools =>
                BuildToolsContent(request, promptTotal),
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

        return CreateContent(
            "实例选择页面",
            "选择要启动的实例。",
            BuildFacts(
                ("当前页面", surface.Title),
                ("返回目标", request.Navigation.BackTarget?.Label ?? "无"),
                ("提示数", promptTotal.ToString()),
                ("当前提示分组", selectedLaneTitle ?? "无")),
            Section(
                "实例",
                "当前状态",
                "从列表中选择要启动的实例。",
                "选择后可返回启动页继续启动。"));
    }

    private static LauncherFrontendPageContent BuildLaunchContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        string? selectedLaneTitle)
    {
        var launch = request.Launch;
        var title = request.Navigation.CurrentPage.SidebarItemTitle ?? request.Navigation.CurrentPage.Title;

        return CreateContent(
            "启动页面",
            "查看当前账号、Java 和启动前准备状态。",
            BuildFacts(
                ("当前页面", title),
                ("登录", launch?.LoginProviderLabel ?? "未提供"),
                ("Java", launch?.JavaRuntimeLabel ?? "未提供"),
                ("Java 警告", launch?.JavaWarningMessage ?? "无"),
                ("提示数", promptTotal.ToString())),
            Section(
                "当前启动",
                "启动信息",
                $"场景: {launch?.ScenarioLabel ?? "未提供"}",
                $"身份: {launch?.SelectedIdentityLabel ?? "未提供"}",
                $"登录步骤: {launch?.LoginStepCount.ToString() ?? "0"}",
                $"当前提示分组: {selectedLaneTitle ?? "无"}"),
            Section(
                "运行时",
                "Java 与分辨率",
                $"Java: {launch?.JavaRuntimeLabel ?? "未提供"}",
                $"Java 警告: {launch?.JavaWarningMessage ?? "无"}",
                $"下载目标: {launch?.JavaDownloadTarget ?? "无"}",
                $"分辨率: {launch?.ResolutionLabel ?? "未提供"}",
                $"Classpath 条目: {launch?.ClasspathEntryCount.ToString() ?? "0"} | 替换值: {launch?.ReplacementValueCount.ToString() ?? "0"}"),
            Section(
                "文件",
                "启动前文件",
                $"options.txt: {FormatPath(launch?.OptionsTargetFilePath) ?? "未提供"}",
                $"launcher_profiles: {FormatBool(launch?.WritesLauncherProfiles)}",
                $"脚本导出: {FormatPath(launch?.ScriptExportPath) ?? "无"}",
                $"natives: {FormatPath(launch?.NativesDirectory) ?? "未提供"}",
                string.IsNullOrWhiteSpace(launch?.CompletionMessage) ? null : $"最近状态: {launch!.CompletionMessage}"));
    }

    private static LauncherFrontendPageContent BuildDownloadContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount,
        string? selectedLaneTitle)
    {
        return request.Navigation.CurrentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.DownloadInstall =>
                BuildDownloadInstallContent(request, promptTotal, visibleUtilityCount, selectedLaneTitle),
            LauncherFrontendSubpageKey.DownloadClient =>
                BuildDownloadClientContent(request, promptTotal, visibleUtilityCount, selectedLaneTitle),
            LauncherFrontendSubpageKey.DownloadMod
                or LauncherFrontendSubpageKey.DownloadPack
                or LauncherFrontendSubpageKey.DownloadDataPack
                or LauncherFrontendSubpageKey.DownloadResourcePack
                or LauncherFrontendSubpageKey.DownloadShader
                or LauncherFrontendSubpageKey.DownloadWorld =>
                BuildDownloadResourceContent(request, promptTotal, visibleUtilityCount, selectedLaneTitle),
            _ => BuildGenericDownloadContent(request, promptTotal, visibleUtilityCount, selectedLaneTitle)
        };
    }

    private static LauncherFrontendPageContent BuildDownloadInstallContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount,
        string? selectedLaneTitle)
    {
        return BuildSidebarPageContent(
            request,
            "自动安装页面",
            "设置实例名称、选择 Minecraft 版本并确认安装组件。",
            promptTotal,
            visibleUtilityCount,
            Section(
                "当前步骤",
                "安装流程",
                "先选择 Minecraft 版本，再确认实例名称。",
                "根据需要选择 Forge、Fabric、Quilt、OptiFine 等组件。",
                $"当前提示分组: {selectedLaneTitle ?? "无"}"),
            Section(
                "组件",
                "安装项",
                "每个安装项都会显示当前状态、选择入口和清除操作。",
                "修改 Minecraft 版本后，请重新确认已选择的组件。"));
    }

    private static LauncherFrontendPageContent BuildDownloadClientContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount,
        string? selectedLaneTitle)
    {
        return BuildSidebarPageContent(
            request,
            "Minecraft 安装页面",
            "浏览并选择要安装的 Minecraft 版本。",
            promptTotal,
            visibleUtilityCount,
            Section(
                "版本列表",
                "Minecraft 版本分组",
                "版本会按最新版本、正式版、预览版、远古版和愚人节版分组显示。",
                "选择版本后可继续设置实例名称并开始安装。",
                $"当前提示分组: {selectedLaneTitle ?? "无"}"),
            Section(
                "安装入口",
                "直接安装",
                "此页专注于 Minecraft 版本本身的安装。",
                "如需安装 Forge、Fabric 等组件，可在自动安装页继续选择。"));
    }

    private static LauncherFrontendPageContent BuildDownloadResourceContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount,
        string? selectedLaneTitle)
    {
        var routeTitle = request.Navigation.CurrentPage.SidebarItemTitle ?? request.Navigation.CurrentPage.Title;

        return BuildSidebarPageContent(
            request,
            $"{routeTitle} 页面",
            $"搜索并浏览 {routeTitle}。",
            promptTotal,
            visibleUtilityCount,
            Section(
                "筛选",
                "搜索与条件",
                "可按来源、标签、排序、版本与加载器筛选结果。",
                $"当前提示分组: {selectedLaneTitle ?? "无"}"),
            Section(
                "结果",
                "资源列表",
                "结果列表会显示名称、简介、标签和详情入口。",
                "翻页后会继续保留当前筛选条件。"));
    }

    private static LauncherFrontendPageContent BuildGenericDownloadContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount,
        string? selectedLaneTitle)
    {
        var routeTitle = request.Navigation.CurrentPage.SidebarItemTitle ?? request.Navigation.CurrentPage.Title;

        return BuildSidebarPageContent(
            request,
            $"{routeTitle} 页面",
            request.Navigation.CurrentPage.Summary,
            promptTotal,
            visibleUtilityCount,
            Section(
                "版本列表",
                "当前页面",
                $"可以在此浏览 {routeTitle} 的可用版本。",
                $"当前提示分组: {selectedLaneTitle ?? "无"}"));
    }

    private static LauncherFrontendPageContent BuildCompDetailContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return BuildDetailPageContent(
            request,
            "资源详情页面",
            "查看资源简介、版本信息与相关操作。",
            promptTotal,
            visibleUtilityCount,
            Section(
                "详情",
                "当前页面",
                "这里会显示选中资源的说明、版本信息和相关入口。"));
    }

    private static LauncherFrontendPageContent BuildHomePageMarketContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return BuildDetailPageContent(
            request,
            "主页市场页面",
            "查看推荐内容与相关入口。",
            promptTotal,
            visibleUtilityCount,
            Section(
                "推荐",
                "当前页面",
                "这里会显示主页市场的推荐内容与可选入口。"));
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
            LauncherFrontendSubpageKey.SetupJava => BuildSetupJavaContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupLauncherMisc => BuildSetupLauncherMiscContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupLog => BuildSetupLogContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupUI => BuildSetupUiContent(request, promptTotal),
            LauncherFrontendSubpageKey.SetupUpdate => BuildSetupUpdateContent(request, promptTotal),
            _ => BuildSetupOverviewContent(request, promptTotal)
        };
    }

    private static LauncherFrontendPageContent BuildSetupOverviewContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var startup = request.StartupPlan;
        var launch = request.Launch;

        return CreateContent(
            "设置页面",
            "查看启动器当前的基础设置状态。",
            BuildFacts(
                ("更新通道", startup.Bootstrap.DefaultUpdateChannel.ToString()),
                ("配置项", startup.Bootstrap.ConfigKeysToLoad.Count.ToString()),
                ("协议提示", request.Consent.Prompts.Count.ToString()),
                ("提示数", promptTotal.ToString())),
            Section(
                "启动准备",
                "当前状态",
                $"待创建目录: {startup.Bootstrap.DirectoriesToCreate.Count}",
                $"旧日志清理项: {startup.Bootstrap.LegacyLogFilesToDelete.Count}",
                $"立即命令: {startup.ImmediateCommand.Kind}",
                $"环境提示: {startup.Bootstrap.EnvironmentWarningMessage ?? "无"}"),
            Section(
                "运行时",
                "Java 与显示",
                $"推荐 Java: {launch?.JavaRuntimeLabel ?? "未提供"}",
                $"Java 警告: {launch?.JavaWarningMessage ?? "无"}",
                $"下载目标: {launch?.JavaDownloadTarget ?? "无"}",
                $"分辨率: {launch?.ResolutionLabel ?? "未提供"}",
                $"启动图标: {FormatBool(startup.Visual.ShouldShowSplashScreen)}"));
    }

    private static LauncherFrontendPageContent BuildSetupLaunchContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var launch = request.Launch;

        return CreateContent(
            "启动页面",
            "调整启动参数、Java、内存与高级启动选项。",
            BuildFacts(
                ("当前分区", "启动"),
                ("Java", launch?.JavaRuntimeLabel ?? "未提供"),
                ("Java 警告", launch?.JavaWarningMessage ?? "无"),
                ("分辨率", launch?.ResolutionLabel ?? "未提供"),
                ("提示数", promptTotal.ToString())),
            Section(
                "启动选项",
                "基础启动参数",
                "版本隔离、窗口标题、自定义信息和窗口大小会显示在此页。",
                "验证方式和网络协议偏好也可在此调整。"),
            Section(
                "内存",
                "游戏内存",
                "可在自动配置和自定义之间切换内存设置。",
                "内存提示会根据当前配置显示在此页。"),
            Section(
                "高级",
                "高级启动选项",
                "可查看 Java、JVM 参数、游戏参数和启动前命令。",
                $"脚本导出: {FormatPath(launch?.ScriptExportPath) ?? "无"}"));
    }

    private static LauncherFrontendPageContent BuildSetupAboutContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var startup = request.StartupPlan;

        return CreateContent(
            "关于页面",
            "查看版本信息、项目成员与特别鸣谢。",
            BuildFacts(
                ("当前分区", "关于"),
                ("更新通道", startup.Bootstrap.DefaultUpdateChannel.ToString()),
                ("配置项", startup.Bootstrap.ConfigKeysToLoad.Count.ToString()),
                ("提示数", promptTotal.ToString())),
            Section(
                "关于",
                "项目与团队",
                "版本信息、项目成员、头像和相关链接会显示在此页。"),
            Section(
                "鸣谢",
                "特别鸣谢",
                "鸣谢列表和相关说明会集中展示。"));
    }

    private static LauncherFrontendPageContent BuildSetupFeedbackContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "反馈页面",
            "查看反馈入口与问题列表。",
            BuildFacts(
                ("当前分区", "反馈"),
                ("协议提示", request.Consent.Prompts.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "反馈",
                "反馈入口",
                "可以打开反馈入口并提交问题或建议。"),
            Section(
                "列表",
                "反馈状态",
                "反馈会按处理状态分组显示。"));
    }

    private static LauncherFrontendPageContent BuildSetupUpdateContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var startup = request.StartupPlan;

        return CreateContent(
            "更新页面",
            "管理更新通道并查看当前版本状态。",
            BuildFacts(
                ("当前分区", "更新"),
                ("更新通道", startup.Bootstrap.DefaultUpdateChannel.ToString()),
                ("配置项", startup.Bootstrap.ConfigKeysToLoad.Count.ToString()),
                ("提示数", promptTotal.ToString())),
            Section(
                "设置",
                "更新设置",
                "可调整更新通道和自动检查设置。",
                "Mirror 酱 CDK 等相关输入也会显示在此页。"),
            Section(
                "状态",
                "版本信息",
                "可用更新与当前版本状态会显示在此页。",
                "可从此进入下载或查看详情。"));
    }

    private static LauncherFrontendPageContent BuildSetupGameManageContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "游戏管理页面",
            "调整下载、资源与辅助功能相关设置。",
            BuildFacts(
                ("当前分区", "游戏管理"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "下载与资源",
                "基础设置",
                "可设置下载源、线程数、命名方式和下载目录。"),
            Section(
                "辅助功能",
                "显示与提示",
                "无障碍和其他显示相关选项会在此页显示。"));
    }

    private static LauncherFrontendPageContent BuildSetupJavaContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "Java 页面",
            "管理 Java 运行时列表与默认选择。",
            BuildFacts(
                ("当前分区", "Java"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "Java 列表",
                "运行时管理",
                "可查看、添加、启用或禁用 Java 运行时。"),
            Section(
                "默认选择",
                "当前设置",
                "可设置自动选择或手动指定默认 Java。"));
    }

    private static LauncherFrontendPageContent BuildSetupLauncherMiscContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "启动器杂项页面",
            "调整系统、网络与调试相关设置。",
            BuildFacts(
                ("当前分区", "启动器杂项"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "系统与网络",
                "基础设置",
                "系统行为、网络代理和相关开关会显示在此页。"),
            Section(
                "调试",
                "调试选项",
                "调试相关的开关与说明会显示在此页。"));
    }

    private static LauncherFrontendPageContent BuildSetupLogContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "日志页面",
            "查看日志目录并执行导出或清理操作。",
            BuildFacts(
                ("当前分区", "日志"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "日志操作",
                "导出与清理",
                "可导出日志、打开日志目录或清理历史日志。"),
            Section(
                "实时输出",
                "相关页面",
                "启动后的实时输出会显示在日志页或实时日志页。"));
    }

    private static LauncherFrontendPageContent BuildSetupUiContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "界面页面",
            "调整主题、背景、音乐与主页显示。",
            BuildFacts(
                ("当前分区", "界面"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "界面与背景",
                "基础设置",
                "可修改界面样式、背景内容与标题栏图片。"),
            Section(
                "主页与音乐",
                "附加内容",
                "可调整主页内容和背景音乐。"));
    }

    private static LauncherFrontendPageContent BuildToolsContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return request.Navigation.CurrentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.ToolsLauncherHelp => BuildToolsHelpContent(request, promptTotal),
            LauncherFrontendSubpageKey.ToolsTest => BuildToolsTestContent(request, promptTotal),
            _ => BuildGenericToolsContent(request, promptTotal)
        };
    }

    private static LauncherFrontendPageContent BuildToolsHelpContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "帮助页面",
            "搜索帮助内容并浏览分类主题。",
            BuildFacts(
                ("当前分区", "帮助"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "搜索",
                "搜索帮助",
                "使用搜索框筛选帮助内容。",
                "搜索结果区会显示匹配的帮助条目。"),
            Section(
                "列表",
                "帮助主题",
                "帮助主题会按分类显示。"));
    }

    private static LauncherFrontendPageContent BuildToolsTestContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "测试页面",
            "使用下载、皮肤、网络与成就相关工具。",
            BuildFacts(
                ("当前分区", "测试"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "工具",
                "下载与皮肤工具",
                "可以下载自定义文件、保存皮肤或生成成就图片。",
                "网络工具会显示当前 User-Agent。"),
            Section(
                "服务器",
                "服务器工具",
                "服务器地址输入框可用于查询服务器信息。"));
    }

    private static LauncherFrontendPageContent BuildGenericToolsContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var routeTitle = request.Navigation.CurrentPage.SidebarItemTitle ?? request.Navigation.CurrentPage.Title;

        return CreateContent(
            $"{routeTitle} 页面",
            request.Navigation.CurrentPage.Summary,
            BuildFacts(
                ("当前分区", routeTitle),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "当前页面",
                "说明",
                request.Navigation.CurrentPage.Summary));
    }

    private static LauncherFrontendPageContent BuildHelpDetailContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return BuildDetailPageContent(
            request,
            "帮助详情页面",
            "查看帮助条目正文与相关操作。",
            promptTotal,
            visibleUtilityCount,
            Section(
                "帮助",
                "当前条目",
                "从帮助列表选择条目后，可在此查看说明。"));
    }

    private static LauncherFrontendPageContent BuildTaskManagerContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return CreateContent(
            "任务管理页面",
            "查看下载、安装和其他后台任务。",
            BuildFacts(
                ("当前页面", request.Navigation.CurrentPage.Title),
                ("可见工具", visibleUtilityCount.ToString()),
                ("提示数", promptTotal.ToString()),
                ("返回目标", request.Navigation.BackTarget?.Label ?? "无")),
            Section(
                "任务",
                "任务概览",
                "这里会显示后台任务的进度、速度和剩余文件。"));
    }

    private static LauncherFrontendPageContent BuildGameLogContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        return CreateContent(
            "实时日志页面",
            "查看实时输出与最近日志文件。",
            BuildFacts(
                ("当前页面", request.Navigation.CurrentPage.Title),
                ("可见工具", visibleUtilityCount.ToString()),
                ("提示数", promptTotal.ToString()),
                ("返回目标", request.Navigation.BackTarget?.Label ?? "无")),
            Section(
                "实时日志",
                "当前会话",
                "当前会话输出会实时追加到此页。"),
            Section(
                "文件",
                "最近日志",
                "可查看最近生成的日志文件并执行导出。"));
    }

    private static LauncherFrontendPageContent BuildInstanceContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return request.Navigation.CurrentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionSetup => BuildInstanceSetupContent(request, promptTotal),
            LauncherFrontendSubpageKey.VersionExport => BuildInstanceExportContent(request, promptTotal),
            LauncherFrontendSubpageKey.VersionWorld => BuildInstanceWorldContent(request, promptTotal),
            LauncherFrontendSubpageKey.VersionScreenshot => BuildInstanceScreenshotContent(request, promptTotal),
            LauncherFrontendSubpageKey.VersionInstall => BuildInstanceInstallContent(request, promptTotal),
            LauncherFrontendSubpageKey.VersionServer => BuildInstanceServerContent(request, promptTotal),
            LauncherFrontendSubpageKey.VersionMod
                or LauncherFrontendSubpageKey.VersionModDisabled
                or LauncherFrontendSubpageKey.VersionResourcePack
                or LauncherFrontendSubpageKey.VersionShader
                or LauncherFrontendSubpageKey.VersionSchematic =>
                BuildInstanceResourceContent(request, promptTotal),
            _ => BuildInstanceOverviewContent(request, promptTotal)
        };
    }

    private static LauncherFrontendPageContent BuildInstanceOverviewContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "实例概览页面",
            "查看当前实例概览、个性化信息和快捷操作。",
            BuildFacts(
                ("当前分区", "概览"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "概览",
                "实例信息",
                "这里会显示实例基本信息、个性化内容和快捷操作。"),
            Section(
                "管理",
                "高级操作",
                "高级操作会集中显示在此页。"));
    }

    private static LauncherFrontendPageContent BuildInstanceSetupContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "实例设置页面",
            "调整当前实例的启动、内存、服务器与高级选项。",
            BuildFacts(
                ("当前分区", "设置"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "实例设置",
                "启动与内存",
                "可修改版本隔离、窗口标题、自定义信息、Java 与内存分配。"),
            Section(
                "附加内容",
                "服务器与高级选项",
                "服务器设置和高级启动选项也会显示在此页。"));
    }

    private static LauncherFrontendPageContent BuildInstanceExportContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "实例导出页面",
            "选择要导出的内容并生成实例包。",
            BuildFacts(
                ("当前分区", "导出"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "导出",
                "导出内容",
                "可设置导出名称、选择包含内容并调整高级选项。"),
            Section(
                "开始导出",
                "当前步骤",
                "确认选项后即可开始导出。"));
    }

    private static LauncherFrontendPageContent BuildInstanceInstallContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var routeTitle = request.Navigation.CurrentPage.SidebarItemTitle ?? request.Navigation.CurrentPage.Title;

        return CreateContent(
            "实例修改页面",
            "修改 Minecraft 版本并选择需要的组件。",
            BuildFacts(
                ("当前分区", routeTitle),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                routeTitle,
                "修改项",
                "可修改 Minecraft 版本并选择需要的组件。",
                "开始操作前请确认组件兼容性。"));
    }

    private static LauncherFrontendPageContent BuildInstanceServerContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "实例服务器页面",
            "管理当前实例的服务器列表。",
            BuildFacts(
                ("当前分区", "服务器"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "服务器",
                "服务器列表",
                "可以查看、添加、编辑或删除服务器条目。"));
    }

    private static LauncherFrontendPageContent BuildInstanceWorldContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "实例世界页面",
            "浏览当前实例的世界存档。",
            BuildFacts(
                ("当前分区", "世界"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "世界",
                "世界列表",
                "可以查看、刷新或打开世界目录。"));
    }

    private static LauncherFrontendPageContent BuildInstanceScreenshotContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "实例截图页面",
            "浏览当前实例的截图。",
            BuildFacts(
                ("当前分区", "截图"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "截图",
                "截图列表",
                "可以浏览截图并执行相关操作。"));
    }

    private static LauncherFrontendPageContent BuildInstanceResourceContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        var routeTitle = request.Navigation.CurrentPage.SidebarItemTitle ?? request.Navigation.CurrentPage.Title;

        return CreateContent(
            $"{routeTitle} 页面",
            $"管理当前实例的 {routeTitle}。",
            BuildFacts(
                ("当前分区", routeTitle),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "资源",
                "资源列表",
                $"可以查看当前实例中的 {routeTitle} 条目并执行相关操作。"));
    }

    private static LauncherFrontendPageContent BuildSavesContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return request.Navigation.CurrentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionSavesBackup => BuildSavesBackupContent(request, promptTotal),
            LauncherFrontendSubpageKey.VersionSavesDatapack => BuildSavesDatapackContent(request, promptTotal),
            _ => BuildSavesInfoContent(request, promptTotal)
        };
    }

    private static LauncherFrontendPageContent BuildSavesInfoContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "存档信息页面",
            "查看当前存档信息与相关设置。",
            BuildFacts(
                ("当前分区", "概览"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "存档",
                "存档信息",
                "这里会显示当前存档的基本信息与相关设置。"));
    }

    private static LauncherFrontendPageContent BuildSavesBackupContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "存档备份页面",
            "管理当前存档的备份与恢复记录。",
            BuildFacts(
                ("当前分区", "备份"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "备份",
                "备份与恢复",
                "可以查看备份记录并执行恢复。"));
    }

    private static LauncherFrontendPageContent BuildSavesDatapackContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal)
    {
        return CreateContent(
            "存档数据包页面",
            "管理当前存档的数据包。",
            BuildFacts(
                ("当前分区", "数据包"),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString()),
                ("页面类型", request.Navigation.CurrentPage.Kind.ToString())),
            Section(
                "数据包",
                "数据包列表",
                "可以查看当前存档中的数据包并执行相关操作。"));
    }

    private static LauncherFrontendPageContent BuildGenericContent(
        LauncherFrontendPageContentRequest request,
        int promptTotal,
        int visibleUtilityCount)
    {
        var surface = request.Navigation.CurrentPage;

        return CreateContent(
            $"{surface.Title} 页面",
            request.Navigation.CurrentPage.Summary,
            BuildFacts(
                ("当前页面", surface.SidebarItemTitle ?? surface.Title),
                ("页面类型", surface.Kind.ToString()),
                ("可见工具", visibleUtilityCount.ToString()),
                ("提示数", promptTotal.ToString())),
            Section(
                "当前页面",
                "页面信息",
                $"返回目标: {request.Navigation.BackTarget?.Label ?? "无"}",
                $"导航层级: {request.Navigation.Breadcrumbs.Count}",
                request.Navigation.CurrentPage.Summary));
    }

    private static LauncherFrontendPageContent BuildSidebarPageContent(
        LauncherFrontendPageContentRequest request,
        string eyebrow,
        string summary,
        int promptTotal,
        int visibleUtilityCount,
        params LauncherFrontendPageSection[] sections)
    {
        var surface = request.Navigation.CurrentPage;

        return CreateContent(
            eyebrow,
            summary,
            BuildFacts(
                ("当前分区", surface.SidebarItemTitle ?? surface.Title),
                ("可见工具", visibleUtilityCount.ToString()),
                ("导航项", request.Navigation.SidebarEntries.Count.ToString()),
                ("提示数", promptTotal.ToString())),
            sections);
    }

    private static LauncherFrontendPageContent BuildDetailPageContent(
        LauncherFrontendPageContentRequest request,
        string eyebrow,
        string summary,
        int promptTotal,
        int visibleUtilityCount,
        params LauncherFrontendPageSection[] sections)
    {
        return CreateContent(
            eyebrow,
            summary,
            BuildFacts(
                ("当前页面", request.Navigation.CurrentPage.Title),
                ("返回目标", request.Navigation.BackTarget?.Label ?? "无"),
                ("可见工具", visibleUtilityCount.ToString()),
                ("提示数", promptTotal.ToString())),
            sections);
    }

    private static LauncherFrontendPageContent CreateContent(
        string eyebrow,
        string summary,
        IReadOnlyList<LauncherFrontendPageFact> facts,
        params LauncherFrontendPageSection[] sections)
    {
        return new LauncherFrontendPageContent(
            eyebrow,
            summary,
            facts,
            sections.Where(section => section.Lines.Count > 0).ToArray());
    }

    private static LauncherFrontendPageFact[] BuildFacts(params (string Label, string Value)[] entries)
    {
        return entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Label) && !string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => new LauncherFrontendPageFact(entry.Label, entry.Value))
            .ToArray();
    }

    private static LauncherFrontendPageSection Section(string eyebrow, string title, params string?[] lines)
    {
        return new LauncherFrontendPageSection(
            eyebrow,
            title,
            lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line!).ToArray());
    }

    private static string FormatBool(bool? value)
    {
        return value switch
        {
            true => "是",
            false => "否",
            null => "未提供"
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
    string? JavaWarningMessage,
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
