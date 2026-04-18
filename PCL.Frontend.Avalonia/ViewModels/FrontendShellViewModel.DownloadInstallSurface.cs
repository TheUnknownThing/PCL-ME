using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly (string Title, string IconName)[] DownloadInstallOptionBlueprints =
    [
        ("Forge", "Anvil.png"),
        ("Cleanroom", "Cleanroom.png"),
        ("NeoForge", "NeoForge.png"),
        ("Fabric", "Fabric.png"),
        ("Legacy Fabric", "Fabric.png"),
        ("Fabric API", "Fabric.png"),
        ("Legacy Fabric API", "Fabric.png"),
        ("Quilt", "Quilt.png"),
        ("QFAPI / QSL", "Quilt.png"),
        ("LabyMod", "LabyMod.png"),
        ("OptiFine", "GrassPath.png"),
        ("OptiFabric", "OptiFabric.png"),
        ("LiteLoader", "Egg.png")
    ];

    private readonly Dictionary<string, IReadOnlyList<FrontendInstallChoice>> _downloadInstallOptionChoices = new(StringComparer.Ordinal);
    private readonly HashSet<string> _downloadInstallOptionLoadsInProgress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _downloadInstallOptionLoadErrors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _downloadInstallAutoSelectionSuppressedOptions = new(StringComparer.Ordinal);
    private IReadOnlyList<FrontendInstallChoice> _downloadInstallMinecraftCatalogChoices = [];
    private bool _downloadInstallMinecraftCatalogLoaded;
    private bool _isDownloadInstallMinecraftCatalogLoading;
    private int _downloadInstallMinecraftCatalogVersion;
    private LauncherFrontendSubpageKey? _downloadInstallLastVisitedSubpage;
    private bool _downloadInstallIsInSelectionStage;
    private string _downloadInstallMinecraftCatalogStatus = string.Empty;
    private string _downloadInstallNameValidationMessage = string.Empty;
    private string? _downloadInstallExpandedOptionTitle;
    private bool _downloadInstallIsUpdatingGeneratedName;
    private bool _downloadInstallIsNameEditedByUser;
    private bool _downloadInstallAutoSelectedFabricApi;
    private bool _downloadInstallAutoSelectedLegacyFabricApi;
    private bool _downloadInstallAutoSelectedQsl;
    private bool _downloadInstallAutoSelectedOptiFabric;

    public ActionCommand DownloadInstallBackCommand => new(ExitDownloadInstallSelectionStage);

    public bool ShowDownloadInstallMinecraftCatalog => !_downloadInstallIsInSelectionStage;

    public bool ShowDownloadInstallSelectionStage => _downloadInstallIsInSelectionStage;

    public bool ShowDownloadInstallMinecraftCatalogLoading => ShowDownloadInstallMinecraftCatalog && _isDownloadInstallMinecraftCatalogLoading;

    public bool ShowDownloadInstallMinecraftCatalogContent => ShowDownloadInstallMinecraftCatalog && !_isDownloadInstallMinecraftCatalogLoading && DownloadInstallMinecraftSections.Count > 0;

    public bool ShowDownloadInstallMinecraftCatalogEmptyState => ShowDownloadInstallMinecraftCatalog && !_isDownloadInstallMinecraftCatalogLoading && DownloadInstallMinecraftSections.Count == 0;

    public string DownloadInstallMinecraftCatalogStatus
    {
        get => _downloadInstallMinecraftCatalogStatus;
        private set
        {
            if (SetProperty(ref _downloadInstallMinecraftCatalogStatus, value))
            {
                RaisePropertyChanged(nameof(ShowDownloadInstallMinecraftCatalogLoading));
                RaisePropertyChanged(nameof(ShowDownloadInstallMinecraftCatalogContent));
                RaisePropertyChanged(nameof(ShowDownloadInstallMinecraftCatalogEmptyState));
            }
        }
    }

    public string DownloadInstallNameValidationMessage
    {
        get => _downloadInstallNameValidationMessage;
        private set
        {
            if (SetProperty(ref _downloadInstallNameValidationMessage, value))
            {
                RaisePropertyChanged(nameof(ShowDownloadInstallNameValidation));
                RaisePropertyChanged(nameof(CanStartDownloadInstall));
            }
        }
    }

    public bool ShowDownloadInstallNameValidation => !string.IsNullOrWhiteSpace(DownloadInstallNameValidationMessage);

    public bool CanStartDownloadInstall => _downloadInstallIsInSelectionStage && string.IsNullOrWhiteSpace(DownloadInstallNameValidationMessage);

    private bool IsDownloadClientInstallRoute =>
        _currentRoute.Page == LauncherFrontendPageKey.Download
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadClient;

    private void RefreshDownloadInstallSurfaceState()
    {
        ReplaceItems(DownloadInstallOptions, []);
        EnsureDownloadInstallEditableState();

        if (_downloadInstallIsInSelectionStage)
        {
            SyncDownloadInstallSelectionHeader();
            if (IsDownloadClientInstallRoute)
            {
                ReplaceItems(DownloadInstallHints, []);
                ReplaceItems(DownloadInstallOptionCards, []);
            }
            else
            {
                ReplaceItems(
                    DownloadInstallHints,
                    GetEffectiveInstallHints(isExistingInstance: false).Select(hint =>
                        CreateDangerNoticeStrip(hint)));
                SyncDownloadInstallOptionCards(BuildDownloadInstallOptionCards());
            }
        }
        else
        {
            DownloadInstallMinecraftVersion = "Minecraft";
            DownloadInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", "Grass.png");
            ReplaceItems(DownloadInstallHints, []);
            ReplaceItems(DownloadInstallOptionCards, []);
            EnsureDownloadInstallMinecraftCatalogLoaded();
        }

        ValidateDownloadInstallName();
        RaiseDownloadInstallSurfaceProperties();
    }

    private void SyncDownloadInstallRouteState()
    {
        if (_downloadInstallLastVisitedSubpage == _currentRoute.Subpage)
        {
            return;
        }

        _downloadInstallLastVisitedSubpage = _currentRoute.Subpage;
        if (!IsDownloadClientInstallRoute)
        {
            return;
        }

        _downloadInstallIsInSelectionStage = false;
        _downloadInstallExpandedOptionTitle = null;
        _downloadInstallMinecraftChoice = null;
        _downloadInstallIsNameEditedByUser = false;
        _downloadInstallOptionChoices.Clear();
        _downloadInstallOptionLoadsInProgress.Clear();
        _downloadInstallOptionLoadErrors.Clear();
        _downloadInstallAutoSelectedFabricApi = false;
        _downloadInstallAutoSelectedLegacyFabricApi = false;
        _downloadInstallAutoSelectedQsl = false;
        _downloadInstallAutoSelectedOptiFabric = false;
    }

    private void RaiseDownloadInstallSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowDownloadInstallMinecraftCatalog));
        RaisePropertyChanged(nameof(ShowDownloadInstallSelectionStage));
        RaisePropertyChanged(nameof(ShowDownloadInstallMinecraftCatalogLoading));
        RaisePropertyChanged(nameof(ShowDownloadInstallMinecraftCatalogContent));
        RaisePropertyChanged(nameof(ShowDownloadInstallMinecraftCatalogEmptyState));
        RaisePropertyChanged(nameof(ShowDownloadInstallNameValidation));
        RaisePropertyChanged(nameof(CanStartDownloadInstall));
    }

    private void EnsureDownloadInstallMinecraftCatalogLoaded(bool forceReload = false)
    {
        if (_isDownloadInstallMinecraftCatalogLoading)
        {
            return;
        }

        if (_downloadInstallMinecraftCatalogLoaded && !forceReload)
        {
            ApplyDownloadInstallMinecraftCatalogSections();
            return;
        }

        _downloadInstallMinecraftCatalogLoaded = false;
        _isDownloadInstallMinecraftCatalogLoading = true;
        DownloadInstallMinecraftCatalogStatus = T("download.install.catalog.loading");
        RaiseDownloadInstallSurfaceProperties();

        var refreshVersion = ++_downloadInstallMinecraftCatalogVersion;
        var preferredVersion = _instanceComposition.Install.MinecraftVersion.Replace("Minecraft ", string.Empty, StringComparison.Ordinal);

        _ = Task.Run(() => FrontendInstallWorkflowService.GetMinecraftCatalogChoices(preferredVersion, SelectedDownloadSourceIndex, _i18n))
            .ContinueWith(async task =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (refreshVersion != _downloadInstallMinecraftCatalogVersion)
                    {
                        return;
                    }

                    _isDownloadInstallMinecraftCatalogLoading = false;
                    if (task.IsFaulted)
                    {
                        _downloadInstallMinecraftCatalogChoices = [];
                        DownloadInstallMinecraftCatalogStatus = task.Exception?.GetBaseException().Message ?? T("download.install.catalog.load_failed");
                    }
                    else
                    {
                        _downloadInstallMinecraftCatalogChoices = task.Result;
                        _downloadInstallMinecraftCatalogLoaded = true;
                        ApplyDownloadInstallMinecraftCatalogSections();
                        if (_downloadInstallMinecraftCatalogChoices.Count == 0)
                        {
                            DownloadInstallMinecraftCatalogStatus = T("download.install.catalog.empty");
                        }
                    }

                    RaiseDownloadInstallSurfaceProperties();
                });
            }, TaskScheduler.Default);
    }

    private void ApplyDownloadInstallMinecraftCatalogSections()
    {
        ReplaceItems(DownloadInstallMinecraftSections, BuildDownloadInstallMinecraftSections());
        RaiseDownloadInstallSurfaceProperties();
    }

    private IReadOnlyList<DownloadInstallMinecraftSectionViewModel> BuildDownloadInstallMinecraftSections()
    {
        if (_downloadInstallMinecraftCatalogChoices.Count == 0)
        {
            return [];
        }

        string GetLocalizedGroupTitle(string group)
        {
            return group switch
            {
                "release" => T("download.install.catalog.groups.release"),
                "preview" => T("download.install.catalog.groups.preview"),
                "legacy" => T("download.install.catalog.groups.legacy"),
                "april_fools" => T("download.install.catalog.groups.april_fools"),
                _ => group
            };
        }

        var choicesByGroup = _downloadInstallMinecraftCatalogChoices
            .GroupBy(choice => choice.Metadata?["catalogGroup"]?.GetValue<string>() ?? "release")
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(GetDownloadInstallChoiceReleaseTime)
                    .ToArray(),
                StringComparer.Ordinal);

        var result = new List<DownloadInstallMinecraftSectionViewModel>();
        var latestEntries = BuildDownloadInstallLatestMinecraftChoices(choicesByGroup);
        if (latestEntries.Count > 0)
        {
            result.Add(new DownloadInstallMinecraftSectionViewModel(
                T("download.install.catalog.groups.latest"),
                latestEntries,
                isExpanded: true,
                canCollapse: false,
                toggleCommand: new ActionCommand(() => { })));
        }

        foreach (var groupTitle in new[] { "release", "preview", "legacy", "april_fools" })
        {
            if (!choicesByGroup.TryGetValue(groupTitle, out var choices) || choices.Length == 0)
            {
                continue;
            }

            var sectionChoices = choices.Select(choice => CreateDownloadInstallMinecraftChoice(choice)).ToArray();
            result.Add(new DownloadInstallMinecraftSectionViewModel(
                $"{GetLocalizedGroupTitle(groupTitle)} ({sectionChoices.Length})",
                sectionChoices,
                isExpanded: false,
                canCollapse: true,
                toggleCommand: new ActionCommand(() => ToggleDownloadInstallMinecraftSection(GetLocalizedGroupTitle(groupTitle)))));
        }

        return result;
    }

    private IReadOnlyList<DownloadInstallMinecraftChoiceViewModel> BuildDownloadInstallLatestMinecraftChoices(
        IReadOnlyDictionary<string, FrontendInstallChoice[]> choicesByGroup)
    {
        var latest = new List<DownloadInstallMinecraftChoiceViewModel>();
        if (choicesByGroup.TryGetValue("release", out var releases) && releases.Length > 0)
        {
            latest.Add(CreateDownloadInstallMinecraftChoice(
                releases[0],
                summaryOverride: BuildLatestMinecraftSummary(releases[0], T("download.install.catalog.latest.release"))));
        }

        if (choicesByGroup.TryGetValue("preview", out var previews) && previews.Length > 0)
        {
            var latestPreview = previews[0];
            var latestReleaseTime = releases is null || releases.Length == 0
                ? DateTimeOffset.MinValue
                : GetDownloadInstallChoiceReleaseTime(releases[0]);
            var latestPreviewTime = GetDownloadInstallChoiceReleaseTime(latestPreview);
            if (latestPreviewTime > latestReleaseTime)
            {
                latest.Add(CreateDownloadInstallMinecraftChoice(
                    latestPreview,
                    summaryOverride: BuildLatestMinecraftSummary(latestPreview, T("download.install.catalog.latest.preview"))));
            }
        }

        return latest;
    }

    private string BuildLatestMinecraftSummary(FrontendInstallChoice choice, string label)
    {
        var releaseTime = GetDownloadInstallChoiceReleaseTime(choice);
        return releaseTime is null
            ? label
            : T("download.install.catalog.latest.published_at", ("label", label), ("published_at", releaseTime.Value.LocalDateTime.ToString("yyyy/MM/dd HH:mm")));
    }

    private static DateTimeOffset? GetDownloadInstallChoiceReleaseTime(FrontendInstallChoice choice)
    {
        var rawValue = choice.Metadata?["releaseTime"]?.GetValue<string>();
        return DateTimeOffset.TryParse(rawValue, out var parsed) ? parsed : null;
    }

    private DownloadInstallMinecraftChoiceViewModel CreateDownloadInstallMinecraftChoice(
        FrontendInstallChoice choice,
        string? summaryOverride = null)
    {
        var iconName = choice.Metadata?["iconName"]?.GetValue<string>() ?? "Grass.png";
        return new DownloadInstallMinecraftChoiceViewModel(
            choice.Title,
            summaryOverride ?? choice.Summary,
            LoadLauncherBitmap("Images", "Blocks", iconName),
            new ActionCommand(() => EnterDownloadInstallSelectionStage(choice)));
    }

    private void ToggleDownloadInstallMinecraftSection(string groupTitle)
    {
        var normalizedTitle = NormalizeDownloadInstallSectionTitle(groupTitle);
        var section = DownloadInstallMinecraftSections.FirstOrDefault(existing =>
            existing.CanCollapse
            && string.Equals(NormalizeDownloadInstallSectionTitle(existing.Title), normalizedTitle, StringComparison.Ordinal));
        if (section is null)
        {
            return;
        }

        section.IsExpanded = !section.IsExpanded;
    }

    private static string NormalizeDownloadInstallSectionTitle(string title)
    {
        var index = title.IndexOf(' ');
        return index <= 0 ? title : title[..index];
    }

    private void EnterDownloadInstallSelectionStage(FrontendInstallChoice choice)
    {
        ResetDownloadInstallEditableSelections(choice);
        _downloadInstallIsInSelectionStage = true;
        RefreshDownloadInstallSurfaceState();
    }

    private void ExitDownloadInstallSelectionStage()
    {
        _downloadInstallIsInSelectionStage = false;
        _downloadInstallExpandedOptionTitle = null;
        ReplaceItems(DownloadInstallHints, []);
        ReplaceItems(DownloadInstallOptionCards, []);
        RaiseDownloadInstallSurfaceProperties();
    }

    private void ResetDownloadInstallEditableSelections(FrontendInstallChoice minecraftChoice)
    {
        _downloadInstallMinecraftChoice = minecraftChoice;
        _downloadInstallBaselineMinecraftVersion = $"Minecraft {minecraftChoice.Version}";
        _downloadInstallSelections.Clear();
        _downloadInstallBaselineSelections.Clear();
        foreach (var (title, _) in DownloadInstallOptionBlueprints)
        {
            _downloadInstallSelections[title] = FrontendEditableInstallSelection.Unchanged;
            _downloadInstallBaselineSelections[title] = CreateAvailableSelectionState();
        }

        _downloadInstallSeedSignature = $"download-install:{minecraftChoice.Version}";
        _downloadInstallExpandedOptionTitle = null;
        _downloadInstallOptionChoices.Clear();
        _downloadInstallOptionLoadsInProgress.Clear();
        _downloadInstallOptionLoadErrors.Clear();
        _downloadInstallAutoSelectedFabricApi = false;
        _downloadInstallAutoSelectedLegacyFabricApi = false;
        _downloadInstallAutoSelectedQsl = false;
        _downloadInstallAutoSelectedOptiFabric = false;
        _downloadInstallAutoSelectionSuppressedOptions.Clear();
        _downloadInstallIsNameEditedByUser = false;
        UpdateGeneratedDownloadInstallName(force: true);
    }

    private void SyncDownloadInstallSelectionHeader()
    {
        if (_downloadInstallMinecraftChoice is null)
        {
            DownloadInstallMinecraftVersion = "Minecraft";
            DownloadInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", "Grass.png");
            return;
        }

        DownloadInstallMinecraftVersion = _downloadInstallMinecraftChoice.Title;
        DownloadInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", GetDownloadInstallSelectionIconName());
        UpdateGeneratedDownloadInstallName(force: false);
    }

    private IReadOnlyList<DownloadInstallOptionCardViewModel> BuildDownloadInstallOptionCards()
    {
        if (_downloadInstallMinecraftChoice is null)
        {
            return [];
        }

        var minecraftVersion = _downloadInstallMinecraftChoice.Version;
        var cards = new List<DownloadInstallOptionCardViewModel>();
        foreach (var (title, iconName) in DownloadInstallOptionBlueprints)
        {
            if (!ShouldShowInstallOption(title, minecraftVersion))
            {
                continue;
            }

            cards.Add(CreateDownloadInstallOptionCard(title, iconName, minecraftVersion));
        }

        return cards;
    }

    private bool ShouldShowInstallOption(string optionTitle, string minecraftVersion)
    {
        return optionTitle switch
        {
            "Cleanroom" => string.Equals(minecraftVersion, "1.12.2", StringComparison.OrdinalIgnoreCase),
            "NeoForge" => IsVersionAtLeast(minecraftVersion, "1.20.1"),
            "Quilt" or "QFAPI / QSL" => !IgnoreQuiltLoader,
            "LiteLoader" => !IsVersionAtLeast(minecraftVersion, "1.13"),
            _ => true
        };
    }

    private DownloadInstallOptionCardViewModel CreateDownloadInstallOptionCard(string optionTitle, string iconName, string minecraftVersion)
    {
        var optionIcon = LoadLauncherBitmap("Images", "Blocks", iconName);
        var effectiveChoice = ResolveEffectiveChoice(isExistingInstance: false, optionTitle, minecraftVersion);
        var staticUnavailableReason = GetInstallOptionStaticUnavailableReason(isExistingInstance: false, optionTitle, minecraftVersion);
        var loadError = _downloadInstallOptionLoadErrors.TryGetValue(optionTitle, out var loadErrorText)
            ? loadErrorText
            : null;
        var hasLoadedChoices = _downloadInstallOptionChoices.TryGetValue(optionTitle, out var availableChoices);
        var cachedChoices = hasLoadedChoices
            ? availableChoices!
            : [];
        var unavailableReason = loadError
            ?? staticUnavailableReason
            ?? (hasLoadedChoices
                ? GetInstallOptionUnavailableReason(isExistingInstance: false, optionTitle, minecraftVersion, cachedChoices)
                : null);
        var selectionText = effectiveChoice?.Title
                            ?? (unavailableReason is null ? T("download.install.options.available") : unavailableReason);
        var canExpand = unavailableReason is null;
        var isExpanded = canExpand && string.Equals(_downloadInstallExpandedOptionTitle, optionTitle, StringComparison.Ordinal);
        var choiceItems = cachedChoices.Select(choice => new DownloadInstallChoiceItemViewModel(
            choice.Title,
            choice.Summary,
            optionIcon,
            effectiveChoice is not null && string.Equals(choice.Id, effectiveChoice.Id, StringComparison.Ordinal),
            new ActionCommand(() => SelectDownloadInstallOption(optionTitle, choice)))).ToArray();

        return new DownloadInstallOptionCardViewModel(
            optionTitle,
            selectionText,
            effectiveChoice is null ? null : optionIcon,
            effectiveChoice is not null,
            effectiveChoice is null,
            canExpand,
            isExpanded,
            _downloadInstallOptionLoadsInProgress.Contains(optionTitle),
            _downloadInstallOptionLoadsInProgress.Contains(optionTitle) ? T("download.install.catalog.loading") : string.Empty,
            canExpand && !_downloadInstallOptionLoadsInProgress.Contains(optionTitle) && choiceItems.Length == 0 && isExpanded,
            canExpand ? string.Empty : selectionText,
            T("download.install.options.search.watermark"),
            query => T("download.install.options.search.no_results", ("query", query)),
            choiceItems,
            CanClearInstallSelection(isExistingInstance: false, optionTitle),
            DownloadInstallClearSelectionText,
            new ActionCommand(() => ToggleDownloadInstallOptionCard(optionTitle)),
            new ActionCommand(() => ClearDownloadInstallOption(optionTitle)));
    }

    private void SyncDownloadInstallOptionCards(IReadOnlyList<DownloadInstallOptionCardViewModel> cards)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            var desiredCard = cards[index];
            var existingIndex = FindDownloadInstallOptionCardIndex(desiredCard.Title);
            if (existingIndex >= 0)
            {
                var existingCard = DownloadInstallOptionCards[existingIndex];
                existingCard.UpdateFrom(desiredCard);
                if (existingIndex != index)
                {
                    DownloadInstallOptionCards.Move(existingIndex, index);
                }

                continue;
            }

            DownloadInstallOptionCards.Insert(index, desiredCard);
        }

        while (DownloadInstallOptionCards.Count > cards.Count)
        {
            DownloadInstallOptionCards.RemoveAt(DownloadInstallOptionCards.Count - 1);
        }
    }

    private int FindDownloadInstallOptionCardIndex(string title)
    {
        for (var index = 0; index < DownloadInstallOptionCards.Count; index++)
        {
            if (string.Equals(DownloadInstallOptionCards[index].Title, title, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void ToggleDownloadInstallOptionCard(string optionTitle)
    {
        if (_downloadInstallMinecraftChoice is null)
        {
            return;
        }

        var minecraftVersion = _downloadInstallMinecraftChoice.Version;
        var staticUnavailableReason = GetInstallOptionStaticUnavailableReason(isExistingInstance: false, optionTitle, minecraftVersion);
        if (staticUnavailableReason is not null)
        {
            return;
        }

        var nextTitle = string.Equals(_downloadInstallExpandedOptionTitle, optionTitle, StringComparison.Ordinal)
            ? null
            : optionTitle;
        _downloadInstallExpandedOptionTitle = nextTitle;
        RefreshDownloadInstallSurfaceState();

        if (nextTitle is not null)
        {
            EnsureDownloadInstallOptionChoicesLoaded(optionTitle, minecraftVersion);
        }
    }

    private void EnsureDownloadInstallOptionChoicesLoaded(string optionTitle, string minecraftVersion)
    {
        if (_downloadInstallOptionChoices.ContainsKey(optionTitle) || _downloadInstallOptionLoadsInProgress.Contains(optionTitle))
        {
            return;
        }

        _downloadInstallOptionLoadsInProgress.Add(optionTitle);
        _downloadInstallOptionLoadErrors.Remove(optionTitle);
        RefreshDownloadInstallSurfaceState();

        var versionSignature = minecraftVersion;
        _ = Task.Run(() => GetSelectableInstallChoices(isExistingInstance: false, optionTitle, versionSignature))
            .ContinueWith(async task =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _downloadInstallOptionLoadsInProgress.Remove(optionTitle);
                    if (!string.Equals(_downloadInstallMinecraftChoice?.Version, versionSignature, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        _downloadInstallOptionLoadErrors[optionTitle] = task.Exception?.GetBaseException().Message ?? T("download.install.catalog.load_failed");
                    }
                    else
                    {
                        _downloadInstallOptionChoices[optionTitle] = task.Result;
                        TryApplyDownloadInstallAutoSelection(optionTitle, task.Result, versionSignature);
                    }

                    RefreshDownloadInstallSurfaceState();
                });
            }, TaskScheduler.Default);
    }

    private void TryApplyDownloadInstallAutoSelection(
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
            if (_downloadInstallAutoSelectedQsl
                || _downloadInstallAutoSelectionSuppressedOptions.Contains(optionTitle)
                || !HasInstallSelection(isExistingInstance: false, "Quilt"))
            {
                return;
            }

            _downloadInstallAutoSelectedQsl = true;
            _downloadInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choices[0], false);
            return;
        }

        if (string.Equals(optionTitle, "Fabric API", StringComparison.Ordinal))
        {
            var hasLoadedQslChoices = _downloadInstallOptionChoices.ContainsKey("QFAPI / QSL");
            var shouldAutoSelect = HasInstallSelection(isExistingInstance: false, "Fabric")
                                   || (HasInstallSelection(isExistingInstance: false, "Quilt")
                                       && hasLoadedQslChoices
                                       && GetInstallOptionUnavailableReason(false, "QFAPI / QSL", minecraftVersion, ResolveCachedDownloadInstallChoices("QFAPI / QSL")) == T("download.install.options.unavailable"));
            if (_downloadInstallAutoSelectedFabricApi
                || _downloadInstallAutoSelectionSuppressedOptions.Contains(optionTitle)
                || !shouldAutoSelect)
            {
                return;
            }

            _downloadInstallAutoSelectedFabricApi = true;
            _downloadInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choices[0], false);
            return;
        }

        if (string.Equals(optionTitle, "Legacy Fabric API", StringComparison.Ordinal))
        {
            var hasLoadedQslChoices = _downloadInstallOptionChoices.ContainsKey("QFAPI / QSL");
            var shouldAutoSelect = HasInstallSelection(isExistingInstance: false, "Legacy Fabric")
                                   || (HasInstallSelection(isExistingInstance: false, "Quilt")
                                       && hasLoadedQslChoices
                                       && GetInstallOptionUnavailableReason(false, "QFAPI / QSL", minecraftVersion, ResolveCachedDownloadInstallChoices("QFAPI / QSL")) == T("download.install.options.unavailable"));
            if (_downloadInstallAutoSelectedLegacyFabricApi
                || _downloadInstallAutoSelectionSuppressedOptions.Contains(optionTitle)
                || !shouldAutoSelect)
            {
                return;
            }

            _downloadInstallAutoSelectedLegacyFabricApi = true;
            _downloadInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choices[0], false);
            return;
        }

        if (string.Equals(optionTitle, "OptiFabric", StringComparison.Ordinal))
        {
            if (_downloadInstallAutoSelectedOptiFabric
                || _downloadInstallAutoSelectionSuppressedOptions.Contains(optionTitle)
                || !HasInstallSelection(isExistingInstance: false, "Fabric")
                || !HasInstallSelection(isExistingInstance: false, "OptiFine")
                || IsOptiFabricOriginsOnlyVersion(minecraftVersion))
            {
                return;
            }

            _downloadInstallAutoSelectedOptiFabric = true;
            _downloadInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choices[0], false);
        }
    }

    private IReadOnlyList<FrontendInstallChoice> ResolveCachedDownloadInstallChoices(string optionTitle)
    {
        return _downloadInstallOptionChoices.TryGetValue(optionTitle, out var choices) ? choices : [];
    }

    private void SelectDownloadInstallOption(string optionTitle, FrontendInstallChoice choice)
    {
        var minecraftVersion = _downloadInstallMinecraftChoice?.Version;
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return;
        }

        _downloadInstallAutoSelectionSuppressedOptions.Remove(optionTitle);
        _downloadInstallSelections[optionTitle] = new FrontendEditableInstallSelection(choice, false);

        if (ManagedPrimaryInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
        {
            foreach (var title in ManagedPrimaryInstallTitles.Where(title => !string.Equals(title, optionTitle, StringComparison.Ordinal)))
            {
                _downloadInstallSelections[title] = FrontendEditableInstallSelection.Cleared;
            }

            if (!string.Equals(optionTitle, "Quilt", StringComparison.Ordinal))
            {
                _downloadInstallSelections["QFAPI / QSL"] = FrontendEditableInstallSelection.Cleared;
                _downloadInstallAutoSelectedQsl = false;
            }
            else
            {
                _downloadInstallSelections["Legacy Fabric API"] = FrontendEditableInstallSelection.Cleared;
                _downloadInstallAutoSelectedLegacyFabricApi = false;
            }

            if (!string.Equals(optionTitle, "Fabric", StringComparison.Ordinal))
            {
                _downloadInstallSelections["Fabric API"] = FrontendEditableInstallSelection.Cleared;
                _downloadInstallSelections["OptiFabric"] = FrontendEditableInstallSelection.Cleared;
                _downloadInstallAutoSelectedFabricApi = false;
                _downloadInstallAutoSelectedOptiFabric = false;
            }

            if (!string.Equals(optionTitle, "Legacy Fabric", StringComparison.Ordinal))
            {
                _downloadInstallSelections["Legacy Fabric API"] = FrontendEditableInstallSelection.Cleared;
                _downloadInstallAutoSelectedLegacyFabricApi = false;
            }
        }
        else if (ManagedApiInstallTitles.Contains(optionTitle, StringComparer.Ordinal))
        {
            foreach (var title in ManagedApiInstallTitles.Where(title => !string.Equals(title, optionTitle, StringComparison.Ordinal)))
            {
                _downloadInstallSelections[title] = FrontendEditableInstallSelection.Cleared;
            }
        }
        else if (string.Equals(optionTitle, "OptiFine", StringComparison.Ordinal)
                 && !string.Equals(GetCurrentPrimaryInstallTitle(isExistingInstance: false), "Fabric", StringComparison.Ordinal))
        {
            _downloadInstallSelections["OptiFabric"] = FrontendEditableInstallSelection.Cleared;
            _downloadInstallAutoSelectedOptiFabric = false;
        }

        _downloadInstallExpandedOptionTitle = null;
        _downloadInstallOptionChoices.Clear();
        _downloadInstallOptionLoadErrors.Clear();
        _downloadInstallOptionLoadsInProgress.Clear();
        UpdateGeneratedDownloadInstallName(force: false);
        RefreshDownloadInstallSurfaceState();
    }

    private void ClearDownloadInstallOption(string optionTitle)
    {
        if (IsAutoSelectableInstallOption(optionTitle))
        {
            _downloadInstallAutoSelectionSuppressedOptions.Add(optionTitle);
        }

        ClearInstallOption(isExistingInstance: false, optionTitle);
        switch (optionTitle)
        {
            case "Fabric":
                _downloadInstallAutoSelectedFabricApi = false;
                _downloadInstallAutoSelectedOptiFabric = false;
                break;
            case "Legacy Fabric":
                _downloadInstallAutoSelectedLegacyFabricApi = false;
                break;
            case "Quilt":
                _downloadInstallAutoSelectedQsl = false;
                break;
        }

        _downloadInstallExpandedOptionTitle = null;
        _downloadInstallOptionChoices.Clear();
        _downloadInstallOptionLoadErrors.Clear();
        _downloadInstallOptionLoadsInProgress.Clear();
        UpdateGeneratedDownloadInstallName(force: false);
        RefreshDownloadInstallSurfaceState();
    }

    private void UpdateGeneratedDownloadInstallName(bool force)
    {
        if (!force && _downloadInstallIsNameEditedByUser)
        {
            return;
        }

        _downloadInstallIsUpdatingGeneratedName = true;
        DownloadInstallName = BuildGeneratedDownloadInstallName();
        _downloadInstallIsUpdatingGeneratedName = false;
    }

    private string BuildGeneratedDownloadInstallName()
    {
        var minecraftVersion = _downloadInstallMinecraftChoice?.Version;
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return T("download.install.generated_name.default");
        }

        var name = minecraftVersion;
        AppendGeneratedSuffix(ref name, "Fabric", "-Fabric_", removePrefix: false, normalize: static text => text.Replace("+build", string.Empty, StringComparison.Ordinal));
        AppendGeneratedSuffix(ref name, "Legacy Fabric", "-LegacyFabric_");
        AppendGeneratedSuffix(ref name, "Quilt", "-Quilt_");
        AppendGeneratedSuffix(ref name, "LabyMod", "-LabyMod_", normalize: static text => text.Replace(" \u7A33\u5B9A\u7248", "_Production", StringComparison.Ordinal).Replace(" \u5FEB\u7167\u7248", "_Snapshot", StringComparison.Ordinal));
        AppendGeneratedSuffix(ref name, "Forge", "-Forge_");
        AppendGeneratedSuffix(ref name, "NeoForge", "-NeoForge_");
        AppendGeneratedSuffix(ref name, "Cleanroom", "-Cleanroom_");
        if (ResolveEffectiveChoice(false, "LiteLoader", minecraftVersion) is not null)
        {
            name += "-LiteLoader";
        }

        var optiFineChoice = ResolveEffectiveChoice(false, "OptiFine", minecraftVersion);
        if (optiFineChoice is not null)
        {
            var suffix = optiFineChoice.Title.Replace(_downloadInstallMinecraftChoice?.Title + " ", string.Empty, StringComparison.Ordinal).Replace(' ', '_');
            name += $"-OptiFine_{suffix}";
        }

        return name;
    }

    private void AppendGeneratedSuffix(ref string baseName, string optionTitle, string prefix, bool removePrefix = false, Func<string, string>? normalize = null)
    {
        var minecraftVersion = _downloadInstallMinecraftChoice?.Version;
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return;
        }

        var choice = ResolveEffectiveChoice(false, optionTitle, minecraftVersion);
        if (choice is null)
        {
            return;
        }

        var suffix = choice.Title;
        if (removePrefix && suffix.StartsWith(minecraftVersion + " ", StringComparison.Ordinal))
        {
            suffix = suffix[(minecraftVersion.Length + 1)..];
        }

        suffix = normalize is null ? suffix : normalize(suffix);
        baseName += prefix + suffix.Replace(' ', '_');
    }

    private string GetDownloadInstallSelectionIconName()
    {
        var minecraftVersion = _downloadInstallMinecraftChoice?.Version;
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return "Grass.png";
        }

        if (ResolveEffectiveChoice(false, "Fabric", minecraftVersion) is not null
            || ResolveEffectiveChoice(false, "Legacy Fabric", minecraftVersion) is not null)
        {
            return "Fabric.png";
        }

        if (ResolveEffectiveChoice(false, "Forge", minecraftVersion) is not null)
        {
            return "Anvil.png";
        }

        if (ResolveEffectiveChoice(false, "NeoForge", minecraftVersion) is not null)
        {
            return "NeoForge.png";
        }

        if (ResolveEffectiveChoice(false, "LiteLoader", minecraftVersion) is not null)
        {
            return "Egg.png";
        }

        if (ResolveEffectiveChoice(false, "OptiFine", minecraftVersion) is not null)
        {
            return "GrassPath.png";
        }

        if (ResolveEffectiveChoice(false, "Quilt", minecraftVersion) is not null)
        {
            return "Quilt.png";
        }

        if (ResolveEffectiveChoice(false, "Cleanroom", minecraftVersion) is not null)
        {
            return "Cleanroom.png";
        }

        if (ResolveEffectiveChoice(false, "LabyMod", minecraftVersion) is not null)
        {
            return "LabyMod.png";
        }

        return _downloadInstallMinecraftChoice?.Metadata?["iconName"]?.GetValue<string>() ?? "Grass.png";
    }

    private void OnDownloadInstallNameChanged()
    {
        if (_downloadInstallIsInSelectionStage && !_downloadInstallIsUpdatingGeneratedName)
        {
            _downloadInstallIsNameEditedByUser = true;
        }

        ValidateDownloadInstallName();
    }

    private void ValidateDownloadInstallName()
    {
        if (!_downloadInstallIsInSelectionStage)
        {
            DownloadInstallNameValidationMessage = string.Empty;
            return;
        }

        var trimmedName = DownloadInstallName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            DownloadInstallNameValidationMessage = T("download.install.validation.empty_name");
            return;
        }

        if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            DownloadInstallNameValidationMessage = T("download.install.validation.invalid_characters");
            return;
        }

        if (trimmedName.EndsWith(".", StringComparison.Ordinal) || trimmedName.EndsWith(" ", StringComparison.Ordinal))
        {
            DownloadInstallNameValidationMessage = T("download.install.validation.invalid_ending");
            return;
        }

        var launcherDirectory = _instanceComposition.Selection.LauncherDirectory;
        if (!string.IsNullOrWhiteSpace(launcherDirectory))
        {
            var targetDirectory = Path.Combine(launcherDirectory, "versions", trimmedName);
            if (Directory.Exists(targetDirectory))
            {
                DownloadInstallNameValidationMessage = T("download.install.validation.already_exists");
                return;
            }
        }

        DownloadInstallNameValidationMessage = string.Empty;
    }
}
