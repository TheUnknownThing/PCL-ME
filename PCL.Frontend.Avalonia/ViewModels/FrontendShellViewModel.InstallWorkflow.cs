using System.Threading;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

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
        ResetInstanceInstallOptionBrowserState();
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
                "从可用版本中选择要使用的 Minecraft 版本。",
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

                ResetInstanceInstallOptionBrowserState();
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
            AddFailureActivity("加载 Minecraft 版本失败", ex.Message);
        }
    }

    private async Task EditInstallOptionAsync(bool isExistingInstance, string optionTitle)
    {
        if (!FrontendInstallWorkflowService.IsFrontendManagedOption(optionTitle))
        {
            AddActivity($"选择安装项: {optionTitle}", "暂不支持自动安装这一项。");
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
                "从当前可用候选中选择一个版本。",
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
            AddFailureActivity($"选择安装项失败: {optionTitle}", ex.Message);
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
            var hasModableComponents = primaryChoice is not null
                                       || liteLoaderChoice is not null
                                       || optiFineChoice is not null
                                       || fabricApiChoice is not null
                                       || legacyFabricApiChoice is not null
                                       || qslChoice is not null
                                       || optiFabricChoice is not null;
            var useIsolation = FrontendIsolationPolicyService.ShouldIsolateByGlobalMode(
                SelectedLaunchIsolationIndex,
                hasModableComponents,
                FrontendIsolationPolicyService.IsNonReleaseMinecraftChoice(minecraftChoice));
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
                    async (installTask, cancelToken) =>
                    {
                        installTask.AdvancePhase(
                            FrontendInstallApplyPhase.PrepareManifest,
                            "正在写入安装清单并准备安装环境…");
                        var result = await Task.Run(
                            () => FrontendInstallWorkflowService.Apply(
                                request,
                                (phase, message) => installTask.AdvancePhase(phase, message),
                                snapshot =>
                                {
                                    installTask.ApplyRepairProgress(snapshot);
                                },
                                _shellActionService.GetDownloadTransferOptions(),
                                cancelToken),
                            cancelToken);

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (isExistingInstance || AutoSelectNewInstance)
                            {
                                _shellActionService.PersistLocalValue("LaunchInstanceSelect", targetInstanceName);
                            }

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

                        installTask.CompleteSuccessfully(
                            $"已写入安装清单，下载 {result.DownloadedFiles.Count} 个文件，复用 {result.ReusedFiles.Count} 个文件。");
                    },
                    async ex =>
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            AddFailureActivity(activityTitleOverride ?? (isExistingInstance ? "开始修改失败" : "开始安装失败"), ex.Message);
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
            AddFailureActivity(activityTitleOverride ?? (isExistingInstance ? "开始修改失败" : "开始安装失败"), ex.Message);
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

        var cachedChoice = ResolveCachedBaselineChoice(isExistingInstance, optionTitle, baselineText);
        if (cachedChoice is not null)
        {
            return cachedChoice;
        }

        var choices = FrontendInstallWorkflowService.GetSupportedChoices(optionTitle, minecraftVersion);
        return MatchInstallChoice(choices, baselineText);
    }

    private FrontendInstallChoice? ResolveCachedEffectiveChoice(bool isExistingInstance, string optionTitle, string minecraftVersion)
    {
        var state = GetEditableSelectionState(isExistingInstance, optionTitle);
        if (state.SelectedChoice is not null)
        {
            return state.SelectedChoice;
        }

        if (state.IsExplicitlyCleared || !FrontendInstallWorkflowService.IsFrontendManagedOption(optionTitle))
        {
            return null;
        }

        var baselineText = GetBaselineSelection(isExistingInstance, optionTitle);
        if (string.IsNullOrWhiteSpace(baselineText) || baselineText is "未安装" or "可以添加")
        {
            return null;
        }

        return ResolveCachedBaselineChoice(isExistingInstance, optionTitle, baselineText);
    }

    private FrontendInstallChoice? ResolveCachedBaselineChoice(bool isExistingInstance, string optionTitle, string baselineText)
    {
        return MatchInstallChoice(GetCachedInstallChoices(isExistingInstance, optionTitle), baselineText);
    }

    private IReadOnlyList<FrontendInstallChoice> GetCachedInstallChoices(bool isExistingInstance, string optionTitle)
    {
        var choices = isExistingInstance ? _instanceInstallOptionChoices : _downloadInstallOptionChoices;
        return choices.TryGetValue(optionTitle, out var cachedChoices)
            ? cachedChoices
            : [];
    }

    private static FrontendInstallChoice? MatchInstallChoice(IEnumerable<FrontendInstallChoice> choices, string baselineText)
    {
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
    Func<FrontendInstallTask, CancellationToken, Task> executeAsync,
    Func<Exception, Task>? onErrorAsync = null)
    : ITask, ITaskProgressive, ITaskGroup, ITaskProgressStatus, ITaskCancelable
{
    private readonly Dictionary<FrontendInstallTaskStage, FrontendInstallStageTask> _stages = new()
    {
        [FrontendInstallTaskStage.Prepare] = new("写入安装清单"),
        [FrontendInstallTaskStage.SupportFiles] = new("下载游戏支持文件"),
        [FrontendInstallTaskStage.AssetFiles] = new("下载游戏资源文件"),
        [FrontendInstallTaskStage.Finalize] = new("完成安装")
    };

    private readonly Dictionary<FrontendInstallTaskStage, double> _stageWeights = new()
    {
        [FrontendInstallTaskStage.Prepare] = 1d,
        [FrontendInstallTaskStage.SupportFiles] = 4d,
        [FrontendInstallTaskStage.AssetFiles] = 5d,
        [FrontendInstallTaskStage.Finalize] = 1d
    };

    private double _progress;
    private TaskProgressStatusSnapshot _progressStatus = new("0%", "0 B/s", null, null);
    private readonly CancellationTokenSource _cancellation = new();

    public string Title { get; } = title;

    public TaskProgressStatusSnapshot ProgressStatus => _progressStatus;

    public event TaskStateEvent StateChanged = delegate { };

    public event TaskProgressEvent ProgressChanged = delegate { };

    public event TaskGroupEvent AddTask = delegate { };

    public event TaskGroupEvent RemoveTask = delegate { };

    public event TaskProgressStatusEvent ProgressStatusChanged = delegate { };

    public void Cancel()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Cancel();
        ReportState(TaskState.Running, "正在取消任务…");
    }

    public void AdvancePhase(FrontendInstallApplyPhase phase, string message)
    {
        switch (phase)
        {
            case FrontendInstallApplyPhase.PrepareManifest:
                UpdateStage(FrontendInstallTaskStage.Prepare, TaskState.Running, message, 0.55);
                UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Waiting, "等待开始下载支持文件…", 0d);
                UpdateStage(FrontendInstallTaskStage.AssetFiles, TaskState.Waiting, "等待解析资源文件清单…", 0d);
                UpdateStage(FrontendInstallTaskStage.Finalize, TaskState.Waiting, "等待安装收尾…", 0d);
                ReportState(TaskState.Running, message);
                break;
            case FrontendInstallApplyPhase.DownloadSupportFiles:
                UpdateStage(FrontendInstallTaskStage.Prepare, TaskState.Success, "安装清单已写入", 1d);
                if (_stages[FrontendInstallTaskStage.SupportFiles].State == TaskState.Waiting)
                {
                    UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Running, message, 0.02);
                }

                ReportState(TaskState.Running, message);
                break;
            case FrontendInstallApplyPhase.Finalize:
                if (_stages[FrontendInstallTaskStage.SupportFiles].State is TaskState.Waiting or TaskState.Running)
                {
                    UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Success, "游戏支持文件已就绪", 1d);
                }

                if (_stages[FrontendInstallTaskStage.AssetFiles].State is TaskState.Waiting or TaskState.Running)
                {
                    UpdateStage(FrontendInstallTaskStage.AssetFiles, TaskState.Success, "资源文件已就绪", 1d);
                }

                UpdateStage(FrontendInstallTaskStage.Finalize, TaskState.Running, message, 0.45);
                ReportState(TaskState.Running, message);
                break;
        }
    }

    public void ApplyRepairProgress(FrontendInstanceRepairProgressSnapshot snapshot)
    {
        var supportSnapshot = BuildMergedGroupSnapshot(
            snapshot,
            FrontendInstanceRepairFileGroup.Client,
            FrontendInstanceRepairFileGroup.Libraries,
            FrontendInstanceRepairFileGroup.AssetIndex);
        var assetSnapshot = GetGroupSnapshot(snapshot, FrontendInstanceRepairFileGroup.Assets);

        UpdateStageFromGroup(
            FrontendInstallTaskStage.SupportFiles,
            supportSnapshot,
            "正在补全游戏主文件与支持库…",
            "无需下载游戏支持文件");
        UpdateStageFromGroup(
            FrontendInstallTaskStage.AssetFiles,
            assetSnapshot,
            "正在下载游戏资源文件…",
            "无需下载游戏资源文件");

        if (supportSnapshot.TotalFiles == 0 && assetSnapshot.TotalFiles == 0)
        {
            UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Success, "无需补全游戏支持文件", 1d);
            UpdateStage(FrontendInstallTaskStage.AssetFiles, TaskState.Success, "无需下载游戏资源文件", 1d);
        }

        PublishProgressStatus(
            new TaskProgressStatusSnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                snapshot.SpeedBytesPerSecond > 0d
                    ? $"{FormatBytes(snapshot.SpeedBytesPerSecond)}/s"
                    : "0 B/s",
                snapshot.RemainingFileCount,
                null));

        ReportState(
            TaskState.Running,
            string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                ? "正在下载依赖文件…"
                : $"正在处理 {snapshot.CurrentFileName}");
    }

    public void CompleteSuccessfully(string message)
    {
        UpdateStage(FrontendInstallTaskStage.Prepare, TaskState.Success, "安装清单已写入", 1d);
        UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Success, "游戏支持文件已准备完成", 1d);
        UpdateStage(FrontendInstallTaskStage.AssetFiles, TaskState.Success, "资源文件已准备完成", 1d);
        UpdateStage(FrontendInstallTaskStage.Finalize, TaskState.Success, "安装完成", 1d);
        PublishProgressStatus(
            new TaskProgressStatusSnapshot(
                "100%",
                "0 B/s",
                0,
                null));
        ReportState(TaskState.Success, message);
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var executionToken = linkedCancellation.Token;

        foreach (var stage in _stages.Values)
        {
            AddTask(stage);
        }

        ReportState(TaskState.Waiting, "已加入任务中心");

        foreach (var stage in _stages.Values)
        {
            stage.Report(TaskState.Waiting, "等待执行", 0d);
        }

        try
        {
            await executeAsync(this, executionToken);
        }
        catch (OperationCanceledException)
        {
            UpdateStage(FrontendInstallTaskStage.Finalize, TaskState.Canceled, "任务已取消", _progress);
            PublishProgressStatus(
                new TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1):0.#}%",
                    "0 B/s",
                    null,
                    null));
            ReportState(TaskState.Canceled, "任务已取消");
            throw;
        }
        catch (Exception ex)
        {
            if (onErrorAsync is not null)
            {
                await onErrorAsync(ex);
            }

            UpdateFailingStage(ex.Message);
            ReportState(TaskState.Failed, ex.Message);
            throw;
        }
    }

    private void UpdateFailingStage(string message)
    {
        var stage = _stages.Values.FirstOrDefault(candidate => candidate.State == TaskState.Running)
                    ?? _stages[FrontendInstallTaskStage.Finalize];
        stage.Report(TaskState.Failed, message, stage.Progress);
        PublishProgressStatus(
            new TaskProgressStatusSnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                _progressStatus.SpeedText,
                _progressStatus.RemainingFileCount,
                null));
    }

    private void UpdateStage(
        FrontendInstallTaskStage stage,
        TaskState state,
        string message,
        double progress)
    {
        _stages[stage].Report(state, message, progress);
        RecalculateProgress();
    }

    private void UpdateStageFromGroup(
        FrontendInstallTaskStage stage,
        FrontendInstanceRepairGroupSnapshot snapshot,
        string activePrefix,
        string emptyMessage)
    {
        if (snapshot.TotalFiles == 0)
        {
            UpdateStage(stage, TaskState.Success, emptyMessage, 1d);
            return;
        }

        var message = snapshot.Progress >= 0.999
            ? $"{snapshot.CompletedFiles}/{snapshot.TotalFiles} 个文件已就绪"
            : string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                ? $"{activePrefix} {snapshot.CompletedFiles}/{snapshot.TotalFiles}"
                : $"{activePrefix} {snapshot.CompletedFiles}/{snapshot.TotalFiles} • {snapshot.CurrentFileName}";
        var state = snapshot.Progress >= 0.999 ? TaskState.Success : TaskState.Running;
        UpdateStage(stage, state, message, snapshot.Progress);
    }

    private void ReportState(TaskState state, string message)
    {
        StateChanged(state, message);
    }

    private void PublishProgressStatus(TaskProgressStatusSnapshot snapshot)
    {
        _progressStatus = snapshot;
        ProgressStatusChanged(snapshot);
    }

    private void RecalculateProgress()
    {
        var totalWeight = _stageWeights.Sum(pair => pair.Value);
        _progress = totalWeight <= 0d
            ? 0d
            : _stages.Sum(pair => pair.Value.Progress * _stageWeights[pair.Key]) / totalWeight;
        ProgressChanged(_progress);
        PublishProgressStatus(
            _progressStatus with
            {
                ProgressText = $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%"
            });
    }

    private static FrontendInstanceRepairGroupSnapshot BuildMergedGroupSnapshot(
        FrontendInstanceRepairProgressSnapshot snapshot,
        params FrontendInstanceRepairFileGroup[] groups)
    {
        var available = groups
            .Select(group => GetGroupSnapshot(snapshot, group))
            .Where(group => group.TotalFiles > 0)
            .ToArray();
        if (available.Length == 0)
        {
            return new FrontendInstanceRepairGroupSnapshot(
                FrontendInstanceRepairFileGroup.Client,
                0,
                0,
                0,
                0,
                string.Empty);
        }

        var active = available.FirstOrDefault(group => group.Progress < 0.999) ?? available.Last();
        return new FrontendInstanceRepairGroupSnapshot(
            FrontendInstanceRepairFileGroup.Client,
            available.Sum(group => group.CompletedFiles),
            available.Sum(group => group.TotalFiles),
            available.Sum(group => group.CompletedBytes),
            available.Sum(group => group.TotalBytes),
            active.CurrentFileName);
    }

    private static FrontendInstanceRepairGroupSnapshot GetGroupSnapshot(
        FrontendInstanceRepairProgressSnapshot snapshot,
        FrontendInstanceRepairFileGroup group)
    {
        return snapshot.Groups.TryGetValue(group, out var value)
            ? value
            : new FrontendInstanceRepairGroupSnapshot(group, 0, 0, 0, 0, string.Empty);
    }

    private static string FormatBytes(double value)
    {
        if (value <= 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = value;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

}

internal enum FrontendInstallTaskStage
{
    Prepare,
    SupportFiles,
    AssetFiles,
    Finalize
}

internal sealed class FrontendInstallStageTask(string title) : ITask, ITaskProgressive
{
    private double _progress;

    public string Title { get; } = title;

    public TaskState State { get; private set; } = TaskState.Waiting;

    public double Progress => _progress;

    public event TaskStateEvent StateChanged = delegate { };

    public event TaskProgressEvent ProgressChanged = delegate { };

    public Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        return Task.CompletedTask;
    }

    public void Report(TaskState state, string message, double progress)
    {
        State = state;
        _progress = Math.Clamp(progress, 0d, 1d);
        ProgressChanged(_progress);
        StateChanged(state, message);
    }
}
