using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private readonly Dictionary<string, IReadOnlyList<FrontendInstallChoice>> _instanceInstallOptionChoices = new(StringComparer.Ordinal);
    private readonly HashSet<string> _instanceInstallOptionLoadsInProgress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _instanceInstallOptionLoadErrors = new(StringComparer.Ordinal);
    private string _instanceInstallSelectionTitle = "Modern Fabric Demo";
    private string _instanceInstallSelectionSummary = "Minecraft 1.21.1 / Fabric 0.16.9 / 独立实例";
    private string _instanceInstallMinecraftVersion = "Minecraft 1.21.1";
    private Bitmap? _instanceInstallSelectionIcon;
    private Bitmap? _instanceInstallMinecraftIcon;
    private string? _instanceInstallExpandedOptionTitle;
    private bool _instanceInstallAutoSelectedFabricApi;
    private bool _instanceInstallAutoSelectedLegacyFabricApi;
    private bool _instanceInstallAutoSelectedQsl;
    private bool _instanceInstallAutoSelectedOptiFabric;

    public string InstanceInstallSelectionTitle
    {
        get => _instanceInstallSelectionTitle;
        private set => SetProperty(ref _instanceInstallSelectionTitle, value);
    }

    public string InstanceInstallSelectionSummary
    {
        get => _instanceInstallSelectionSummary;
        private set => SetProperty(ref _instanceInstallSelectionSummary, value);
    }

    public Bitmap? InstanceInstallSelectionIcon
    {
        get => _instanceInstallSelectionIcon;
        private set => SetProperty(ref _instanceInstallSelectionIcon, value);
    }

    public string InstanceInstallMinecraftVersion
    {
        get => _instanceInstallMinecraftVersion;
        private set => SetProperty(ref _instanceInstallMinecraftVersion, value);
    }

    public Bitmap? InstanceInstallMinecraftIcon
    {
        get => _instanceInstallMinecraftIcon;
        private set => SetProperty(ref _instanceInstallMinecraftIcon, value);
    }

    public bool CanApplyInstanceInstall => _instanceComposition.Selection.HasSelection;

    public string InstanceInstallApplyButtonIconData => FrontendIconCatalog.GetNavigationIcon("下载").Data;

    public double InstanceInstallApplyButtonIconScale => 0.95;

    public ActionCommand EditInstanceInstallSelectionCommand => new(() =>
        AddActivity("修改实例安装目标", $"{InstanceInstallSelectionTitle} • {InstanceInstallSelectionSummary}"));

    public ActionCommand EditInstanceInstallMinecraftCommand => new(() =>
        _ = EditInstallMinecraftAsync(isExistingInstance: true));

    private void InitializeInstanceInstallSurface()
    {
        EnsureInstanceInstallEditableState();
        var installState = _instanceComposition.Install;
        InstanceInstallSelectionTitle = installState.SelectionTitle;
        InstanceInstallSelectionSummary = BuildInstanceInstallSelectionSummary();
        InstanceInstallSelectionIcon = LoadLauncherBitmap("Images", "Blocks", GetInstanceInstallSelectionIconName());
        InstanceInstallMinecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance: true);
        InstanceInstallMinecraftIcon = LoadLauncherBitmap(
            "Images",
            "Blocks",
            _instanceInstallMinecraftChoice is null ? installState.MinecraftIconName : "Grass.png");

        ReplaceItems(InstanceInstallHints, BuildInstanceInstallHintStrips());
        SyncInstanceInstallOptionCards(BuildInstanceInstallOptionCards());
    }

    private void RefreshInstanceInstallSurface()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceInstall))
        {
            return;
        }

        RaisePropertyChanged(nameof(InstanceInstallSelectionTitle));
        RaisePropertyChanged(nameof(InstanceInstallSelectionSummary));
        RaisePropertyChanged(nameof(InstanceInstallSelectionIcon));
        RaisePropertyChanged(nameof(InstanceInstallMinecraftVersion));
        RaisePropertyChanged(nameof(InstanceInstallMinecraftIcon));
        RaisePropertyChanged(nameof(CanApplyInstanceInstall));
        RaisePropertyChanged(nameof(InstanceInstallApplyButtonIconData));
        RaisePropertyChanged(nameof(InstanceInstallApplyButtonIconScale));
    }

    private IReadOnlyList<SurfaceNoticeViewModel> BuildInstanceInstallHintStrips()
    {
        var hints = GetEffectiveInstallHints(isExistingInstance: true).ToArray();
        return hints.Select((hint, index) => CreateNoticeStrip(
            hint,
            "#FFF1EA",
            "#F1C8B6",
            "#A94F2B",
            index < 2 ? new Thickness(0, 10, 0, 0) : new Thickness(0, 1, 0, 7))).ToArray();
    }

    private string BuildInstanceInstallSelectionSummary()
    {
        var currentSummary = _instanceComposition.Install.SelectionSummary;
        var targetSummary = BuildInstanceInstallTargetSummary();
        return string.Equals(currentSummary, targetSummary, StringComparison.Ordinal)
            ? targetSummary
            : $"{currentSummary} → {targetSummary}";
    }

    private string BuildInstanceInstallTargetSummary()
    {
        var parts = new List<string>();
        var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance: true).Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
        var primaryChoice = ResolveInstanceInstallPrimaryChoice(minecraftVersion);
        if (primaryChoice is not null)
        {
            parts.Add(primaryChoice.Title);
        }

        parts.Add(GetEffectiveMinecraftVersion(isExistingInstance: true));
        parts.Add(_instanceComposition.Selection.IsIndie ? "独立实例" : "共用实例");
        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private FrontendInstallChoice? ResolveInstanceInstallPrimaryChoice(string minecraftVersion)
    {
        return ResolveEffectiveChoice(isExistingInstance: true, "NeoForge", minecraftVersion)
               ?? ResolveEffectiveChoice(isExistingInstance: true, "Cleanroom", minecraftVersion)
               ?? ResolveEffectiveChoice(isExistingInstance: true, "Fabric", minecraftVersion)
               ?? ResolveEffectiveChoice(isExistingInstance: true, "Legacy Fabric", minecraftVersion)
               ?? ResolveEffectiveChoice(isExistingInstance: true, "Quilt", minecraftVersion)
               ?? ResolveEffectiveChoice(isExistingInstance: true, "Forge", minecraftVersion)
               ?? ResolveEffectiveChoice(isExistingInstance: true, "OptiFine", minecraftVersion)
               ?? ResolveEffectiveChoice(isExistingInstance: true, "LiteLoader", minecraftVersion)
               ?? ResolveEffectiveChoice(isExistingInstance: true, "LabyMod", minecraftVersion);
    }

    private string GetInstanceInstallSelectionIconName()
    {
        var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance: true).Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
        if (ResolveEffectiveChoice(true, "NeoForge", minecraftVersion) is not null)
        {
            return "NeoForge.png";
        }

        if (ResolveEffectiveChoice(true, "Cleanroom", minecraftVersion) is not null)
        {
            return "Cleanroom.png";
        }

        if (ResolveEffectiveChoice(true, "Fabric", minecraftVersion) is not null
            || ResolveEffectiveChoice(true, "Legacy Fabric", minecraftVersion) is not null)
        {
            return "Fabric.png";
        }

        if (ResolveEffectiveChoice(true, "Quilt", minecraftVersion) is not null)
        {
            return "Quilt.png";
        }

        if (ResolveEffectiveChoice(true, "Forge", minecraftVersion) is not null)
        {
            return "Anvil.png";
        }

        if (ResolveEffectiveChoice(true, "OptiFine", minecraftVersion) is not null)
        {
            return "GrassPath.png";
        }

        if (ResolveEffectiveChoice(true, "LabyMod", minecraftVersion) is not null)
        {
            return "LabyMod.png";
        }

        if (ResolveEffectiveChoice(true, "LiteLoader", minecraftVersion) is not null)
        {
            return "Egg.png";
        }

        return "Grass.png";
    }

    private IReadOnlyList<DownloadInstallOptionCardViewModel> BuildInstanceInstallOptionCards()
    {
        var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance: true).Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return [];
        }

        var cards = new List<DownloadInstallOptionCardViewModel>();
        foreach (var (title, iconName) in DownloadInstallOptionBlueprints)
        {
            if (!ShouldShowInstallOption(title, minecraftVersion))
            {
                continue;
            }

            cards.Add(CreateInstanceInstallOptionCard(title, iconName, minecraftVersion));
        }

        return cards;
    }

    private DownloadInstallOptionCardViewModel CreateInstanceInstallOptionCard(string optionTitle, string iconName, string minecraftVersion)
    {
        var effectiveChoice = ResolveEffectiveChoice(isExistingInstance: true, optionTitle, minecraftVersion);
        var staticUnavailableReason = GetInstallOptionStaticUnavailableReason(isExistingInstance: true, optionTitle, minecraftVersion);
        var loadError = _instanceInstallOptionLoadErrors.TryGetValue(optionTitle, out var loadErrorText)
            ? loadErrorText
            : null;
        var hasLoadedChoices = _instanceInstallOptionChoices.TryGetValue(optionTitle, out var availableChoices);
        var cachedChoices = hasLoadedChoices
            ? availableChoices!
            : [];
        var unavailableReason = loadError
            ?? staticUnavailableReason
            ?? (hasLoadedChoices
                ? GetInstallOptionUnavailableReason(isExistingInstance: true, optionTitle, minecraftVersion, cachedChoices)
                : null);
        var selectionText = effectiveChoice?.Title
                            ?? (unavailableReason is null ? "可以添加" : unavailableReason);
        var canExpand = unavailableReason is null;
        var isExpanded = canExpand && string.Equals(_instanceInstallExpandedOptionTitle, optionTitle, StringComparison.Ordinal);
        var choiceItems = cachedChoices.Select(choice => new DownloadInstallChoiceItemViewModel(
            choice.Title,
            choice.Summary,
            effectiveChoice is not null && string.Equals(choice.Id, effectiveChoice.Id, StringComparison.Ordinal),
            new ActionCommand(() => SelectInstanceInstallOption(optionTitle, choice)))).ToArray();

        return new DownloadInstallOptionCardViewModel(
            optionTitle,
            selectionText,
            effectiveChoice is null ? null : LoadLauncherBitmap("Images", "Blocks", iconName),
            effectiveChoice is not null,
            effectiveChoice is null,
            canExpand,
            isExpanded,
            _instanceInstallOptionLoadsInProgress.Contains(optionTitle),
            _instanceInstallOptionLoadsInProgress.Contains(optionTitle) ? "正在获取版本列表" : string.Empty,
            canExpand && !_instanceInstallOptionLoadsInProgress.Contains(optionTitle) && choiceItems.Length == 0 && isExpanded,
            canExpand ? string.Empty : selectionText,
            choiceItems,
            CanClearInstallSelection(isExistingInstance: true, optionTitle),
            new ActionCommand(() => ToggleInstanceInstallOptionCard(optionTitle)),
            new ActionCommand(() => ClearInstanceInstallOption(optionTitle)));
    }

    private void SyncInstanceInstallOptionCards(IReadOnlyList<DownloadInstallOptionCardViewModel> cards)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            var desiredCard = cards[index];
            var existingIndex = FindInstanceInstallOptionCardIndex(desiredCard.Title);
            if (existingIndex >= 0)
            {
                var existingCard = InstanceInstallOptionCards[existingIndex];
                existingCard.UpdateFrom(desiredCard);
                if (existingIndex != index)
                {
                    InstanceInstallOptionCards.Move(existingIndex, index);
                }

                continue;
            }

            InstanceInstallOptionCards.Insert(index, desiredCard);
        }

        while (InstanceInstallOptionCards.Count > cards.Count)
        {
            InstanceInstallOptionCards.RemoveAt(InstanceInstallOptionCards.Count - 1);
        }
    }

    private int FindInstanceInstallOptionCardIndex(string title)
    {
        for (var index = 0; index < InstanceInstallOptionCards.Count; index++)
        {
            if (string.Equals(InstanceInstallOptionCards[index].Title, title, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void ToggleInstanceInstallOptionCard(string optionTitle)
    {
        var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance: true).Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return;
        }

        if (GetInstallOptionStaticUnavailableReason(isExistingInstance: true, optionTitle, minecraftVersion) is not null)
        {
            return;
        }

        var nextTitle = string.Equals(_instanceInstallExpandedOptionTitle, optionTitle, StringComparison.Ordinal)
            ? null
            : optionTitle;
        _instanceInstallExpandedOptionTitle = nextTitle;
        InitializeInstanceInstallSurface();

        if (nextTitle is not null)
        {
            EnsureInstanceInstallOptionChoicesLoaded(optionTitle, minecraftVersion);
        }
    }

    private void EnsureInstanceInstallOptionChoicesLoaded(string optionTitle, string minecraftVersion)
    {
        if (_instanceInstallOptionChoices.ContainsKey(optionTitle) || _instanceInstallOptionLoadsInProgress.Contains(optionTitle))
        {
            return;
        }

        _instanceInstallOptionLoadsInProgress.Add(optionTitle);
        _instanceInstallOptionLoadErrors.Remove(optionTitle);
        InitializeInstanceInstallSurface();

        var versionSignature = minecraftVersion;
        _ = Task.Run(() => GetSelectableInstallChoices(isExistingInstance: true, optionTitle, versionSignature))
            .ContinueWith(async task =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _instanceInstallOptionLoadsInProgress.Remove(optionTitle);
                    var currentVersion = GetEffectiveMinecraftVersion(isExistingInstance: true).Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
                    if (!string.Equals(currentVersion, versionSignature, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        _instanceInstallOptionLoadErrors[optionTitle] = task.Exception?.GetBaseException().Message ?? "获取版本列表失败";
                    }
                    else
                    {
                        _instanceInstallOptionChoices[optionTitle] = task.Result;
                        TryApplyInstanceInstallAutoSelection(optionTitle, task.Result, versionSignature);
                    }

                    InitializeInstanceInstallSurface();
                });
            }, TaskScheduler.Default);
    }

    private void TryApplyInstanceInstallAutoSelection(
        string optionTitle,
        IReadOnlyList<FrontendInstallChoice> choices,
        string minecraftVersion)
    {
        if (choices.Count == 0)
        {
            return;
        }

        if (string.Equals(optionTitle, "QFAPI / QSL", StringComparison.Ordinal))
        {
            if (_instanceInstallAutoSelectedQsl || !HasInstallSelection(isExistingInstance: true, "Quilt"))
            {
                return;
            }

            _instanceInstallAutoSelectedQsl = true;
            _instanceInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choices[0], false);
            return;
        }

        if (string.Equals(optionTitle, "Fabric API", StringComparison.Ordinal))
        {
            var hasLoadedQslChoices = _instanceInstallOptionChoices.ContainsKey("QFAPI / QSL");
            var shouldAutoSelect = HasInstallSelection(isExistingInstance: true, "Fabric")
                                   || (HasInstallSelection(isExistingInstance: true, "Quilt")
                                       && hasLoadedQslChoices
                                       && GetInstallOptionUnavailableReason(true, "QFAPI / QSL", minecraftVersion, ResolveCachedInstanceInstallChoices("QFAPI / QSL")) == "无可用版本");
            if (_instanceInstallAutoSelectedFabricApi || !shouldAutoSelect)
            {
                return;
            }

            _instanceInstallAutoSelectedFabricApi = true;
            _instanceInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choices[0], false);
            return;
        }

        if (string.Equals(optionTitle, "Legacy Fabric API", StringComparison.Ordinal))
        {
            var hasLoadedQslChoices = _instanceInstallOptionChoices.ContainsKey("QFAPI / QSL");
            var shouldAutoSelect = HasInstallSelection(isExistingInstance: true, "Legacy Fabric")
                                   || (HasInstallSelection(isExistingInstance: true, "Quilt")
                                       && hasLoadedQslChoices
                                       && GetInstallOptionUnavailableReason(true, "QFAPI / QSL", minecraftVersion, ResolveCachedInstanceInstallChoices("QFAPI / QSL")) == "无可用版本");
            if (_instanceInstallAutoSelectedLegacyFabricApi || !shouldAutoSelect)
            {
                return;
            }

            _instanceInstallAutoSelectedLegacyFabricApi = true;
            _instanceInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choices[0], false);
            return;
        }

        if (string.Equals(optionTitle, "OptiFabric", StringComparison.Ordinal))
        {
            if (_instanceInstallAutoSelectedOptiFabric
                || !HasInstallSelection(isExistingInstance: true, "Fabric")
                || !HasInstallSelection(isExistingInstance: true, "OptiFine")
                || IsOptiFabricOriginsOnlyVersion(minecraftVersion))
            {
                return;
            }

            _instanceInstallAutoSelectedOptiFabric = true;
            _instanceInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choices[0], false);
        }
    }

    private IReadOnlyList<FrontendInstallChoice> ResolveCachedInstanceInstallChoices(string optionTitle)
    {
        return _instanceInstallOptionChoices.TryGetValue(optionTitle, out var choices) ? choices : [];
    }

    private void SelectInstanceInstallOption(string optionTitle, FrontendInstallChoice choice)
    {
        _instanceInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choice, false);

        if (ManagedPrimaryInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
        {
            foreach (var title in ManagedPrimaryInstallTitles.Where(title => !string.Equals(title, optionTitle, StringComparison.Ordinal)))
            {
                _instanceInstallSelections[title] = FrontendEditableInstallSelection.Cleared;
            }

            if (!string.Equals(optionTitle, "Quilt", StringComparison.Ordinal))
            {
                _instanceInstallSelections["QFAPI / QSL"] = FrontendEditableInstallSelection.Cleared;
                _instanceInstallAutoSelectedQsl = false;
            }
            else
            {
                _instanceInstallSelections["Legacy Fabric API"] = FrontendEditableInstallSelection.Cleared;
                _instanceInstallAutoSelectedLegacyFabricApi = false;
            }

            if (!string.Equals(optionTitle, "Fabric", StringComparison.Ordinal))
            {
                _instanceInstallSelections["Fabric API"] = FrontendEditableInstallSelection.Cleared;
                _instanceInstallSelections["OptiFabric"] = FrontendEditableInstallSelection.Cleared;
                _instanceInstallAutoSelectedFabricApi = false;
                _instanceInstallAutoSelectedOptiFabric = false;
            }

            if (!string.Equals(optionTitle, "Legacy Fabric", StringComparison.Ordinal))
            {
                _instanceInstallSelections["Legacy Fabric API"] = FrontendEditableInstallSelection.Cleared;
                _instanceInstallAutoSelectedLegacyFabricApi = false;
            }
        }
        else if (ManagedApiInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
        {
            foreach (var title in ManagedApiInstallTitles.Where(title => !string.Equals(title, optionTitle, StringComparison.Ordinal)))
            {
                _instanceInstallSelections[title] = FrontendEditableInstallSelection.Cleared;
            }
        }
        else if (string.Equals(optionTitle, "OptiFine", StringComparison.Ordinal)
                 && !string.Equals(GetCurrentPrimaryInstallTitle(isExistingInstance: true), "Fabric", StringComparison.Ordinal))
        {
            _instanceInstallSelections["OptiFabric"] = FrontendEditableInstallSelection.Cleared;
            _instanceInstallAutoSelectedOptiFabric = false;
        }

        ResetInstanceInstallOptionBrowserState();
        InitializeInstanceInstallSurface();
        RaiseInstallWorkflowProperties();
    }

    private void ClearInstanceInstallOption(string optionTitle)
    {
        _instanceInstallSelections[optionTitle] = FrontendEditableInstallSelection.Cleared;
        if (ManagedPrimaryInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
        {
            foreach (var title in ManagedPrimaryDependentInstallTitles)
            {
                _instanceInstallSelections[title] = FrontendEditableInstallSelection.Cleared;
            }
        }
        else if (string.Equals(optionTitle, "OptiFine", StringComparison.Ordinal))
        {
            _instanceInstallSelections["OptiFabric"] = FrontendEditableInstallSelection.Cleared;
        }
        else if (ManagedApiInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
        {
            foreach (var title in ManagedApiInstallTitles.Where(title => !string.Equals(title, optionTitle, StringComparison.Ordinal)))
            {
                _instanceInstallSelections[title] = FrontendEditableInstallSelection.Cleared;
            }
        }

        switch (optionTitle)
        {
            case "Fabric":
                _instanceInstallAutoSelectedFabricApi = false;
                _instanceInstallAutoSelectedOptiFabric = false;
                break;
            case "Legacy Fabric":
                _instanceInstallAutoSelectedLegacyFabricApi = false;
                break;
            case "Quilt":
                _instanceInstallAutoSelectedQsl = false;
                break;
        }

        ResetInstanceInstallOptionBrowserState();
        InitializeInstanceInstallSurface();
        RaiseInstallWorkflowProperties();
    }

    private void ResetInstanceInstallOptionBrowserState()
    {
        _instanceInstallExpandedOptionTitle = null;
        _instanceInstallOptionChoices.Clear();
        _instanceInstallOptionLoadErrors.Clear();
        _instanceInstallOptionLoadsInProgress.Clear();
        _instanceInstallAutoSelectedFabricApi = false;
        _instanceInstallAutoSelectedLegacyFabricApi = false;
        _instanceInstallAutoSelectedQsl = false;
        _instanceInstallAutoSelectedOptiFabric = false;
    }
}
