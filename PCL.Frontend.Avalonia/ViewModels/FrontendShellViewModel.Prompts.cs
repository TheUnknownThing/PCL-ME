using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Tasks;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.OS;
using System.Runtime.InteropServices;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private Dictionary<AvaloniaPromptLaneKind, List<PromptCardViewModel>> BuildPromptCatalog(string scenario)
    {
        var startupPrompts = LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent);

        return new Dictionary<AvaloniaPromptLaneKind, List<PromptCardViewModel>>
        {
            [AvaloniaPromptLaneKind.Startup] = startupPrompts.Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Startup, prompt)).ToList(),
            [AvaloniaPromptLaneKind.Launch] = [],
            [AvaloniaPromptLaneKind.Crash] = []
        };
    }

    private void InitializePromptLanes()
    {
        RebuildPromptLanes();
        SyncPromptLaneState();
        SelectPromptLane(_selectedPromptLane);

        if (_promptCatalog[AvaloniaPromptLaneKind.Startup].Count > 0)
        {
            SetPromptOverlayOpen(true);
        }
    }

    private void SelectPromptLane(AvaloniaPromptLaneKind lane, bool updateActivity = true, bool raiseCollectionState = true)
    {
        if (_promptCatalog[lane].Count == 0)
        {
            var firstAvailableLane = GetFirstAvailablePromptLane();
            if (firstAvailableLane is null)
            {
                _selectedPromptLane = lane;
                ReplaceItems(ActivePrompts, []);
                RaisePropertyChanged(nameof(HasActivePrompts));
                RaisePropertyChanged(nameof(HasNoActivePrompts));
                RaisePropertyChanged(nameof(CurrentPrompt));
                RaisePropertyChanged(nameof(HasCurrentPrompt));
                RaisePromptOverlayPresentationProperties();
                return;
            }

            lane = firstAvailableLane.Value;
        }

        _selectedPromptLane = lane;
        SyncPromptLaneState();
        ReplaceItems(ActivePrompts, _promptCatalog[lane]);
        RaisePropertyChanged(nameof(HasActivePrompts));
        RaisePropertyChanged(nameof(HasNoActivePrompts));
        RaisePropertyChanged(nameof(CurrentPrompt));
        RaisePropertyChanged(nameof(HasCurrentPrompt));
        RaisePromptOverlayPresentationProperties();

        var selectedLane = ResolvePromptLaneViewModel(lane);
        var (laneTitle, laneSummary) = selectedLane is null
            ? GetPromptLaneMetadata(lane)
            : (selectedLane.Title, selectedLane.Summary);
        var laneCount = selectedLane?.Count ?? _promptCatalog[lane].Count;
        PromptInboxTitle = $"{laneTitle}提示";
        PromptInboxSummary = laneSummary;
        PromptEmptyState = $"当前没有待处理的{laneTitle}提示。";
        var pageContent = BuildPageContent(BuildShellPlan());
        ReplaceSurfaceFactsIfChanged(pageContent.Facts);
        ReplaceSurfaceSectionsIfChanged(pageContent.Sections);
        if (raiseCollectionState)
        {
            RaiseCollectionStateProperties();
        }

        if (updateActivity)
        {
            AddActivity("Switched prompt lane.", $"{laneTitle} now has {laneCount} queued prompt(s).");
        }
    }

    private PromptLaneViewModel? ResolvePromptLaneViewModel(AvaloniaPromptLaneKind lane)
    {
        var selectedLane = PromptLanes.FirstOrDefault(item => item.Kind == lane);
        if (selectedLane is not null || _promptCatalog[lane].Count == 0)
        {
            return selectedLane;
        }

        RebuildPromptLanes();
        SyncPromptLaneState();
        return PromptLanes.FirstOrDefault(item => item.Kind == lane);
    }

    private void SyncPromptLaneState()
    {
        foreach (var lane in PromptLanes)
        {
            lane.Count = _promptCatalog[lane.Kind].Count;
            lane.IsSelected = lane.Kind == _selectedPromptLane;
        }
    }

    private void RebuildPromptLanes()
    {
        var visibleLanes = new[]
            {
                AvaloniaPromptLaneKind.Startup,
                AvaloniaPromptLaneKind.Launch,
                AvaloniaPromptLaneKind.Crash
            }
            .Where(kind => _promptCatalog[kind].Count > 0)
            .Select(CreatePromptLane)
            .ToArray();

        ReplaceItems(PromptLanes, visibleLanes);
    }

    private PromptLaneViewModel CreatePromptLane(AvaloniaPromptLaneKind lane)
    {
        var (title, summary) = GetPromptLaneMetadata(lane);
        return new PromptLaneViewModel(
            lane,
            title,
            summary,
            new ActionCommand(() => SelectPromptLane(lane)));
    }

    private static (string Title, string Summary) GetPromptLaneMetadata(AvaloniaPromptLaneKind lane)
    {
        return lane switch
        {
            AvaloniaPromptLaneKind.Startup => ("启动前", "许可、环境与首次启动提示。"),
            AvaloniaPromptLaneKind.Launch => ("启动中", "启动前检查、赞助与 Java 下载提示。"),
            AvaloniaPromptLaneKind.Crash => ("崩溃恢复", "崩溃输出与导出恢复提示。"),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown prompt lane.")
        };
    }

    private AvaloniaPromptLaneKind? GetFirstAvailablePromptLane()
    {
        foreach (var lane in new[] { AvaloniaPromptLaneKind.Startup, AvaloniaPromptLaneKind.Launch, AvaloniaPromptLaneKind.Crash })
        {
            if (_promptCatalog[lane].Count > 0)
            {
                return lane;
            }
        }

        return null;
    }

    private PromptCardViewModel CreatePromptCard(AvaloniaPromptLaneKind lane, LauncherFrontendPrompt prompt)
    {
        return new PromptCardViewModel(
            lane,
            prompt.Id,
            prompt.Title,
            prompt.Message,
            prompt.Source.ToString(),
            prompt.Severity.ToString(),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushRedLight", "#D33232")
                : FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushRedLight", "#D33232")
                : FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticDangerBackground", "#FFF1EA")
                : FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoBackground", "#EDF5FF"),
            prompt.Options.Select((option, index) => new PromptOptionViewModel(
                option.Label,
                string.Empty,
                ResolvePromptOptionColorType(prompt.Severity, index, prompt.Options.Count),
                new ActionCommand(() => _ = ApplyPromptOptionAsync(lane, prompt.Id, option)))).ToList());
    }

    private static PclButtonColorState ResolvePromptOptionColorType(
        LauncherFrontendPromptSeverity severity,
        int index,
        int optionCount)
    {
        if (index != 0)
        {
            return PclButtonColorState.Normal;
        }

        if (severity == LauncherFrontendPromptSeverity.Warning)
        {
            return PclButtonColorState.Red;
        }

        return PclButtonColorState.Highlight;
    }

    private async Task ApplyPromptOptionAsync(AvaloniaPromptLaneKind lane, string promptId, LauncherFrontendPromptOption option)
    {
        var commandSummary = option.Commands.Count == 0
            ? "No commands attached."
            : string.Join(" • ", option.Commands.Select(DescribePromptCommand));
        AddActivity($"Prompt action: {option.Label}", commandSummary);

        if (option.ClosesPrompt && IsPromptOverlayVisible)
        {
            SetPromptOverlayOpen(false);
            await Task.Delay(PclModalMotion.ExitDuration);
        }

        foreach (var command in option.Commands)
        {
            ExecutePromptCommand(lane, command);
        }

        if (option.ClosesPrompt)
        {
            if (lane == AvaloniaPromptLaneKind.Launch)
            {
                _dismissedLaunchPromptIds.Add(promptId);
            }

            _promptCatalog[lane].RemoveAll(prompt => prompt.Id == promptId);
            RebuildPromptLanes();
            SyncPromptLaneState();
            SelectPromptLane(_selectedPromptLane, updateActivity: false);
            if (HasActivePrompts)
            {
                SetPromptOverlayOpen(true);
            }
            else
            {
                SetPromptOverlayOpen(false);
            }

            AddActivity("Prompt closed.", $"{promptId} was dismissed from the {lane} lane.");

            if (lane == AvaloniaPromptLaneKind.Launch &&
                _pendingLaunchAfterPrompt &&
                !_isLaunchBlockedByPrompt &&
                _promptCatalog[AvaloniaPromptLaneKind.Launch].Count == 0)
            {
                _pendingLaunchAfterPrompt = false;
                _ = StartLaunchAsync();
            }
        }
    }

    private void ExecutePromptCommand(
        AvaloniaPromptLaneKind lane,
        LauncherFrontendPromptCommand command)
    {
        switch (command.Kind)
        {
            case LauncherFrontendPromptCommandKind.ViewGameLog:
                OpenCrashLogFromPrompt();
                break;
            case LauncherFrontendPromptCommandKind.OpenInstanceSettings:
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup), "Prompt routed the shell to instance settings.");
                break;
            case LauncherFrontendPromptCommandKind.ExportCrashReport:
                ExportCrashReportFromPrompt();
                break;
            case LauncherFrontendPromptCommandKind.DownloadJavaRuntime:
                _ = DownloadJavaRuntimeFromPromptAsync();
                break;
            case LauncherFrontendPromptCommandKind.OpenUrl:
                OpenExternalTarget(command.Value, "已根据提示打开外部链接。");
                break;
            case LauncherFrontendPromptCommandKind.AppendLaunchArgument:
                AppendPromptLaunchArgument(command.Value);
                break;
            case LauncherFrontendPromptCommandKind.AcceptConsent:
                AcceptPromptConsent();
                break;
            case LauncherFrontendPromptCommandKind.RejectConsent:
                AddActivity("已拒绝协议授权", "当前提示未写入同意状态。");
                break;
            case LauncherFrontendPromptCommandKind.ContinueFlow:
                ContinuePromptFlow(lane);
                break;
            case LauncherFrontendPromptCommandKind.AbortLaunch:
                AbortLaunchFromPrompt();
                break;
            case LauncherFrontendPromptCommandKind.PersistSetting:
                PersistPromptSetting(command.Value);
                break;
            case LauncherFrontendPromptCommandKind.PersistInstanceJavaCompatibilityIgnored:
                PersistJavaCompatibilityOverride();
                break;
            case LauncherFrontendPromptCommandKind.IgnoreJavaCompatibilityOnce:
                IgnoreJavaCompatibilityWarningOnce();
                break;
            case LauncherFrontendPromptCommandKind.ClosePrompt:
                AddActivity("关闭提示", "当前提示已标记为完成。");
                break;
            case LauncherFrontendPromptCommandKind.ExitLauncher:
                AddActivity("退出启动器", "已根据提示请求关闭启动器。");
                _shellActionService.ExitLauncher();
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

    private void AddFailureActivity(string title, string body)
    {
        AddActivity(title, body);
        AvaloniaHintBus.Show(ComposeFailureHintMessage(title, body), AvaloniaHintTheme.Error);
    }

    private static string ComposeFailureHintMessage(string title, string body)
    {
        var normalizedTitle = (title ?? string.Empty).Trim();
        var normalizedBody = (body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            return string.IsNullOrWhiteSpace(normalizedTitle) ? "操作失败。" : normalizedTitle;
        }

        if (string.IsNullOrWhiteSpace(normalizedTitle) ||
            normalizedBody.StartsWith(normalizedTitle, StringComparison.Ordinal))
        {
            return normalizedBody;
        }

        return $"{normalizedTitle}: {normalizedBody}";
    }

    private void TogglePromptOverlay()
    {
        if (HasPromptOverlayInlineDialog)
        {
            return;
        }

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
            NotifyTopLevelNavigationInteractionChanged();
            return;
        }

        _isPromptOverlayOpen = isOpen;
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
        NotifyTopLevelNavigationInteractionChanged();
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
            LauncherFrontendPromptCommandKind.AbortLaunch => "Abort launch",
            LauncherFrontendPromptCommandKind.AppendLaunchArgument => $"Append launch arg ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.PersistSetting => $"Persist setting ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.DownloadJavaRuntime => $"Download Java ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.PersistInstanceJavaCompatibilityIgnored => "Persist Java compatibility override",
            LauncherFrontendPromptCommandKind.IgnoreJavaCompatibilityOnce => "Ignore Java compatibility once",
            LauncherFrontendPromptCommandKind.ClosePrompt => "Close prompt",
            LauncherFrontendPromptCommandKind.ViewGameLog => "Open game log",
            LauncherFrontendPromptCommandKind.OpenInstanceSettings => "Open instance settings",
            LauncherFrontendPromptCommandKind.ExportCrashReport => "Export crash report",
            _ => command.Kind.ToString()
        };
    }

    private async Task HandleLaunchRequestedAsync()
    {
        if (_isLaunchInProgress)
        {
            AddActivity("启动进行中", "当前已经有一个启动会话正在运行。");
            return;
        }

        await AwaitLatestSelectedInstanceRefreshAsync();
        RefreshLaunchState();

        if (_isLaunchBlockedByPrompt)
        {
            AddActivity("启动已被提示中止", "请先重新确认启动前提示或调整当前实例设置。");
            return;
        }

        if (!_launchComposition.PrecheckResult.IsSuccess)
        {
            AddFailureActivity("启动前检查未通过", _launchComposition.PrecheckResult.FailureMessage ?? "当前实例尚未满足启动条件。");
            return;
        }

        _dismissedLaunchPromptIds.Clear();
        EnsureLaunchPromptLane();
        if (_promptCatalog[AvaloniaPromptLaneKind.Launch].Count > 0)
        {
            _pendingLaunchAfterPrompt = true;
            RebuildPromptLanes();
            SetPromptOverlayOpen(true);
            SelectPromptLane(AvaloniaPromptLaneKind.Launch, updateActivity: false);
            AddActivity("启动前提示待处理", $"{LaunchVersionSubtitle} 还有 {_promptCatalog[AvaloniaPromptLaneKind.Launch].Count} 个提示需要确认。");
            return;
        }

        await StartLaunchAsync();
    }

    private void AcceptPromptConsent()
    {
        _shellActionService.AcceptLauncherEula();
        UpdateStartupConsentRequest(request => request with { HasAcceptedEula = true });
        AddActivity("已同意协议授权", "协议授权状态已写入共享配置。");
    }

    private void ContinuePromptFlow(AvaloniaPromptLaneKind lane)
    {
        if (lane == AvaloniaPromptLaneKind.Launch)
        {
            _isLaunchBlockedByPrompt = false;
            AddActivity("继续启动流程", _pendingLaunchAfterPrompt ? "启动前提示已放行，将继续当前启动。" : "启动前提示已放行，启动按钮恢复可用。");
            return;
        }

        AddActivity("继续当前流程", "提示要求的继续操作已完成。");
    }

    private void AbortLaunchFromPrompt()
    {
        _isLaunchBlockedByPrompt = false;
        _ignoreJavaCompatibilityWarningOnce = false;
        _pendingLaunchAfterPrompt = false;
        AddActivity("已中止启动流程", "启动提示要求返回处理，当前启动动作已被阻止。");
    }

    private void PersistPromptSetting(string? rawValue)
    {
        _shellActionService.DisableNonAsciiGamePathWarning();
        var updatedPrecheckRequest = _launchComposition.PrecheckRequest with
        {
            IsNonAsciiPathWarningDisabled = true
        };
        _launchComposition = _launchComposition with
        {
            PrecheckRequest = updatedPrecheckRequest,
            PrecheckResult = MinecraftLaunchPrecheckService.Evaluate(updatedPrecheckRequest)
        };
        RaiseLaunchCompositionProperties();
        AddActivity(
            "已保存提示设置",
            string.IsNullOrWhiteSpace(rawValue)
                ? "已关闭非 ASCII 游戏路径提示。"
                : $"已保存设置: {rawValue}");
    }

    private void PersistJavaCompatibilityOverride()
    {
        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;
        if (string.IsNullOrWhiteSpace(instanceDirectory))
        {
            AddFailureActivity("无法强制启动", "当前没有可写入设置的实例。");
            return;
        }

        _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceJava", true);
        IgnoreInstanceJavaCompatibilityWarning = true;
        RefreshLaunchState();
        AddActivity("已启用 Java 强制启动", "当前实例后续将忽略 Java 兼容性检查，继续使用你手动选择的 Java。");
    }

    private void IgnoreJavaCompatibilityWarningOnce()
    {
        _ignoreJavaCompatibilityWarningOnce = true;
        AddActivity("已为本次启动忽略 Java 检查", "当前启动将继续使用你手动选择的 Java，不会修改实例设置。");
    }

    private void AppendPromptLaunchArgument(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            AddFailureActivity("追加启动参数失败", "提示中未提供可写入的参数。");
            return;
        }

        var currentArguments = LaunchGameArguments.Trim();
        var argumentTokens = currentArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (argumentTokens.Contains(argument, StringComparer.Ordinal))
        {
            AddActivity("启动参数已存在", argument);
            return;
        }

        LaunchGameArguments = string.IsNullOrWhiteSpace(currentArguments)
            ? argument
            : $"{currentArguments} {argument}";
        AddActivity("已追加启动参数", LaunchGameArguments);
    }

    private void OpenCrashLogFromPrompt()
    {
        var logPath = _shellActionService.MaterializeCrashLog(_activeCrashPlan);
        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog), "Prompt routed the shell to the live game log surface.");
        RefreshGameLogSurface();
        AddActivity("查看日志", $"已生成崩溃日志，可在实时日志页点击打开：{logPath}");
    }

    private void ExportCrashReportFromPrompt()
    {
        var exportResult = _shellActionService.ExportCrashReport(_activeCrashPlan);
        OpenExternalTarget(exportResult.ArchivePath, "已导出并打开崩溃报告压缩包。");
        AddActivity("崩溃报告已导出", $"{exportResult.ArchivePath} • {exportResult.ArchivedFileCount} 个文件已归档。");
    }

    private async Task DownloadJavaRuntimeFromPromptAsync()
    {
        if (_launchComposition.JavaRuntimeManifestPlan is null || _launchComposition.JavaRuntimeTransferPlan is null)
        {
            AddFailureActivity("Java 运行时准备失败", "当前启动状态没有可执行的 Java 下载计划。");
            return;
        }

        AddActivity("Java 运行时准备中", "正在后台下载并注册 Java 运行时。");

        try
        {
            var installResult = await Task.Run(() => _shellActionService.MaterializeJavaRuntime(
                _launchComposition.JavaRuntimeManifestPlan,
                _launchComposition.JavaRuntimeTransferPlan));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var installedRuntime = FrontendJavaInventoryService.TryResolveRuntime(
                    _shellActionService.GetJavaExecutablePath(installResult.RuntimeDirectory),
                    isEnabled: true,
                    fallbackDisplayName: $"Java {installResult.VersionName}");
                _launchComposition = _launchComposition with
                {
                    SelectedJavaRuntime = new FrontendJavaRuntimeSummary(
                        installedRuntime?.ExecutablePath ?? _shellActionService.GetJavaExecutablePath(installResult.RuntimeDirectory),
                        installedRuntime?.DisplayName ?? $"Java {installResult.VersionName}",
                        installedRuntime?.MajorVersion ?? _launchComposition.JavaWorkflow.RecommendedMajorVersion,
                        IsEnabled: installedRuntime?.IsEnabled ?? true,
                        Is64Bit: installedRuntime?.Is64Bit ?? Environment.Is64BitOperatingSystem,
                        Architecture: installedRuntime?.Architecture),
                    JavaWarningMessage = null
                };
                RaiseLaunchCompositionProperties();
                RegisterMaterializedJavaRuntime(installResult);
                _isLaunchBlockedByPrompt = false;
                _pendingLaunchAfterPrompt = false;
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupJava), "Prompt routed the shell to Java settings after preparing the runtime.");
                AddActivity(
                    "Java 运行时已准备",
                    $"{installResult.RuntimeDirectory} • 下载 {installResult.DownloadedFileCount} 个文件，复用 {installResult.ReusedFileCount} 个文件。");
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddFailureActivity("Java 运行时准备失败", ex.Message);
            });
        }
    }

    private void RegisterMaterializedJavaRuntime(FrontendJavaRuntimeInstallResult installResult)
    {
        var key = $"downloaded-{Path.GetFileName(installResult.RuntimeDirectory)}";
        var tags = new List<string> { "64 Bit", "Prompt Download" };
        if (installResult.ReusedFileCount > 0)
        {
            tags.Add($"复用 {installResult.ReusedFileCount}");
        }

        var newEntry = CreateJavaRuntimeEntry(
            key,
            $"Java {installResult.VersionName}",
            installResult.RuntimeDirectory,
            tags,
            isEnabled: true);

        var existingIndex = -1;
        for (var index = 0; index < JavaRuntimeEntries.Count; index++)
        {
            if (JavaRuntimeEntries[index].Key == key)
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            JavaRuntimeEntries[existingIndex] = newEntry;
        }
        else
        {
            JavaRuntimeEntries.Add(newEntry);
        }

        RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
        SelectJavaRuntime(key);
    }

    private void OpenExternalTarget(string? target, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            AddFailureActivity("外部打开失败", "缺少可打开的目标。");
            return;
        }

        if (_shellActionService.TryOpenExternalTarget(target, out var error))
        {
            AddActivity("已打开外部目标", $"{successMessage} {target}");
            return;
        }

        AddFailureActivity("外部打开失败", $"{target} • {error ?? "未知错误"}");
    }

    private void UpdateStartupConsentRequest(Func<LauncherStartupConsentRequest, LauncherStartupConsentRequest> updater)
    {
        var updatedRequest = updater(_shellComposition.StartupConsentRequest);
        var updatedConsent = LauncherStartupConsentService.Evaluate(updatedRequest);

        _shellComposition = _shellComposition with
        {
            StartupConsentRequest = updatedRequest,
            StartupConsentResult = updatedConsent
        };
        _startupPlan = _startupPlan with
        {
            Consent = updatedConsent
        };
    }

    private async Task StartLaunchAsync()
    {
        try
        {
            var launchCancellation = new CancellationTokenSource();
            _launchSessionCancellation = launchCancellation;
            ShowLaunchDialog();
            SetLaunchDialogRunningState(
                "正在启动游戏",
                "初始化",
                0d,
                showDownload: false,
                isError: false);

            _isLaunchInProgress = true;
            _showLaunchLog = true;
            ClearLaunchLogBuffer();
            RaiseLaunchSessionProperties();
            RefreshGameLogSurface();

            await EnsureSelectedLaunchProfileReadyForLaunchAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();

            await RefreshLaunchCompositionAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();

            if (_launchComposition.SelectedJavaRuntime is null)
            {
                throw new InvalidOperationException("当前没有可用的 Java 运行时，请先处理启动提示或在设置中选择 Java。");
            }

            if (!string.IsNullOrWhiteSpace(_launchComposition.JavaWarningMessage))
            {
                AppendLaunchLogLine(_launchComposition.JavaWarningMessage);
                AddActivity("Java 兼容性检查已忽略", _launchComposition.JavaWarningMessage);
            }

            foreach (var line in _launchComposition.SessionStartPlan.WatcherWorkflowPlan.StartupSummaryLogLines)
            {
                AppendLaunchLogLine(line);
            }

            SetLaunchDialogRunningState(
                "正在启动游戏",
                DisableInstanceFileValidation || !_instanceComposition.Selection.HasSelection
                    ? "准备启动参数"
                    : "校验实例文件",
                DisableInstanceFileValidation || !_instanceComposition.Selection.HasSelection ? 0.18d : 0.06d,
                showDownload: false,
                isError: false);

            await EnsureLaunchFilesAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();

            SetLaunchDialogRunningState(
                "正在启动游戏",
                "写入启动脚本",
                0.88d,
                showDownload: false,
                isError: false);

            var startResult = _shellActionService.StartLaunchSession(
                _launchComposition,
                _instanceComposition.Selection.InstanceDirectory);
            _activeLaunchProcess = startResult.Process;
            AppendLaunchLogLine(_launchComposition.SessionStartPlan.ProcessShellPlan.StartedLogMessage);
            AddActivity("游戏进程已启动", $"{LaunchVersionSubtitle} • PID {startResult.Process.Id}");
            if (_currentRoute.Page != LauncherFrontendPageKey.Launch)
            {
                NavigateTo(
                    new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                    "游戏启动成功后已返回主页。",
                    RouteNavigationBehavior.Reset);
            }

            AvaloniaHintBus.Show("游戏启动成功！", AvaloniaHintTheme.Success);
            HideLaunchDialog();
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();

            _ = MonitorLaunchSessionAsync(startResult);
        }
        catch (OperationCanceledException)
        {
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();
            AppendLaunchLogLine("启动已取消。");
            AddActivity("启动已取消", "启动前文件校验或准备步骤已取消。");
            SetLaunchDialogStoppedState("已取消启动", "启动前文件校验或准备步骤已取消。", isError: false);
        }
        catch (Exception ex)
        {
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();
            AppendLaunchLogLine($"启动失败：{ex.Message}");
            AddFailureActivity("启动失败", ex.Message);
            SetLaunchDialogStoppedState("启动失败", ex.Message, isError: true);
        }
        finally
        {
            _launchSessionCancellation?.Dispose();
            _launchSessionCancellation = null;
            RaiseLaunchSessionProperties();
        }
    }

    private async Task RefreshLaunchCompositionAsync(CancellationToken cancellationToken)
    {
        SetLaunchDialogRunningState(
            "正在启动游戏",
            "读取启动配置",
            0.02d,
            showDownload: false,
            isError: false);

        var ignoreJavaCompatibilityWarningOnce = _ignoreJavaCompatibilityWarningOnce;
        _ignoreJavaCompatibilityWarningOnce = false;
        var launchComposition = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FrontendLaunchCompositionService.Compose(
                _options,
                _shellActionService.RuntimePaths,
                ignoreJavaCompatibilityWarningOnce);
        }, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        ApplyLaunchComposition(launchComposition, normalizeLaunchProfileSurface: true);
    }

    private async Task EnsureSelectedLaunchProfileReadyForLaunchAsync(CancellationToken cancellationToken)
    {
        var result = await RefreshSelectedLaunchProfileCoreAsync(
            cancellationToken,
            forceRefresh: false,
            onStatusChanged: stage => SetLaunchDialogRunningState(
                "正在启动游戏",
                stage,
                0.03d,
                showDownload: false,
                isError: false));
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.WasChecked)
        {
            return;
        }

        if (result.ShouldInvalidateAvatarCache)
        {
            InvalidateLaunchAvatarCache(_launchComposition.SelectedProfile);
        }

        AppendLaunchLogLine(result.Message);
        AddActivity("启动前账号检查", result.Message);
    }

    private async Task EnsureLaunchFilesAsync(CancellationToken cancellationToken)
    {
        if (!_instanceComposition.Selection.HasSelection || DisableInstanceFileValidation)
        {
            return;
        }

        AppendLaunchLogLine("正在校验并补全实例文件...");
        AppendLaunchLogLine("详细进度已同步到任务管理。");

        try
        {
            var repairResult = await ExecuteManagedInstanceRepairAsync(
                $"启动前校验实例文件：{_instanceComposition.Selection.InstanceName}",
                new FrontendInstanceRepairRequest(
                    _instanceComposition.Selection.LauncherDirectory,
                    _instanceComposition.Selection.InstanceDirectory,
                    _instanceComposition.Selection.InstanceName,
                    ForceCoreRefresh: false),
                ApplyLaunchRepairProgress,
                cancellationToken);
            var completionMessage = $"启动前文件校验完成：下载 {repairResult.DownloadedFiles.Count} 个文件，复用 {repairResult.ReusedFiles.Count} 个文件。";
            AppendLaunchLogLine(completionMessage);

            if (repairResult.DownloadedFiles.Count > 0)
            {
                AddActivity("启动前文件校验", completionMessage);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"启动前文件校验失败：{ex.Message}", ex);
        }
    }

    private async Task MonitorLaunchSessionAsync(FrontendLaunchStartResult startResult)
    {
        try
        {
            startResult.Process.EnableRaisingEvents = true;
            if (startResult.Process.StartInfo.RedirectStandardOutput)
            {
                startResult.Process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        AppendLaunchLogLine(args.Data);
                        File.AppendAllText(startResult.RawOutputLogPath, args.Data + Environment.NewLine, new System.Text.UTF8Encoding(false));
                    }
                };
            }

            if (startResult.Process.StartInfo.RedirectStandardError)
            {
                startResult.Process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        AppendLaunchLogLine(args.Data);
                        File.AppendAllText(startResult.RawOutputLogPath, args.Data + Environment.NewLine, new System.Text.UTF8Encoding(false));
                    }
                };
            }

            if (startResult.Process.StartInfo.RedirectStandardOutput)
            {
                startResult.Process.BeginOutputReadLine();
            }

            if (startResult.Process.StartInfo.RedirectStandardError)
            {
                startResult.Process.BeginErrorReadLine();
            }

            await startResult.Process.WaitForExitAsync();
            _shellActionService.ApplyWatcherStopShellPlan(_launchComposition);
            AppendLaunchLogLine($"游戏进程已退出，退出码 {startResult.Process.ExitCode}。");
            AddActivity("游戏进程已结束", $"{LaunchVersionSubtitle} • ExitCode {startResult.Process.ExitCode}");
            if (startResult.Process.ExitCode != 0 && !_launchProcessTerminationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ShowCrashPromptForLaunchFailure(startResult));
            }
        }
        catch (Exception ex)
        {
            AppendLaunchLogLine($"会话监控异常：{ex.Message}");
            AddActivity("会话监控异常", ex.Message);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _activeLaunchProcess = null;
                _launchProcessTerminationRequested = false;
                _isLaunchInProgress = false;
                RaiseLaunchSessionProperties();
                RefreshLaunchState();
                RefreshGameLogSurface();
                if (IsLaunchDialogVisible)
                {
                    HideLaunchDialog();
                }
            });
        }
    }

    private void RefreshLaunchState()
    {
        ReloadSetupComposition();
        ReloadInstanceComposition();
        ApplyLaunchComposition(
            FrontendLaunchCompositionService.Compose(_options, _shellActionService.RuntimePaths),
            normalizeLaunchProfileSurface: true);
    }

    private async Task RefreshLaunchProfileCompositionAsync()
    {
        var refreshVersion = Interlocked.Increment(ref _launchProfileCompositionRefreshVersion);
        var launchComposition = await Task.Run(() =>
            FrontendLaunchCompositionService.Compose(_options, _shellActionService.RuntimePaths));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _launchProfileCompositionRefreshVersion)
            {
                return;
            }

            ApplyLaunchComposition(launchComposition, normalizeLaunchProfileSurface: true);
        });
    }

    private void ApplyLaunchComposition(FrontendLaunchComposition composition, bool normalizeLaunchProfileSurface)
    {
        _launchComposition = composition;
        ClearOptimisticLaunchInstanceName(raiseProperties: false);
        if (normalizeLaunchProfileSurface)
        {
            NormalizeLaunchProfileSurface();
        }

        var launchPromptContextKey = BuildLaunchPromptContextKey(_launchComposition, _instanceComposition.Selection.InstanceDirectory);
        if (!string.Equals(_launchPromptContextKey, launchPromptContextKey, StringComparison.Ordinal))
        {
            _dismissedLaunchPromptIds.Clear();
            _launchPromptContextKey = launchPromptContextKey;
        }

        RaiseLaunchCompositionProperties();
        ScheduleLaunchAvatarRefresh();
    }

    private void AppendLaunchLogLine(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AppendLaunchLogEntry(line);
            RaiseGameLogSurfaceProperties();
            if (!_showLaunchLog)
            {
                _showLaunchLog = true;
                RaisePropertyChanged(nameof(ShowLaunchLog));
            }
        });
    }

    private void ShowLaunchCompletionNotification()
    {
        if (string.IsNullOrWhiteSpace(_launchComposition.CompletionNotification.Message))
        {
            return;
        }

        switch (_launchComposition.CompletionNotification.Kind)
        {
            case MinecraftLaunchNotificationKind.Info:
                AvaloniaHintBus.Show(_launchComposition.CompletionNotification.Message, AvaloniaHintTheme.Info);
                break;
            case MinecraftLaunchNotificationKind.Finish:
                AvaloniaHintBus.Show(_launchComposition.CompletionNotification.Message, AvaloniaHintTheme.Success);
                break;
        }
    }

    private void RaiseLaunchSessionProperties()
    {
        RaisePropertyChanged(nameof(LaunchButtonTitle));
        RaisePropertyChanged(nameof(ShowLaunchLog));
        RaisePropertyChanged(nameof(LaunchLogText));
        RaisePropertyChanged(nameof(LaunchMigrationLines));
        _launchCommand.NotifyCanExecuteChanged();
        _cancelLaunchCommand.NotifyCanExecuteChanged();
        RaiseLaunchDialogProperties();
    }

    private void EnsureLaunchPromptLane()
    {
        var launchPrompts = LauncherFrontendPromptService.BuildLaunchPromptQueue(
            _launchComposition.PrecheckResult,
            _launchComposition.SupportPrompt,
            _launchComposition.JavaCompatibilityPrompt,
            GetPendingJavaPrompt());
        _promptCatalog[AvaloniaPromptLaneKind.Launch] = launchPrompts
            .Where(prompt => !_dismissedLaunchPromptIds.Contains(prompt.Id))
            .Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Launch, prompt))
            .ToList();
    }

    private static string BuildLaunchPromptContextKey(
        FrontendLaunchComposition launchComposition,
        string? instanceDirectory)
    {
        return string.Join(
            "|",
            instanceDirectory ?? string.Empty,
            launchComposition.InstanceName,
            launchComposition.SelectedProfile.Kind,
            launchComposition.SelectedProfile.UserName);
    }

    private void EnsureCrashPromptLane()
    {
        var crashPrompts = LauncherFrontendPromptService.BuildCrashPromptQueue(_activeCrashPlan.OutputPrompt);
        _promptCatalog[AvaloniaPromptLaneKind.Crash] = crashPrompts
            .Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Crash, prompt))
            .ToList();
    }

    private void ShowCrashPromptForLaunchFailure(FrontendLaunchStartResult startResult)
    {
        _activeCrashPlan = BuildCrashPlanForLaunchFailure(startResult);
        EnsureCrashPromptLane();
        RebuildPromptLanes();
        SetPromptOverlayOpen(true);
        SelectPromptLane(AvaloniaPromptLaneKind.Crash, updateActivity: false);
        AddActivity("Minecraft 出现错误", "已弹出崩溃恢复提示，可直接查看日志或导出错误报告。");
    }

    private CrashAvaloniaPlan BuildCrashPlanForLaunchFailure(FrontendLaunchStartResult startResult)
    {
        var exportRequest = new MinecraftCrashExportPlanRequest(
            Timestamp: DateTime.Now,
            ReportDirectory: Path.Combine(
                _shellActionService.RuntimePaths.FrontendTempDirectory,
                "CrashReport",
                DateTime.Now.ToString("yyyy-MM-dd")),
            LauncherVersionName: "frontend-avalonia",
            UniqueAddress: _instanceComposition.Selection.InstanceDirectory ??
                           _launchComposition.InstancePath,
            SourceFilePaths:
            [
                startResult.LaunchScriptPath,
                startResult.RawOutputLogPath
            ],
            AdditionalSourceFilePaths:
            [
                startResult.SessionSummaryPath,
                Path.Combine(_launchComposition.InstancePath, "logs", "latest.log")
            ],
            CurrentLauncherLogFilePath: _shellActionService.RuntimePaths.ResolveCurrentLauncherLogFilePath(),
            Environment: GetHostEnvironmentSnapshot(),
            CurrentAccessToken: _launchComposition.SelectedProfile.AccessToken,
            CurrentUserUuid: _launchComposition.SelectedProfile.Uuid,
            UserProfilePath: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var analysisResult = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
            BuildAnalysisSourcePaths(exportRequest),
            exportRequest.CurrentLauncherLogFilePath));
        var resultText = analysisResult.HasKnownReason
            ? analysisResult.ResultText
            : $"{analysisResult.ResultText}{Environment.NewLine}{Environment.NewLine}详细信息：{Environment.NewLine}{BuildLaunchFailureMessage(startResult)}";
        var outputPrompt = MinecraftCrashWorkflowService.BuildOutputPrompt(new MinecraftCrashOutputPromptRequest(
            resultText,
            IsManualAnalysis: false,
            HasDirectFile: analysisResult.HasDirectFile,
            CanOpenModLoaderSettings: true));
        var exportPlan = MinecraftCrashExportWorkflowService.CreatePlan(exportRequest);

        return new CrashAvaloniaPlan(outputPrompt, exportPlan);
    }

    private static IReadOnlyList<string> BuildAnalysisSourcePaths(MinecraftCrashExportPlanRequest request)
    {
        return request.SourceFilePaths
            .Concat(request.AdditionalSourceFilePaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildLaunchFailureMessage(FrontendLaunchStartResult startResult)
    {
        var details = new List<string>
        {
            $"游戏进程异常退出，退出码 {startResult.Process.ExitCode}。"
        };

        var lastOutputLine = TryReadLastMeaningfulLine(startResult.RawOutputLogPath);
        if (!string.IsNullOrWhiteSpace(lastOutputLine))
        {
            details.Add(lastOutputLine);
        }

        details.Add($"原始输出日志：{startResult.RawOutputLogPath}");
        return string.Join(Environment.NewLine, details);
    }

    private static string? TryReadLastMeaningfulLine(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadLines(path)
                .Reverse()
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return null;
        }
    }

    private static SystemEnvironmentSnapshot GetHostEnvironmentSnapshot()
    {
        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var totalPhysicalMemoryBytes = availableMemory > 0
            ? (ulong)availableMemory
            : 8UL * 1024UL * 1024UL * 1024UL;

        return new SystemEnvironmentSnapshot(
            RuntimeInformation.OSDescription,
            Environment.OSVersion.Version,
            RuntimeInformation.OSArchitecture,
            Environment.Is64BitOperatingSystem,
            totalPhysicalMemoryBytes,
            GetHostCpuName(),
            []);
    }

    private static string GetHostCpuName()
    {
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
               Environment.GetEnvironmentVariable("HOSTTYPE") ??
               RuntimeInformation.ProcessArchitecture.ToString();
    }

    private void TriggerCrashPromptTest()
    {
        EnsureCrashPromptLane();
        RebuildPromptLanes();
        SetPromptOverlayOpen(true);
        SelectPromptLane(AvaloniaPromptLaneKind.Crash, updateActivity: false);
        AddActivity("崩溃测试已触发", "崩溃恢复提示现已加入提示队列。");
    }

    private LauncherFrontendPageContent BuildPageContent(LauncherFrontendShellPlan shellPlan)
    {
        return LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            shellPlan.Navigation,
            shellPlan.StartupPlan,
            shellPlan.Consent,
            BuildPromptLaneSummaries(),
            BuildLaunchSurfaceData(),
            BuildCrashSurfaceData()));
    }

    private LauncherFrontendPromptLaneSummary[] BuildPromptLaneSummaries()
    {
        return PromptLanes
            .Select(lane => new LauncherFrontendPromptLaneSummary(
                lane.Kind.ToString().ToLowerInvariant(),
                lane.Title,
                lane.Summary,
                lane.Count,
                lane.IsSelected))
            .ToArray();
    }

    private LauncherFrontendLaunchSurfaceData BuildLaunchSurfaceData()
    {
        return new LauncherFrontendLaunchSurfaceData(
            _launchComposition.Scenario,
            LaunchAuthLabel,
            _launchComposition.SelectedProfile.IdentityLabel,
            _launchComposition.SelectedProfile.Kind == MinecraftLaunchProfileKind.None ? 0 : 1,
            GetLaunchJavaRuntimeLabel(),
            _launchComposition.JavaWarningMessage,
            GetPendingJavaPrompt()?.DownloadTarget,
            $"{_launchComposition.ResolutionPlan.Width} x {_launchComposition.ResolutionPlan.Height}",
            _launchComposition.ClasspathPlan.Entries.Count,
            _launchComposition.ReplacementPlan.Values.Count,
            _launchComposition.NativesDirectory,
            _launchComposition.PrerunPlan.Options.TargetFilePath,
            _launchComposition.PrerunPlan.LauncherProfiles.Workflow.ShouldWrite,
            false,
            null,
            _launchComposition.CompletionNotification.Message);
    }

    private string GetLaunchJavaRuntimeLabel()
    {
        if (_launchComposition.SelectedJavaRuntime is not null)
        {
            return _launchComposition.SelectedJavaRuntime.DisplayName;
        }

        return _launchComposition.JavaWorkflow.RecommendedComponent is null
            ? $"Java {_launchComposition.JavaWorkflow.RecommendedMajorVersion}"
            : $"{_launchComposition.JavaWorkflow.RecommendedComponent} (Java {_launchComposition.JavaWorkflow.RecommendedMajorVersion})";
    }

    private MinecraftLaunchJavaPrompt? GetPendingJavaPrompt()
    {
        return _launchComposition.SelectedJavaRuntime is null
            ? _launchComposition.JavaWorkflow.MissingJavaPrompt
            : null;
    }

    private void RaiseLaunchCompositionProperties()
    {
        RaisePropertyChanged(nameof(LaunchAvatarImage));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(CanRefreshLaunchProfile));
        RaisePropertyChanged(nameof(HasSelectedLaunchProfile));
        RaisePropertyChanged(nameof(ShowLaunchProfileSetupActions));
        RaisePropertyChanged(nameof(LaunchProfileHint));
        RaisePropertyChanged(nameof(LaunchProfileDescription));
        RaiseLaunchProfileSurfaceProperties();
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(LaunchWelcomeBanner));
        RaisePropertyChanged(nameof(LaunchNewsTitle));
        RaisePropertyChanged(nameof(LaunchNewsBadgeText));
        RaisePropertyChanged(nameof(LaunchNewsSectionTitle));
        RaisePropertyChanged(nameof(LaunchAnnouncementHeader));
        RaisePropertyChanged(nameof(LaunchAnnouncementPrimaryText));
        RaisePropertyChanged(nameof(LaunchAnnouncementSecondaryText));
        RaisePropertyChanged(nameof(ShowLaunchAnnouncement));
        RaisePropertyChanged(nameof(LaunchMigrationLines));
        _refreshLaunchProfileCommand.NotifyCanExecuteChanged();
    }

    private LauncherFrontendCrashSurfaceData BuildCrashSurfaceData()
    {
        return new LauncherFrontendCrashSurfaceData(
            _activeCrashPlan.ExportPlan.SuggestedArchiveName,
            _activeCrashPlan.ExportPlan.ExportRequest.SourceFiles.Count,
            !string.IsNullOrWhiteSpace(_activeCrashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath),
            _activeCrashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath);
    }
}
