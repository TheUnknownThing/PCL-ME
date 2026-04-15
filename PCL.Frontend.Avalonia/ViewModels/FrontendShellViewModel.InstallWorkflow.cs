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

    public string InstanceInstallApplyButtonText => HasInstanceInstallChanges
        ? SD("instance.install.actions.apply_changes")
        : SD("instance.install.actions.reset_to_scanned");

    public bool HasInstanceInstallChanges => ComputeHasInstanceInstallChanges();

    private string InstallNoneSelectionText => T("download.install.workflow.selection.none");

    private string InstallAvailableSelectionText => T("download.install.options.available");

    private string BuildInstallOptionActivityTitle(string optionTitle)
    {
        return T("download.install.workflow.activities.select_option", ("option_title", optionTitle));
    }

    private string BuildInstallChoiceDialogTitle(string optionTitle)
    {
        return T("download.install.workflow.dialogs.select_option.title", ("option_title", optionTitle));
    }

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
            var choices = FrontendInstallWorkflowService.GetMinecraftChoices(currentVersion, _i18n);
            var selectedId = isExistingInstance ? _instanceInstallMinecraftChoice?.Id : _downloadInstallMinecraftChoice?.Id;
            var result = await _shellActionService.PromptForChoiceAsync(
                T("download.install.workflow.dialogs.select_minecraft.title"),
                T("download.install.workflow.dialogs.select_minecraft.message"),
                choices.Select(choice => new PclChoiceDialogOption(choice.Id, choice.Title, choice.Summary)).ToArray(),
                selectedId,
                T("download.install.workflow.dialogs.select_minecraft.confirm"));
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
            AddFailureActivity(T("download.install.workflow.activities.load_minecraft_failed"), ex.Message);
        }
    }

    private async Task EditInstallOptionAsync(bool isExistingInstance, string optionTitle)
    {
        if (!FrontendInstallWorkflowService.IsFrontendManagedOption(optionTitle))
        {
            AddActivity(BuildInstallOptionActivityTitle(optionTitle), T("download.install.workflow.messages.option_not_supported"));
            return;
        }

        try
        {
            var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance).Replace("Minecraft ", "", StringComparison.Ordinal);
            var staticUnavailableReason = GetInstallOptionStaticUnavailableReason(isExistingInstance, optionTitle, minecraftVersion);
            if (staticUnavailableReason is not null)
            {
                AddActivity(BuildInstallOptionActivityTitle(optionTitle), staticUnavailableReason);
                return;
            }

            var choices = GetSelectableInstallChoices(isExistingInstance, optionTitle, minecraftVersion);
            var unavailableReason = GetInstallOptionUnavailableReason(isExistingInstance, optionTitle, minecraftVersion, choices);
            if (unavailableReason is not null)
            {
                AddActivity(BuildInstallOptionActivityTitle(optionTitle), unavailableReason);
                return;
            }

            var state = GetEditableSelectionState(isExistingInstance, optionTitle);
            var selectedId = state.SelectedChoice?.Id;
            var result = await _shellActionService.PromptForChoiceAsync(
                BuildInstallChoiceDialogTitle(optionTitle),
                T("download.install.workflow.dialogs.select_option.message"),
                choices.Select(choice => new PclChoiceDialogOption(choice.Id, choice.Title, choice.Summary)).ToArray(),
                selectedId,
                T("download.install.workflow.dialogs.select_option.confirm"));
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
            AddFailureActivity(T("download.install.workflow.activities.select_option_failed", ("option_title", optionTitle)), ex.Message);
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
            AddActivity(T("download.install.actions.start"), T("download.install.workflow.messages.select_minecraft_first"));
            return;
        }

        ValidateDownloadInstallName();
        if (!string.IsNullOrWhiteSpace(DownloadInstallNameValidationMessage))
        {
            AddActivity(T("download.install.actions.start"), DownloadInstallNameValidationMessage);
            return;
        }

        var targetName = string.IsNullOrWhiteSpace(DownloadInstallName)
            ? _downloadComposition.Install.Name
            : DownloadInstallName.Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            AddActivity(T("download.install.actions.start"), T("download.install.validation.empty_name"));
            return;
        }

        await ApplyInstallAsync(targetName, isExistingInstance: false);
    }

    private async Task ApplyInstanceInstallAsync()
    {
        if (!_instanceComposition.Selection.HasSelection || string.IsNullOrWhiteSpace(_instanceComposition.Selection.InstanceName))
        {
            AddActivity(T("download.install.workflow.activities.apply_changes"), T("download.install.workflow.messages.instance_missing"));
            return;
        }

        var actionTitle = HasInstanceInstallChanges
            ? T("download.install.workflow.activities.apply_changes")
            : T("download.install.workflow.activities.reset_instance");
        if (!HasInstanceInstallChanges)
        {
            var confirmed = await _shellActionService.ConfirmAsync(
                T("download.install.workflow.dialogs.reset_instance.title"),
                T("download.install.workflow.dialogs.reset_instance.message"),
                T("download.install.workflow.dialogs.reset_instance.confirm"),
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
                    activityTitleOverride ?? (isExistingInstance ? T("download.install.workflow.activities.apply_changes_failed") : T("download.install.workflow.activities.start_failed")),
                    T("download.install.workflow.messages.unresolved_selections", ("options", string.Join(T("common.punctuation.comma"), unresolvedSelections))));
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
            var activityTitle = activityTitleOverride ?? (isExistingInstance ? InstanceInstallApplyButtonText : T("download.install.actions.start"));
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
                    _i18n,
                    async (installTask, cancelToken) =>
                    {
                        installTask.AdvancePhase(
                            FrontendInstallApplyPhase.PrepareManifest,
                            T("download.install.workflow.tasks.prepare_environment"));
                        var result = await Task.Run(
                            () => FrontendInstallWorkflowService.Apply(
                                request,
                                (phase, message) => installTask.AdvancePhase(phase, message),
                                snapshot =>
                                {
                                    installTask.ApplyRepairProgress(snapshot);
                                },
                                _shellActionService.GetDownloadTransferOptions(),
                                _i18n,
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
                                T(
                                    "download.install.workflow.messages.apply_completed_with_target",
                                    ("target_name", targetInstanceName),
                                    ("manifest_path", result.ManifestPath),
                                    ("downloaded_count", result.DownloadedFiles.Count),
                                    ("reused_count", result.ReusedFiles.Count)));
                        });

                        installTask.CompleteSuccessfully(
                            T(
                                "download.install.workflow.messages.apply_completed",
                                ("downloaded_count", result.DownloadedFiles.Count),
                                ("reused_count", result.ReusedFiles.Count)));
                    },
                    async ex =>
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            AddFailureActivity(activityTitleOverride ?? (isExistingInstance ? T("download.install.workflow.activities.apply_changes_failed") : T("download.install.workflow.activities.start_failed")), ex.Message);
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
                T("download.install.workflow.messages.queued_in_task_center", ("task_title", taskTitle)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(activityTitleOverride ?? (isExistingInstance ? T("download.install.workflow.activities.apply_changes_failed") : T("download.install.workflow.activities.start_failed")), ex.Message);
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
        return FrontendInstallWorkflowService.GetMinecraftChoices(version, _i18n)
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
        if (string.IsNullOrWhiteSpace(baselineText) || baselineText == InstallNoneSelectionText || baselineText == InstallAvailableSelectionText)
        {
            return null;
        }

        var cachedChoice = ResolveCachedBaselineChoice(isExistingInstance, optionTitle, baselineText);
        if (cachedChoice is not null)
        {
            return cachedChoice;
        }

        var choices = FrontendInstallWorkflowService.GetSupportedChoices(optionTitle, minecraftVersion, _i18n);
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
        if (string.IsNullOrWhiteSpace(baselineText) || baselineText == InstallNoneSelectionText || baselineText == InstallAvailableSelectionText)
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
            return InstallNoneSelectionText;
        }

        return GetBaselineSelection(isExistingInstance, optionTitle);
    }

    private string GetBaselineSelection(bool isExistingInstance, string optionTitle)
    {
        var selections = isExistingInstance ? _instanceInstallBaselineSelections : _downloadInstallBaselineSelections;
        return selections.TryGetValue(optionTitle, out var value) ? value : InstallNoneSelectionText;
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
               && !string.Equals(baselineText, InstallNoneSelectionText, StringComparison.Ordinal)
               && !string.Equals(baselineText, InstallAvailableSelectionText, StringComparison.Ordinal);
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
    II18nService i18n,
    Func<FrontendInstallTask, CancellationToken, Task> executeAsync,
    Func<Exception, Task>? onErrorAsync = null)
    : ITask, ITaskProgressive, ITaskGroup, ITaskProgressStatus, ITaskCancelable
{
    private readonly Dictionary<FrontendInstallTaskStage, FrontendInstallStageTask> _stages = new()
    {
        [FrontendInstallTaskStage.Prepare] = new(i18n.T("download.install.workflow.stages.prepare")),
        [FrontendInstallTaskStage.SupportFiles] = new(i18n.T("download.install.workflow.stages.support_files")),
        [FrontendInstallTaskStage.AssetFiles] = new(i18n.T("download.install.workflow.stages.asset_files")),
        [FrontendInstallTaskStage.Finalize] = new(i18n.T("download.install.workflow.stages.finalize"))
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
        ReportState(TaskState.Running, i18n.T("download.install.workflow.tasks.canceling"));
    }

    public void AdvancePhase(FrontendInstallApplyPhase phase, string message)
    {
        switch (phase)
        {
            case FrontendInstallApplyPhase.PrepareManifest:
                UpdateStage(FrontendInstallTaskStage.Prepare, TaskState.Running, message, 0.55);
                UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_support_files"), 0d);
                UpdateStage(FrontendInstallTaskStage.AssetFiles, TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_asset_files"), 0d);
                UpdateStage(FrontendInstallTaskStage.Finalize, TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_finalize"), 0d);
                ReportState(TaskState.Running, message);
                break;
            case FrontendInstallApplyPhase.DownloadSupportFiles:
                UpdateStage(FrontendInstallTaskStage.Prepare, TaskState.Success, i18n.T("download.install.workflow.tasks.manifest_written"), 1d);
                if (_stages[FrontendInstallTaskStage.SupportFiles].State == TaskState.Waiting)
                {
                    UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Running, message, 0.02);
                }

                ReportState(TaskState.Running, message);
                break;
            case FrontendInstallApplyPhase.Finalize:
                if (_stages[FrontendInstallTaskStage.SupportFiles].State is TaskState.Waiting or TaskState.Running)
                {
                    UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Success, i18n.T("download.install.workflow.tasks.support_files_ready"), 1d);
                }

                if (_stages[FrontendInstallTaskStage.AssetFiles].State is TaskState.Waiting or TaskState.Running)
                {
                    UpdateStage(FrontendInstallTaskStage.AssetFiles, TaskState.Success, i18n.T("download.install.workflow.tasks.asset_files_ready"), 1d);
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
            i18n.T("download.install.workflow.tasks.repairing_support_files"),
            i18n.T("download.install.workflow.tasks.no_support_files_needed"));
        UpdateStageFromGroup(
            FrontendInstallTaskStage.AssetFiles,
            assetSnapshot,
            i18n.T("download.install.workflow.tasks.downloading_asset_files"),
            i18n.T("download.install.workflow.tasks.no_asset_files_needed"));

        if (supportSnapshot.TotalFiles == 0 && assetSnapshot.TotalFiles == 0)
        {
            UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Success, i18n.T("download.install.workflow.tasks.no_support_files_to_repair"), 1d);
            UpdateStage(FrontendInstallTaskStage.AssetFiles, TaskState.Success, i18n.T("download.install.workflow.tasks.no_asset_files_to_download"), 1d);
        }

        PublishProgressStatus(
            new TaskProgressStatusSnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                snapshot.SpeedBytesPerSecond > 0d
                    ? $"{FormatBytes(snapshot.SpeedBytesPerSecond)}/s"
                    : i18n.T("launch.dialog.download_speed.zero"),
                snapshot.RemainingFileCount,
                null));

        ReportState(
            TaskState.Running,
            string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                ? i18n.T("download.install.workflow.tasks.downloading_dependencies")
                : i18n.T("download.install.workflow.tasks.processing_file", new Dictionary<string, object?> { ["file_name"] = snapshot.CurrentFileName }));
    }

    public void CompleteSuccessfully(string message)
    {
        UpdateStage(FrontendInstallTaskStage.Prepare, TaskState.Success, i18n.T("download.install.workflow.tasks.manifest_written"), 1d);
        UpdateStage(FrontendInstallTaskStage.SupportFiles, TaskState.Success, i18n.T("download.install.workflow.tasks.support_files_completed"), 1d);
        UpdateStage(FrontendInstallTaskStage.AssetFiles, TaskState.Success, i18n.T("download.install.workflow.tasks.asset_files_completed"), 1d);
        UpdateStage(FrontendInstallTaskStage.Finalize, TaskState.Success, i18n.T("download.install.workflow.tasks.completed"), 1d);
        PublishProgressStatus(
            new TaskProgressStatusSnapshot(
                "100%",
                i18n.T("launch.dialog.download_speed.zero"),
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

        ReportState(TaskState.Waiting, i18n.T("download.install.workflow.tasks.queued"));

        foreach (var stage in _stages.Values)
        {
            stage.Report(TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_execution"), 0d);
        }

        try
        {
            await executeAsync(this, executionToken);
        }
        catch (OperationCanceledException)
        {
            UpdateStage(FrontendInstallTaskStage.Finalize, TaskState.Canceled, i18n.T("download.install.workflow.tasks.canceled"), _progress);
            PublishProgressStatus(
                new TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1):0.#}%",
                    i18n.T("launch.dialog.download_speed.zero"),
                    null,
                    null));
            ReportState(TaskState.Canceled, i18n.T("download.install.workflow.tasks.canceled"));
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
            ? i18n.T("download.install.workflow.tasks.files_ready", new Dictionary<string, object?> { ["completed_count"] = snapshot.CompletedFiles, ["total_count"] = snapshot.TotalFiles })
            : string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                ? i18n.T("download.install.workflow.tasks.progress_without_file", new Dictionary<string, object?> { ["prefix"] = activePrefix, ["completed_count"] = snapshot.CompletedFiles, ["total_count"] = snapshot.TotalFiles })
                : i18n.T("download.install.workflow.tasks.progress_with_file", new Dictionary<string, object?> { ["prefix"] = activePrefix, ["completed_count"] = snapshot.CompletedFiles, ["total_count"] = snapshot.TotalFiles, ["file_name"] = snapshot.CurrentFileName });
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
