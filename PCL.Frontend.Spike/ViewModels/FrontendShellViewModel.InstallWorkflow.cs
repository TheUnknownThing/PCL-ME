using System.Threading;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Spike.Desktop.Dialogs;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string[] ManagedPrimaryInstallTitles = ["Forge", "Cleanroom", "NeoForge", "Fabric", "Legacy Fabric", "Quilt", "LabyMod"];
    private static readonly string[] ManagedAddonInstallTitles = ["LiteLoader", "OptiFine", "Fabric API", "Legacy Fabric API", "QFAPI / QSL", "OptiFabric"];
    private static readonly string[] ManagedApiInstallTitles = ["Fabric API", "Legacy Fabric API", "QFAPI / QSL"];
    private static readonly string[] ManagedPrimaryDependentInstallTitles = ["Fabric API", "Legacy Fabric API", "QFAPI / QSL", "OptiFabric"];

    private string _downloadInstallMinecraftVersion = "Minecraft";
    private Bitmap? _downloadInstallMinecraftIcon;
    private readonly Dictionary<string, FrontendEditableInstallSelection> _downloadInstallSelections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _downloadInstallBaselineSelections = new(StringComparer.Ordinal);
    private FrontendInstallChoice? _downloadInstallMinecraftChoice;
    private string _downloadInstallBaselineMinecraftVersion = "Minecraft";
    private string _downloadInstallSeedSignature = string.Empty;

    private readonly Dictionary<string, FrontendEditableInstallSelection> _instanceInstallSelections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _instanceInstallBaselineSelections = new(StringComparer.Ordinal);
    private FrontendInstallChoice? _instanceInstallMinecraftChoice;
    private string _instanceInstallBaselineMinecraftVersion = "Minecraft";
    private string _instanceInstallSeedSignature = string.Empty;

    public string DownloadInstallMinecraftVersion
    {
        get => _downloadInstallMinecraftVersion;
        private set => SetProperty(ref _downloadInstallMinecraftVersion, value);
    }

    public Bitmap? DownloadInstallMinecraftIcon
    {
        get => _downloadInstallMinecraftIcon;
        private set => SetProperty(ref _downloadInstallMinecraftIcon, value);
    }

    public ActionCommand EditDownloadInstallMinecraftCommand => new(() => _ = EditInstallMinecraftAsync(isExistingInstance: false));

    public ActionCommand StartDownloadInstallCommand => new(() => _ = StartDownloadInstallAsync());

    public ActionCommand ApplyInstanceInstallCommand => new(() => _ = ApplyInstanceInstallAsync());

    public string InstanceInstallApplyButtonText => HasInstanceInstallChanges ? "开始修改" : "开始重置";

    public bool HasInstanceInstallChanges => ComputeHasInstanceInstallChanges();

    private void EnsureDownloadInstallEditableState()
    {
        if (_downloadInstallIsInSelectionStage)
        {
            return;
        }

        var installState = _downloadComposition.Install;
        var signature = string.Join(
            "|",
            installState.MinecraftVersion,
            installState.Options.Select(option => $"{option.Title}:{option.Selection}"));
        if (string.Equals(signature, _downloadInstallSeedSignature, StringComparison.Ordinal))
        {
            return;
        }

        _downloadInstallSeedSignature = signature;
        _downloadInstallSelections.Clear();
        _downloadInstallBaselineSelections.Clear();
        foreach (var option in installState.Options)
        {
            _downloadInstallSelections[option.Title] = FrontendEditableInstallSelection.Unchanged;
            _downloadInstallBaselineSelections[option.Title] = option.Selection;
        }

        _downloadInstallBaselineMinecraftVersion = installState.MinecraftVersion;
        _downloadInstallMinecraftChoice = null;
        DownloadInstallMinecraftVersion = installState.MinecraftVersion;
        DownloadInstallMinecraftIcon = string.IsNullOrWhiteSpace(installState.MinecraftIconName)
            ? null
            : LoadLauncherBitmap("Images", "Blocks", installState.MinecraftIconName);
    }

    private void EnsureInstanceInstallEditableState()
    {
        var installState = _instanceComposition.Install;
        var signature = string.Join(
            "|",
            _instanceComposition.Selection.InstanceName,
            installState.MinecraftVersion,
            installState.Options.Select(option => $"{option.Title}:{option.Selection}"));
        if (string.Equals(signature, _instanceInstallSeedSignature, StringComparison.Ordinal))
        {
            return;
        }

        _instanceInstallSeedSignature = signature;
        _instanceInstallSelections.Clear();
        _instanceInstallBaselineSelections.Clear();
        foreach (var option in installState.Options)
        {
            _instanceInstallSelections[option.Title] = FrontendEditableInstallSelection.Unchanged;
            _instanceInstallBaselineSelections[option.Title] = option.Selection;
        }

        _instanceInstallBaselineMinecraftVersion = installState.MinecraftVersion;
        _instanceInstallMinecraftChoice = null;
    }

    private async Task EditInstallMinecraftAsync(bool isExistingInstance)
    {
        try
        {
            var currentVersion = GetEffectiveMinecraftVersion(isExistingInstance);
            var currentVersionId = currentVersion.Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
            var choices = FrontendInstallWorkflowService.GetMinecraftChoices(currentVersion);
            var selectedId = isExistingInstance ? _instanceInstallMinecraftChoice?.Id : _downloadInstallMinecraftChoice?.Id;
            var result = await _shellActionService.PromptForChoiceAsync(
                "选择 Minecraft 版本",
                "继续沿用原版安装页的版本选择语义，从可安装版本中选定一个新的基线版本。",
                choices.Select(choice => new PclChoiceDialogOption(choice.Id, choice.Title, choice.Summary)).ToArray(),
                selectedId,
                "使用该版本");
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            var selectedChoice = choices.First(choice => string.Equals(choice.Id, result, StringComparison.Ordinal));
            if (isExistingInstance)
            {
                _instanceInstallMinecraftChoice = selectedChoice;
                if (!string.Equals(currentVersionId, selectedChoice.Version, StringComparison.OrdinalIgnoreCase))
                {
                    ClearManagedSelections(_instanceInstallSelections);
                }

                InstanceInstallMinecraftVersion = $"Minecraft {selectedChoice.Version}";
                InstanceInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", "Grass.png");
                InitializeInstanceInstallSurface();
                RaiseInstallWorkflowProperties();
            }
            else
            {
                _downloadInstallMinecraftChoice = selectedChoice;
                if (!string.Equals(currentVersionId, selectedChoice.Version, StringComparison.OrdinalIgnoreCase))
                {
                    ClearManagedSelections(_downloadInstallSelections);
                }

                DownloadInstallMinecraftVersion = $"Minecraft {selectedChoice.Version}";
                DownloadInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", "Grass.png");
                InitializeDownloadInstallSurface();
                RaiseInstallWorkflowProperties();
            }
        }
        catch (Exception ex)
        {
            AddActivity("加载 Minecraft 版本失败", ex.Message);
        }
    }

    private async Task EditInstallOptionAsync(bool isExistingInstance, string optionTitle)
    {
        if (!FrontendInstallWorkflowService.IsFrontendManagedOption(optionTitle))
        {
            AddActivity($"选择安装项: {optionTitle}", "当前壳层尚未为这一项接入安装实现；这里不会再回退到旧安装器。");
            return;
        }

        try
        {
            var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance).Replace("Minecraft ", "", StringComparison.Ordinal);
            var staticUnavailableReason = GetInstallOptionStaticUnavailableReason(isExistingInstance, optionTitle, minecraftVersion);
            if (staticUnavailableReason is not null)
            {
                AddActivity($"选择安装项: {optionTitle}", staticUnavailableReason);
                return;
            }

            var choices = GetSelectableInstallChoices(isExistingInstance, optionTitle, minecraftVersion);
            var unavailableReason = GetInstallOptionUnavailableReason(isExistingInstance, optionTitle, minecraftVersion, choices);
            if (unavailableReason is not null)
            {
                AddActivity($"选择安装项: {optionTitle}", unavailableReason);
                return;
            }

            var state = GetEditableSelectionState(isExistingInstance, optionTitle);
            var selectedId = state.SelectedChoice?.Id;
            var result = await _shellActionService.PromptForChoiceAsync(
                $"选择 {optionTitle}",
                "继续沿用原版安装页的卡片选择流程，从当前可用候选中选定一个版本。",
                choices.Select(choice => new PclChoiceDialogOption(choice.Id, choice.Title, choice.Summary)).ToArray(),
                selectedId,
                "使用该版本");
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            var selectedChoice = choices.First(choice => string.Equals(choice.Id, result, StringComparison.Ordinal));
            var selections = isExistingInstance ? _instanceInstallSelections : _downloadInstallSelections;
            selections[optionTitle] = new FrontendEditableInstallSelection(selectedChoice, false);

            if (ManagedPrimaryInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
            {
                foreach (var title in ManagedPrimaryInstallTitles.Where(title => !string.Equals(title, optionTitle, StringComparison.Ordinal)))
                {
                    selections[title] = FrontendEditableInstallSelection.Cleared;
                }

                if (!string.Equals(optionTitle, "Quilt", StringComparison.Ordinal))
                {
                    selections["QFAPI / QSL"] = FrontendEditableInstallSelection.Cleared;
                }
                else
                {
                    selections["Legacy Fabric API"] = FrontendEditableInstallSelection.Cleared;
                }

                if (!string.Equals(optionTitle, "Fabric", StringComparison.Ordinal))
                {
                    selections["Fabric API"] = FrontendEditableInstallSelection.Cleared;
                    selections["OptiFabric"] = FrontendEditableInstallSelection.Cleared;
                }

                if (!string.Equals(optionTitle, "Legacy Fabric", StringComparison.Ordinal))
                {
                    selections["Legacy Fabric API"] = FrontendEditableInstallSelection.Cleared;
                }
            }
            else if (ManagedApiInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
            {
                foreach (var title in ManagedApiInstallTitles.Where(title => !string.Equals(title, optionTitle, StringComparison.Ordinal)))
                {
                    selections[title] = FrontendEditableInstallSelection.Cleared;
                }
            }
            else if (string.Equals(optionTitle, "OptiFine", StringComparison.Ordinal)
                     && !string.Equals(GetCurrentPrimaryInstallTitle(isExistingInstance), "Fabric", StringComparison.Ordinal))
            {
                selections["OptiFabric"] = FrontendEditableInstallSelection.Cleared;
            }

            if (isExistingInstance)
            {
                InitializeInstanceInstallSurface();
            }
            else
            {
                InitializeDownloadInstallSurface();
            }

            RaiseInstallWorkflowProperties();
        }
        catch (Exception ex)
        {
            AddActivity($"选择安装项: {optionTitle}", ex.Message);
        }
    }

    private void ClearInstallOption(bool isExistingInstance, string optionTitle)
    {
        var selections = isExistingInstance ? _instanceInstallSelections : _downloadInstallSelections;
        selections[optionTitle] = FrontendEditableInstallSelection.Cleared;
        if (ManagedPrimaryInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
        {
            foreach (var title in ManagedPrimaryDependentInstallTitles)
            {
                selections[title] = FrontendEditableInstallSelection.Cleared;
            }
        }
        else if (string.Equals(optionTitle, "OptiFine", StringComparison.Ordinal))
        {
            selections["OptiFabric"] = FrontendEditableInstallSelection.Cleared;
        }
        else if (ManagedApiInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
        {
            foreach (var title in ManagedApiInstallTitles.Where(title => !string.Equals(title, optionTitle, StringComparison.Ordinal)))
            {
                selections[title] = FrontendEditableInstallSelection.Cleared;
            }
        }

        if (isExistingInstance)
        {
            InitializeInstanceInstallSurface();
        }
        else
        {
            InitializeDownloadInstallSurface();
        }

        RaiseInstallWorkflowProperties();
    }

    private async Task StartDownloadInstallAsync()
    {
        if (!_downloadInstallIsInSelectionStage || _downloadInstallMinecraftChoice is null)
        {
            AddActivity("开始安装", "请先从下载页选择一个 Minecraft 版本。");
            return;
        }

        ValidateDownloadInstallName();
        if (!string.IsNullOrWhiteSpace(DownloadInstallNameValidationMessage))
        {
            AddActivity("开始安装", DownloadInstallNameValidationMessage);
            return;
        }

        var targetName = string.IsNullOrWhiteSpace(DownloadInstallName)
            ? _downloadComposition.Install.Name
            : DownloadInstallName.Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            AddActivity("开始安装", "请输入新的实例名称。");
            return;
        }

        await ApplyInstallAsync(targetName, isExistingInstance: false);
    }

    private async Task ApplyInstanceInstallAsync()
    {
        if (!_instanceComposition.Selection.HasSelection || string.IsNullOrWhiteSpace(_instanceComposition.Selection.InstanceName))
        {
            AddActivity("开始修改", "当前未选择实例。");
            return;
        }

        var actionTitle = HasInstanceInstallChanges ? "开始修改" : "开始重置";
        if (!HasInstanceInstallChanges)
        {
            var confirmed = await _shellActionService.ConfirmAsync(
                "重置此实例",
                "将基于当前安装清单重新补全核心文件与支持库，并保留实例目录中的存档、资源包和大多数用户数据。",
                "继续",
                isDanger: false);
            if (!confirmed)
            {
                return;
            }
        }

        await ApplyInstallAsync(_instanceComposition.Selection.InstanceName, isExistingInstance: true, actionTitle);
    }

    private async Task ApplyInstallAsync(string targetInstanceName, bool isExistingInstance, string? activityTitleOverride = null)
    {
        try
        {
            var unresolvedSelections = GetUnresolvedManagedSelections(isExistingInstance);
            if (unresolvedSelections.Count > 0)
            {
                AddActivity(
                    activityTitleOverride ?? (isExistingInstance ? "开始修改失败" : "开始安装失败"),
                    $"以下安装项当前无法映射到受支持的安装矩阵：{string.Join("、", unresolvedSelections)}。请重新选择或清除后再继续。");
                return;
            }

            var minecraftChoice = ResolveEffectiveMinecraftChoice(isExistingInstance);
            var minecraftVersion = minecraftChoice.Version;
            var primaryChoice = ResolveEffectivePrimaryChoice(isExistingInstance, minecraftVersion);
            var liteLoaderChoice = ResolveEffectiveAddonChoice(isExistingInstance, "LiteLoader", minecraftVersion);
            var optiFineChoice = ResolveEffectiveAddonChoice(isExistingInstance, "OptiFine", minecraftVersion);
            var fabricApiChoice = ResolveEffectiveAddonChoice(isExistingInstance, "Fabric API", minecraftVersion);
            var legacyFabricApiChoice = ResolveEffectiveAddonChoice(isExistingInstance, "Legacy Fabric API", minecraftVersion);
            var qslChoice = ResolveEffectiveAddonChoice(isExistingInstance, "QFAPI / QSL", minecraftVersion);
            var optiFabricChoice = ResolveEffectiveAddonChoice(isExistingInstance, "OptiFabric", minecraftVersion);
            var useIsolation = primaryChoice is not null
                               || liteLoaderChoice is not null
                               || optiFineChoice is not null
                               || fabricApiChoice is not null
                               || legacyFabricApiChoice is not null
                               || qslChoice is not null
                               || optiFabricChoice is not null;
            var activityTitle = activityTitleOverride ?? (isExistingInstance ? InstanceInstallApplyButtonText : "开始安装");
            var request = new FrontendInstallApplyRequest(
                _instanceComposition.Selection.LauncherDirectory,
                targetInstanceName,
                minecraftChoice,
                primaryChoice,
                liteLoaderChoice,
                optiFineChoice,
                fabricApiChoice,
                legacyFabricApiChoice,
                qslChoice,
                optiFabricChoice,
                UseInstanceIsolation: useIsolation,
                RunRepair: true,
                ForceCoreRefresh: !isExistingInstance || !string.Equals(
                    GetEffectiveMinecraftVersion(isExistingInstance),
                    isExistingInstance ? _instanceInstallBaselineMinecraftVersion : _downloadInstallBaselineMinecraftVersion,
                    StringComparison.Ordinal));
            var taskTitle = $"{targetInstanceName} {activityTitle}";

            TaskCenter.Register(
                new FrontendInstallTask(
                    taskTitle,
                    async (report, cancelToken) =>
                    {
                        report(TaskState.Running, "正在写入安装清单并补全依赖文件…");
                        var result = await Task.Run(() => FrontendInstallWorkflowService.Apply(request), cancelToken);

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _shellActionService.PersistLocalValue("LaunchInstanceSelect", targetInstanceName);
                            ReloadInstanceComposition();
                            ReloadDownloadComposition();
                            if (!isExistingInstance)
                            {
                                _downloadInstallIsInSelectionStage = false;
                                _downloadInstallExpandedOptionTitle = null;
                                _downloadInstallMinecraftChoice = null;
                                _downloadInstallIsNameEditedByUser = false;
                                _downloadInstallOptionChoices.Clear();
                                _downloadInstallOptionLoadsInProgress.Clear();
                                _downloadInstallOptionLoadErrors.Clear();
                                InitializeDownloadInstallSurface();
                                RaisePropertyChanged(nameof(DownloadInstallName));
                            }
                            else
                            {
                                InitializeInstanceInstallSurface();
                                RaiseInstallWorkflowProperties();
                            }

                            AddActivity(
                                activityTitle,
                                $"{targetInstanceName} • 已写入安装清单 {result.ManifestPath}，下载 {result.DownloadedFiles.Count} 个文件，复用 {result.ReusedFiles.Count} 个文件。");
                        });

                        report(
                            TaskState.Success,
                            $"已写入安装清单，下载 {result.DownloadedFiles.Count} 个文件，复用 {result.ReusedFiles.Count} 个文件。");
                    },
                    async ex =>
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            AddActivity(activityTitleOverride ?? (isExistingInstance ? "开始修改失败" : "开始安装失败"), ex.Message);
                        });
                    }));

            if (!isExistingInstance)
            {
                _downloadInstallIsInSelectionStage = false;
                _downloadInstallExpandedOptionTitle = null;
                InitializeDownloadInstallSurface();
                RaisePropertyChanged(nameof(DownloadInstallName));
            }

            NavigateTo(
                new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
                $"{taskTitle} 已加入任务中心。");
        }
        catch (Exception ex)
        {
            AddActivity(activityTitleOverride ?? (isExistingInstance ? "开始修改失败" : "开始安装失败"), ex.Message);
        }
    }

    private FrontendInstallChoice ResolveEffectiveMinecraftChoice(bool isExistingInstance)
    {
        var explicitChoice = isExistingInstance ? _instanceInstallMinecraftChoice : _downloadInstallMinecraftChoice;
        if (explicitChoice is not null)
        {
            return explicitChoice;
        }

        var version = GetEffectiveMinecraftVersion(isExistingInstance).Replace("Minecraft ", "", StringComparison.Ordinal);
        return FrontendInstallWorkflowService.GetMinecraftChoices(version)
            .First(choice => string.Equals(choice.Version, version, StringComparison.OrdinalIgnoreCase));
    }

    private FrontendInstallChoice? ResolveEffectivePrimaryChoice(bool isExistingInstance, string minecraftVersion)
    {
        foreach (var optionTitle in ManagedPrimaryInstallTitles)
        {
            var effectiveChoice = ResolveEffectiveChoice(isExistingInstance, optionTitle, minecraftVersion);
            if (effectiveChoice is not null)
            {
                return effectiveChoice;
            }
        }

        return null;
    }

    private FrontendInstallChoice? ResolveEffectiveAddonChoice(bool isExistingInstance, string optionTitle, string minecraftVersion)
    {
        return ResolveEffectiveChoice(isExistingInstance, optionTitle, minecraftVersion);
    }

    private FrontendInstallChoice? ResolveEffectiveChoice(bool isExistingInstance, string optionTitle, string minecraftVersion)
    {
        var state = GetEditableSelectionState(isExistingInstance, optionTitle);
        if (state.SelectedChoice is not null)
        {
            return state.SelectedChoice;
        }

        if (state.IsExplicitlyCleared)
        {
            return null;
        }

        if (!FrontendInstallWorkflowService.IsFrontendManagedOption(optionTitle))
        {
            return null;
        }

        var baselineText = GetBaselineSelection(isExistingInstance, optionTitle);
        if (string.IsNullOrWhiteSpace(baselineText) || baselineText is "未安装" or "可以添加")
        {
            return null;
        }

        var choices = FrontendInstallWorkflowService.GetSupportedChoices(optionTitle, minecraftVersion);
        return choices.FirstOrDefault(choice =>
            string.Equals(choice.Title, baselineText, StringComparison.OrdinalIgnoreCase)
            || choice.Title.Contains(baselineText, StringComparison.OrdinalIgnoreCase)
            || baselineText.Contains(choice.Title, StringComparison.OrdinalIgnoreCase));
    }

    private FrontendEditableInstallSelection GetEditableSelectionState(bool isExistingInstance, string optionTitle)
    {
        var selections = isExistingInstance ? _instanceInstallSelections : _downloadInstallSelections;
        return selections.TryGetValue(optionTitle, out var state)
            ? state
            : FrontendEditableInstallSelection.Unchanged;
    }

    private string GetEffectiveMinecraftVersion(bool isExistingInstance)
    {
        var explicitChoice = isExistingInstance ? _instanceInstallMinecraftChoice : _downloadInstallMinecraftChoice;
        return explicitChoice is null
            ? (isExistingInstance ? _instanceInstallBaselineMinecraftVersion : _downloadInstallBaselineMinecraftVersion)
            : $"Minecraft {explicitChoice.Version}";
    }

    private string GetEffectiveSelectionText(bool isExistingInstance, string optionTitle)
    {
        var state = GetEditableSelectionState(isExistingInstance, optionTitle);
        if (state.SelectedChoice is not null)
        {
            return state.SelectedChoice.Title;
        }

        if (state.IsExplicitlyCleared)
        {
            return "未安装";
        }

        return GetBaselineSelection(isExistingInstance, optionTitle);
    }

    private string GetBaselineSelection(bool isExistingInstance, string optionTitle)
    {
        var selections = isExistingInstance ? _instanceInstallBaselineSelections : _downloadInstallBaselineSelections;
        return selections.TryGetValue(optionTitle, out var value) ? value : "未安装";
    }

    private string GetBaselineMinecraftVersion(bool isExistingInstance)
    {
        var version = isExistingInstance ? _instanceInstallBaselineMinecraftVersion : _downloadInstallBaselineMinecraftVersion;
        return version.Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
    }

    private bool HasInstallMinecraftVersionChanged(bool isExistingInstance)
    {
        return !string.Equals(
            GetEffectiveMinecraftVersion(isExistingInstance),
            isExistingInstance ? _instanceInstallBaselineMinecraftVersion : _downloadInstallBaselineMinecraftVersion,
            StringComparison.Ordinal);
    }

    private IReadOnlyList<string> GetUnresolvedManagedSelections(bool isExistingInstance)
    {
        var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance).Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
        return ManagedPrimaryInstallTitles
            .Concat(ManagedAddonInstallTitles)
            .Distinct(StringComparer.Ordinal)
            .Where(optionTitle => IsManagedSelectionUnresolved(isExistingInstance, optionTitle, minecraftVersion))
            .ToArray();
    }

    private bool IsManagedSelectionUnresolved(bool isExistingInstance, string optionTitle, string minecraftVersion)
    {
        var state = GetEditableSelectionState(isExistingInstance, optionTitle);
        if (state.SelectedChoice is not null || state.IsExplicitlyCleared)
        {
            return false;
        }

        if (!FrontendInstallWorkflowService.IsFrontendManagedOption(optionTitle))
        {
            return false;
        }

        return HasBaselineInstallSelection(isExistingInstance, optionTitle)
               && ResolveEffectiveChoice(isExistingInstance, optionTitle, minecraftVersion) is null;
    }

    private bool HasBaselineInstallSelection(bool isExistingInstance, string optionTitle)
    {
        var baselineText = GetBaselineSelection(isExistingInstance, optionTitle);
        return !string.IsNullOrWhiteSpace(baselineText)
               && !string.Equals(baselineText, "未安装", StringComparison.Ordinal)
               && !string.Equals(baselineText, "可以添加", StringComparison.Ordinal);
    }

    private bool ComputeHasInstanceInstallChanges()
    {
        if (_instanceInstallMinecraftChoice is not null
            && !string.Equals($"Minecraft {_instanceInstallMinecraftChoice.Version}", _instanceInstallBaselineMinecraftVersion, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var pair in _instanceInstallSelections)
        {
            if (pair.Value.SelectedChoice is not null || pair.Value.IsExplicitlyCleared)
            {
                return true;
            }
        }

        return false;
    }

    private void RaiseInstallWorkflowProperties()
    {
        RaisePropertyChanged(nameof(DownloadInstallMinecraftVersion));
        RaisePropertyChanged(nameof(DownloadInstallMinecraftIcon));
        RaisePropertyChanged(nameof(HasInstanceInstallChanges));
        RaisePropertyChanged(nameof(InstanceInstallApplyButtonText));
    }

    private static void ClearManagedSelections(IDictionary<string, FrontendEditableInstallSelection> selections)
    {
        foreach (var key in selections.Keys.ToArray())
        {
            selections[key] = FrontendEditableInstallSelection.Cleared;
        }
    }
}

internal sealed record FrontendEditableInstallSelection(
    FrontendInstallChoice? SelectedChoice,
    bool IsExplicitlyCleared)
{
    public static FrontendEditableInstallSelection Unchanged { get; } = new(null, false);

    public static FrontendEditableInstallSelection Cleared { get; } = new(null, true);
}

internal sealed class FrontendInstallTask(
    string title,
    Func<Action<TaskState, string>, CancellationToken, Task> executeAsync,
    Func<Exception, Task>? onErrorAsync = null)
    : ITask
{
    public string Title { get; } = title;

    public event TaskStateEvent StateChanged = delegate { };

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        void Report(TaskState state, string message)
        {
            StateChanged(state, message);
        }

        Report(TaskState.Waiting, "已加入任务中心");

        try
        {
            await executeAsync(Report, cancelToken);
        }
        catch (OperationCanceledException)
        {
            Report(TaskState.Canceled, "任务已取消");
            throw;
        }
        catch (Exception ex)
        {
            if (onErrorAsync is not null)
            {
                await onErrorAsync(ex);
            }

            Report(TaskState.Failed, ex.Message);
            throw;
        }
    }
}
