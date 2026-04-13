using Avalonia.Media;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>> BuildPromptCatalog(string scenario)
    {
        var startupPrompts = _startupPlan.StartupPlan.EnvironmentWarningPrompt is null
            ? LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent)
            : LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent);

        var launchPrompts = LauncherFrontendPromptService.BuildLaunchPromptQueue(
            BuildLaunchPrecheckResult(scenario),
            MinecraftLaunchShellService.GetSupportPrompt(10),
            _launchPlan.JavaWorkflow.MissingJavaPrompt);
        var crashPrompts = LauncherFrontendPromptService.BuildCrashPromptQueue(_crashPlan.OutputPrompt);

        return new Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>>
        {
            [SpikePromptLaneKind.Startup] = startupPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Startup, prompt)).ToList(),
            [SpikePromptLaneKind.Launch] = launchPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Launch, prompt)).ToList(),
            [SpikePromptLaneKind.Crash] = crashPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Crash, prompt)).ToList()
        };
    }

    private void InitializePromptLanes()
    {
        PromptLanes.Clear();
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Startup,
            "启动前",
            "许可、环境与首次启动提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Startup))));
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Launch,
            "启动中",
            "启动前检查、赞助与 Java 下载提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Launch))));
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Crash,
            "崩溃恢复",
            "崩溃输出与导出恢复提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Crash))));

        SyncPromptLaneState();
        SelectPromptLane(_selectedPromptLane);
    }

    private void SelectPromptLane(SpikePromptLaneKind lane, bool updateActivity = true)
    {
        _selectedPromptLane = lane;
        SyncPromptLaneState();
        ReplaceItems(ActivePrompts, _promptCatalog[lane]);
        RaisePropertyChanged(nameof(HasActivePrompts));
        RaisePropertyChanged(nameof(HasNoActivePrompts));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));

        var selectedLane = PromptLanes.First(item => item.Kind == lane);
        PromptInboxTitle = $"{selectedLane.Title}提示";
        PromptInboxSummary = selectedLane.Summary;
        PromptEmptyState = $"当前没有待处理的{selectedLane.Title}提示。";
        var pageContent = BuildPageContent(BuildShellPlan());
        ReplaceItems(SurfaceFacts, pageContent.Facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
        ReplaceItems(SurfaceSections, pageContent.Sections.Select((section, index) => CreateSurfaceSection(section, index)));
        RaiseCollectionStateProperties();

        if (updateActivity)
        {
            AddActivity("Switched prompt lane.", $"{selectedLane.Title} now has {selectedLane.Count} queued prompt(s).");
        }
    }

    private void SyncPromptLaneState()
    {
        foreach (var lane in PromptLanes)
        {
            lane.Count = _promptCatalog[lane.Kind].Count;
            lane.IsSelected = lane.Kind == _selectedPromptLane;
        }
    }

    private PromptCardViewModel CreatePromptCard(SpikePromptLaneKind lane, LauncherFrontendPrompt prompt)
    {
        return new PromptCardViewModel(
            lane,
            prompt.Id,
            prompt.Title,
            prompt.Message,
            prompt.Source.ToString(),
            prompt.Severity.ToString(),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning ? Brush.Parse("#A94F2B") : Brush.Parse("#256A61"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning ? Brush.Parse("#FFF1EA") : Brush.Parse("#EAF7F5"),
            prompt.Options.Select(option => new PromptOptionViewModel(
                option.Label,
                DescribePromptOption(option),
                new ActionCommand(() => ApplyPromptOption(lane, prompt.Id, option)))).ToList());
    }

    private void ApplyPromptOption(SpikePromptLaneKind lane, string promptId, LauncherFrontendPromptOption option)
    {
        var commandSummary = option.Commands.Count == 0
            ? "No commands attached."
            : string.Join(" • ", option.Commands.Select(DescribePromptCommand));
        AddActivity($"Prompt action: {option.Label}", commandSummary);

        foreach (var command in option.Commands)
        {
            ExecutePromptCommand(command);
        }

        if (option.ClosesPrompt)
        {
            _promptCatalog[lane].RemoveAll(prompt => prompt.Id == promptId);
            SyncPromptLaneState();
            SelectPromptLane(_selectedPromptLane, updateActivity: false);
            if (!HasActivePrompts)
            {
                SetPromptOverlayOpen(false);
            }

            AddActivity("Prompt closed.", $"{promptId} was dismissed from the {lane} lane.");
        }
    }

    private void ExecutePromptCommand(LauncherFrontendPromptCommand command)
    {
        switch (command.Kind)
        {
            case LauncherFrontendPromptCommandKind.ViewGameLog:
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog), "Prompt routed the shell to the live game log surface.");
                break;
            case LauncherFrontendPromptCommandKind.OpenInstanceSettings:
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup), "Prompt routed the shell to instance settings.");
                break;
            case LauncherFrontendPromptCommandKind.ExportCrashReport:
                AddActivity("Crash export intent issued.", _crashPlan.ExportPlan.SuggestedArchiveName);
                break;
            case LauncherFrontendPromptCommandKind.DownloadJavaRuntime:
                AddActivity("Java download intent issued.", command.Value ?? _launchPlan.JavaWorkflow.MissingJavaPrompt.DownloadTarget ?? "No download target");
                break;
            case LauncherFrontendPromptCommandKind.OpenUrl:
                AddActivity("External URL intent issued.", command.Value ?? "No URL supplied");
                break;
            case LauncherFrontendPromptCommandKind.AppendLaunchArgument:
                AddActivity("Launch argument intent issued.", command.Value ?? "No argument supplied");
                break;
            case LauncherFrontendPromptCommandKind.SetTelemetryEnabled:
            case LauncherFrontendPromptCommandKind.AcceptConsent:
            case LauncherFrontendPromptCommandKind.RejectConsent:
            case LauncherFrontendPromptCommandKind.ContinueFlow:
            case LauncherFrontendPromptCommandKind.AbortLaunch:
            case LauncherFrontendPromptCommandKind.PersistSetting:
            case LauncherFrontendPromptCommandKind.ClosePrompt:
            case LauncherFrontendPromptCommandKind.ExitLauncher:
                AddActivity("Shell intent recorded.", DescribePromptCommand(command));
                break;
            default:
                AddActivity("Unhandled prompt command encountered.", command.Kind.ToString());
                break;
        }
    }

    private void AddActivity(string title, string body)
    {
        ActivityEntries.Insert(0, new ActivityItemViewModel(DateTime.Now.ToString("HH:mm:ss"), title, body));
        while (ActivityEntries.Count > 12)
        {
            ActivityEntries.RemoveAt(ActivityEntries.Count - 1);
        }

        RaisePropertyChanged(nameof(HasActivityEntries));
    }

    private void TogglePromptOverlay()
    {
        SetPromptOverlayOpen(!IsPromptOverlayVisible);
    }

    private void ToggleLaunchMigrationCard()
    {
        IsLaunchMigrationExpanded = !IsLaunchMigrationExpanded;
    }

    private void ToggleLaunchNewsCard()
    {
        IsLaunchNewsExpanded = !IsLaunchNewsExpanded;
    }

    private void SetPromptOverlayOpen(bool isOpen)
    {
        if (_isPromptOverlayOpen == isOpen)
        {
            RaisePropertyChanged(nameof(IsPromptOverlayVisible));
            return;
        }

        _isPromptOverlayOpen = isOpen;
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
    }

    private static string DescribePromptOption(LauncherFrontendPromptOption option)
    {
        return option.Commands.Count == 0
            ? "No shell commands."
            : string.Join(", ", option.Commands.Select(DescribePromptCommand));
    }

    private static string DescribePromptCommand(LauncherFrontendPromptCommand command)
    {
        return command.Kind switch
        {
            LauncherFrontendPromptCommandKind.ContinueFlow => "Continue flow",
            LauncherFrontendPromptCommandKind.AcceptConsent => "Accept consent",
            LauncherFrontendPromptCommandKind.RejectConsent => "Reject consent",
            LauncherFrontendPromptCommandKind.OpenUrl => $"Open URL ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.ExitLauncher => "Exit launcher",
            LauncherFrontendPromptCommandKind.SetTelemetryEnabled => $"Set telemetry = {command.Value ?? "n/a"}",
            LauncherFrontendPromptCommandKind.AbortLaunch => "Abort launch",
            LauncherFrontendPromptCommandKind.AppendLaunchArgument => $"Append launch arg ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.PersistSetting => $"Persist setting ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.DownloadJavaRuntime => $"Download Java ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.ClosePrompt => "Close prompt",
            LauncherFrontendPromptCommandKind.ViewGameLog => "Open game log",
            LauncherFrontendPromptCommandKind.OpenInstanceSettings => "Open instance settings",
            LauncherFrontendPromptCommandKind.ExportCrashReport => "Export crash report",
            _ => command.Kind.ToString()
        };
    }

    private LauncherFrontendPageContent BuildPageContent(LauncherFrontendShellPlan shellPlan)
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            shellPlan.Navigation,
            shellPlan.StartupPlan,
            shellPlan.Consent,
            BuildPromptLaneSummaries(),
            BuildLaunchSurfaceData(),
            BuildCrashSurfaceData()));

        if (shellPlan.Navigation.CurrentPage.Route.Page != LauncherFrontendPageKey.Launch)
        {
            return content;
        }

        return content with
        {
            Eyebrow = "启动主页",
            Summary = "基于原始启动页结构重建的 Avalonia 主窗口原型。",
            Facts =
            [
                new LauncherFrontendPageFact("账号", LaunchUserName),
                new LauncherFrontendPageFact("验证方式", LaunchAuthLabel),
                new LauncherFrontendPageFact("版本", LaunchVersionSubtitle),
                new LauncherFrontendPageFact("主页", "新闻主页")
            ],
            Sections =
            [
                new LauncherFrontendPageSection(
                    "快照版",
                    "25w20a",
                    [
                        "增加了由 Amos Roddy 创作的新音乐唱片《Tears》。",
                        "鞍具现在可以合成，并且能够用剪刀拆下。",
                        "刷怪蛋与部分实体的视觉表现获得了进一步统一。"
                    ]),
                new LauncherFrontendPageSection(
                    "迁移",
                    "新版主页结构",
                    [
                        "顶部入口、启动区和右侧内容区按原始比例重新收紧。",
                        "卡片标题、箭头、阴影和留白改回接近 PCL 的层级关系。"
                    ])
            ]
        };
    }

    private LauncherFrontendPromptLaneSummary[] BuildPromptLaneSummaries()
    {
        return
        [
            new LauncherFrontendPromptLaneSummary(
                "startup",
                "启动前",
                "许可、环境与首次启动提示。",
                _promptCatalog[SpikePromptLaneKind.Startup].Count,
                _selectedPromptLane == SpikePromptLaneKind.Startup),
            new LauncherFrontendPromptLaneSummary(
                "launch",
                "启动中",
                "启动前检查、赞助与 Java 下载提示。",
                _promptCatalog[SpikePromptLaneKind.Launch].Count,
                _selectedPromptLane == SpikePromptLaneKind.Launch),
            new LauncherFrontendPromptLaneSummary(
                "crash",
                "崩溃恢复",
                "崩溃输出与导出恢复提示。",
                _promptCatalog[SpikePromptLaneKind.Crash].Count,
                _selectedPromptLane == SpikePromptLaneKind.Crash)
        ];
    }

    private LauncherFrontendLaunchSurfaceData BuildLaunchSurfaceData()
    {
        var playerName = _launchPlan.ReplacementPlan.Values.TryGetValue("${auth_player_name}", out var authPlayerName)
            ? authPlayerName
            : "Unknown player";
        var provider = _launchPlan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft
            ? "Microsoft account"
            : "Authlib account";

        return new LauncherFrontendLaunchSurfaceData(
            _launchPlan.Scenario,
            provider,
            playerName,
            _launchPlan.LoginPlan.Steps.Count,
            _launchPlan.JavaWorkflow.RecommendedComponent is null
                ? $"Java {_launchPlan.JavaWorkflow.RecommendedMajorVersion}"
                : $"{_launchPlan.JavaWorkflow.RecommendedComponent} (Java {_launchPlan.JavaWorkflow.RecommendedMajorVersion})",
            _launchPlan.JavaWorkflow.MissingJavaPrompt.DownloadTarget,
            $"{_launchPlan.ResolutionPlan.Width} x {_launchPlan.ResolutionPlan.Height}",
            _launchPlan.ClasspathPlan.Entries.Count,
            _launchPlan.ReplacementPlan.Values.Count,
            _launchPlan.NativesDirectory,
            _launchPlan.PrerunPlan.Options.TargetFilePath,
            _launchPlan.PrerunPlan.LauncherProfiles.Workflow.ShouldWrite,
            _launchPlan.ScriptExportPlan is not null,
            _launchPlan.ScriptExportPlan?.TargetPath,
            _launchPlan.CompletionNotification.Message);
    }

    private LauncherFrontendCrashSurfaceData BuildCrashSurfaceData()
    {
        return new LauncherFrontendCrashSurfaceData(
            _crashPlan.ExportPlan.SuggestedArchiveName,
            _crashPlan.ExportPlan.ExportRequest.SourceFiles.Count,
            !string.IsNullOrWhiteSpace(_crashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath),
            _crashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath);
    }
}
