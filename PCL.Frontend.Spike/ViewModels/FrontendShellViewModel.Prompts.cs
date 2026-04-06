using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Workflows;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>> BuildPromptCatalog(string scenario)
    {
        var startupPrompts = LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent);

        return new Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>>
        {
            [SpikePromptLaneKind.Startup] = startupPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Startup, prompt)).ToList(),
            [SpikePromptLaneKind.Launch] = [],
            [SpikePromptLaneKind.Crash] = []
        };
    }

    private void InitializePromptLanes()
    {
        RebuildPromptLanes();
        SyncPromptLaneState();
        SelectPromptLane(_selectedPromptLane);

        if (_promptCatalog[SpikePromptLaneKind.Startup].Count > 0)
        {
            SetPromptOverlayOpen(true);
        }
    }

    private void SelectPromptLane(SpikePromptLaneKind lane, bool updateActivity = true, bool raiseCollectionState = true)
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
                RaisePropertyChanged(nameof(IsPromptOverlayVisible));
                return;
            }

            lane = firstAvailableLane.Value;
        }

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
        ReplaceSurfaceFactsIfChanged(pageContent.Facts);
        ReplaceSurfaceSectionsIfChanged(pageContent.Sections);
        if (raiseCollectionState)
        {
            RaiseCollectionStateProperties();
        }

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

    private void RebuildPromptLanes()
    {
        var visibleLanes = new[]
            {
                SpikePromptLaneKind.Startup,
                SpikePromptLaneKind.Launch,
                SpikePromptLaneKind.Crash
            }
            .Where(kind => _promptCatalog[kind].Count > 0)
            .Select(CreatePromptLane)
            .ToArray();

        ReplaceItems(PromptLanes, visibleLanes);
    }

    private PromptLaneViewModel CreatePromptLane(SpikePromptLaneKind lane)
    {
        var (title, summary) = GetPromptLaneMetadata(lane);
        return new PromptLaneViewModel(
            lane,
            title,
            summary,
            new ActionCommand(() => SelectPromptLane(lane)));
    }

    private static (string Title, string Summary) GetPromptLaneMetadata(SpikePromptLaneKind lane)
    {
        return lane switch
        {
            SpikePromptLaneKind.Startup => ("启动前", "许可、环境与首次启动提示。"),
            SpikePromptLaneKind.Launch => ("启动中", "启动前检查、赞助与 Java 下载提示。"),
            SpikePromptLaneKind.Crash => ("崩溃恢复", "崩溃输出与导出恢复提示。"),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown prompt lane.")
        };
    }

    private SpikePromptLaneKind? GetFirstAvailablePromptLane()
    {
        foreach (var lane in new[] { SpikePromptLaneKind.Startup, SpikePromptLaneKind.Launch, SpikePromptLaneKind.Crash })
        {
            if (_promptCatalog[lane].Count > 0)
            {
                return lane;
            }
        }

        return null;
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
            ExecutePromptCommand(lane, command);
        }

        if (option.ClosesPrompt)
        {
            if (lane == SpikePromptLaneKind.Launch)
            {
                _dismissedLaunchPromptIds.Add(promptId);
            }

            _promptCatalog[lane].RemoveAll(prompt => prompt.Id == promptId);
            RebuildPromptLanes();
            SyncPromptLaneState();
            SelectPromptLane(_selectedPromptLane, updateActivity: false);
            if (!HasActivePrompts)
            {
                SetPromptOverlayOpen(false);
            }

            AddActivity("Prompt closed.", $"{promptId} was dismissed from the {lane} lane.");

            if (lane == SpikePromptLaneKind.Launch &&
                _pendingLaunchAfterPrompt &&
                !_isLaunchBlockedByPrompt &&
                _promptCatalog[SpikePromptLaneKind.Launch].Count == 0)
            {
                _pendingLaunchAfterPrompt = false;
                _ = StartLaunchAsync();
            }
        }
    }

    private void ExecutePromptCommand(
        SpikePromptLaneKind lane,
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
            case LauncherFrontendPromptCommandKind.SetTelemetryEnabled:
                SetTelemetryPreference(command.Value);
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
            case LauncherFrontendPromptCommandKind.ClosePrompt:
                AddActivity("关闭提示", "当前提示已标记为完成。");
                break;
            case LauncherFrontendPromptCommandKind.ExitLauncher:
                AddActivity("退出启动器", "已根据提示请求关闭前端壳层。");
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

    private async Task HandleLaunchRequestedAsync()
    {
        RefreshLaunchState();

        if (_isLaunchInProgress)
        {
            AddActivity("启动进行中", "当前已经有一个前端启动会话正在运行。");
            return;
        }

        if (_isLaunchBlockedByPrompt)
        {
            AddActivity("启动已被提示中止", "请先重新确认启动前提示或调整当前实例设置。");
            return;
        }

        if (!_launchComposition.PrecheckResult.IsSuccess)
        {
            AddActivity("启动前检查未通过", _launchComposition.PrecheckResult.FailureMessage ?? "当前实例尚未满足启动条件。");
            return;
        }

        EnsureLaunchPromptLane();
        if (_promptCatalog[SpikePromptLaneKind.Launch].Count > 0)
        {
            _pendingLaunchAfterPrompt = true;
            RebuildPromptLanes();
            SetPromptOverlayOpen(true);
            SelectPromptLane(SpikePromptLaneKind.Launch, updateActivity: false);
            AddActivity("启动前提示待处理", $"{LaunchVersionSubtitle} 还有 {_promptCatalog[SpikePromptLaneKind.Launch].Count} 个提示需要确认。");
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

    private void SetTelemetryPreference(string? rawValue)
    {
        if (!bool.TryParse(rawValue, out var enabled))
        {
            AddActivity("遥测设置失败", rawValue ?? "缺少遥测布尔值。");
            return;
        }

        _shellActionService.SetTelemetryEnabled(enabled);
        EnableTelemetry = enabled;
        UpdateStartupConsentRequest(request => request with { IsTelemetryDefault = false });
        RaisePropertyChanged(nameof(EnableTelemetry));
        AddActivity("已更新遥测设置", enabled ? "已启用遥测数据收集。" : "已禁用遥测数据收集。");
    }

    private void ContinuePromptFlow(SpikePromptLaneKind lane)
    {
        if (lane == SpikePromptLaneKind.Launch)
        {
            _isLaunchBlockedByPrompt = false;
            AddActivity("继续启动流程", _pendingLaunchAfterPrompt ? "启动前提示已放行，前端将继续当前启动。" : "启动前提示已放行，启动按钮恢复可用。");
            return;
        }

        AddActivity("继续当前流程", "提示要求的继续操作已完成。");
    }

    private void AbortLaunchFromPrompt()
    {
        _isLaunchBlockedByPrompt = true;
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

    private void AppendPromptLaunchArgument(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            AddActivity("追加启动参数失败", "提示中未提供可写入的参数。");
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
        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog), "Prompt routed the shell to the live game log surface.");

        var logPath = _shellActionService.MaterializeCrashLog(_crashPlan);
        OpenExternalTarget(logPath, "已生成并打开崩溃日志副本。");
    }

    private void ExportCrashReportFromPrompt()
    {
        var exportResult = _shellActionService.ExportCrashReport(_crashPlan);
        OpenExternalTarget(exportResult.ArchivePath, "已导出并打开崩溃报告压缩包。");
        AddActivity("崩溃报告已导出", $"{exportResult.ArchivePath} • {exportResult.ArchivedFileCount} 个文件已归档。");
    }

    private async Task DownloadJavaRuntimeFromPromptAsync()
    {
        if (_launchComposition.JavaRuntimeManifestPlan is null || _launchComposition.JavaRuntimeTransferPlan is null)
        {
            AddActivity("Java 运行时准备失败", "当前启动状态没有可执行的 Java 下载计划。");
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
                _launchComposition = _launchComposition with
                {
                    SelectedJavaRuntime = new FrontendJavaRuntimeSummary(
                        _shellActionService.GetJavaExecutablePath(installResult.RuntimeDirectory),
                        $"Java {installResult.VersionName}",
                        _launchComposition.JavaWorkflow.RecommendedMajorVersion,
                        IsEnabled: true,
                        Is64Bit: Environment.Is64BitOperatingSystem)
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
                AddActivity("Java 运行时准备失败", ex.Message);
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
            AddActivity("外部打开失败", "缺少可打开的目标。");
            return;
        }

        if (_shellActionService.TryOpenExternalTarget(target, out var error))
        {
            AddActivity("已打开外部目标", $"{successMessage} {target}");
            return;
        }

        AddActivity("外部打开失败", $"{target} • {error ?? "未知错误"}");
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
        RefreshLaunchState();

        if (_launchComposition.SelectedJavaRuntime is null)
        {
            AddActivity("启动失败", "当前没有可用的 Java 运行时，请先处理启动提示或在设置中选择 Java。");
            return;
        }

        try
        {
            _isLaunchInProgress = true;
            _showLaunchLog = true;
            _launchLogBuilder.Clear();
            RaiseLaunchSessionProperties();
            RefreshGameLogSurface();

            foreach (var line in _launchComposition.SessionStartPlan.WatcherWorkflowPlan.StartupSummaryLogLines)
            {
                AppendLaunchLogLine(line);
            }

            var startResult = _shellActionService.StartLaunchSession(
                _launchComposition,
                _instanceComposition.Selection.InstanceDirectory);
            AppendLaunchLogLine(_launchComposition.SessionStartPlan.ProcessShellPlan.StartedLogMessage);
            AddActivity("游戏进程已启动", $"{LaunchVersionSubtitle} • PID {startResult.Process.Id}");

            _ = MonitorLaunchSessionAsync(startResult);
        }
        catch (Exception ex)
        {
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();
            AppendLaunchLogLine($"启动失败：{ex.Message}");
            AddActivity("启动失败", ex.Message);
        }
    }

    private async Task MonitorLaunchSessionAsync(FrontendLaunchStartResult startResult)
    {
        try
        {
            startResult.Process.EnableRaisingEvents = true;
            startResult.Process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    AppendLaunchLogLine(args.Data);
                    File.AppendAllText(startResult.RawOutputLogPath, args.Data + Environment.NewLine, new System.Text.UTF8Encoding(false));
                }
            };
            startResult.Process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    AppendLaunchLogLine(args.Data);
                    File.AppendAllText(startResult.RawOutputLogPath, args.Data + Environment.NewLine, new System.Text.UTF8Encoding(false));
                }
            };
            startResult.Process.BeginOutputReadLine();
            startResult.Process.BeginErrorReadLine();

            await startResult.Process.WaitForExitAsync();
            _shellActionService.ApplyWatcherStopShellPlan(_launchComposition);
            AppendLaunchLogLine($"游戏进程已退出，退出码 {startResult.Process.ExitCode}。");
            AddActivity("游戏进程已结束", $"{LaunchVersionSubtitle} • ExitCode {startResult.Process.ExitCode}");
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
                _isLaunchInProgress = false;
                RaiseLaunchSessionProperties();
                RefreshLaunchState();
                RefreshGameLogSurface();
            });
        }
    }

    private void RefreshLaunchState()
    {
        ReloadSetupComposition();
        ReloadInstanceComposition();
        _launchComposition = FrontendLaunchCompositionService.Compose(_options, _shellActionService.RuntimePaths);
        var launchPromptContextKey = BuildLaunchPromptContextKey(_launchComposition, _instanceComposition.Selection.InstanceDirectory);
        if (!string.Equals(_launchPromptContextKey, launchPromptContextKey, StringComparison.Ordinal))
        {
            _dismissedLaunchPromptIds.Clear();
            _launchPromptContextKey = launchPromptContextKey;
        }

        RaiseLaunchCompositionProperties();
    }

    private void AppendLaunchLogLine(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_launchLogBuilder.Length > 0)
            {
                _launchLogBuilder.AppendLine();
            }

            _launchLogBuilder.Append(line);
            RaisePropertyChanged(nameof(LaunchLogText));
            RaiseGameLogSurfaceProperties();
            if (!_showLaunchLog)
            {
                _showLaunchLog = true;
                RaisePropertyChanged(nameof(ShowLaunchLog));
            }
        });
    }

    private void RaiseLaunchSessionProperties()
    {
        RaisePropertyChanged(nameof(LaunchButtonTitle));
        RaisePropertyChanged(nameof(ShowLaunchLog));
        RaisePropertyChanged(nameof(LaunchLogText));
        RaisePropertyChanged(nameof(LaunchMigrationLines));
        _launchCommand.NotifyCanExecuteChanged();
    }

    private void EnsureLaunchPromptLane()
    {
        var launchPrompts = LauncherFrontendPromptService.BuildLaunchPromptQueue(
            _launchComposition.PrecheckResult,
            _launchComposition.SupportPrompt,
            GetPendingJavaPrompt());
        _promptCatalog[SpikePromptLaneKind.Launch] = launchPrompts
            .Where(prompt => !_dismissedLaunchPromptIds.Contains(prompt.Id))
            .Select(prompt => CreatePromptCard(SpikePromptLaneKind.Launch, prompt))
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
        var crashPrompts = LauncherFrontendPromptService.BuildCrashPromptQueue(_crashPlan.OutputPrompt);
        _promptCatalog[SpikePromptLaneKind.Crash] = crashPrompts
            .Select(prompt => CreatePromptCard(SpikePromptLaneKind.Crash, prompt))
            .ToList();
    }

    private void TriggerCrashPromptTest()
    {
        EnsureCrashPromptLane();
        RebuildPromptLanes();
        SetPromptOverlayOpen(true);
        SelectPromptLane(SpikePromptLaneKind.Crash, updateActivity: false);
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
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(LaunchWelcomeBanner));
        RaisePropertyChanged(nameof(LaunchNewsTitle));
        RaisePropertyChanged(nameof(LaunchNewsBadgeText));
        RaisePropertyChanged(nameof(LaunchNewsSectionTitle));
        RaisePropertyChanged(nameof(LaunchMigrationLines));
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
