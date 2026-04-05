using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
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
                ResetManagedSelections(_instanceInstallSelections);
                InstanceInstallMinecraftVersion = $"Minecraft {selectedChoice.Version}";
                InstanceInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", "Grass.png");
                InitializeInstanceInstallSurface();
                RaiseInstallWorkflowProperties();
            }
            else
            {
                _downloadInstallMinecraftChoice = selectedChoice;
                ResetManagedSelections(_downloadInstallSelections);
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
            AddActivity($"选择安装项: {optionTitle}", "这一项仍依赖旧安装器处理，当前迁移切片先保留原卡片结构并明确提示。");
            return;
        }

        try
        {
            var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance).Replace("Minecraft ", "", StringComparison.Ordinal);
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

            var result = FrontendInstallWorkflowService.Apply(new FrontendInstallApplyRequest(
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
                    StringComparison.Ordinal)));

            _shellActionService.PersistLocalValue("LaunchInstanceSelect", targetInstanceName);
            ReloadInstanceComposition();

            if (!isExistingInstance && AutoSelectNewInstance)
            {
                NavigateTo(
                    new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall),
                    $"自动切换到新实例 {targetInstanceName} 的设置页。");
            }

            var activityTitle = activityTitleOverride ?? (isExistingInstance ? InstanceInstallApplyButtonText : "开始安装");
            AddActivity(
                activityTitle,
                $"{targetInstanceName} • 已写入安装清单 {result.ManifestPath}，下载 {result.DownloadedFiles.Count} 个文件，复用 {result.ReusedFiles.Count} 个文件。");
            OpenInstanceTarget(activityTitle, result.TargetDirectory, "安装目标目录不存在。");
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

    private static void ResetManagedSelections(IDictionary<string, FrontendEditableInstallSelection> selections)
    {
        foreach (var key in selections.Keys.ToArray())
        {
            selections[key] = FrontendEditableInstallSelection.Unchanged;
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
