using Avalonia.Media.Imaging;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private DownloadInstallOptionViewModel CreateInstallOptionViewModel(bool isExistingInstance, string optionTitle, string? iconName)
    {
        var presentation = GetInstallOptionPresentation(isExistingInstance, optionTitle);
        return CreateDownloadInstallOption(
            optionTitle,
            GetEffectiveSelectionText(isExistingInstance, optionTitle),
            string.IsNullOrWhiteSpace(iconName) ? null : LoadLauncherBitmap("Images", "Blocks", iconName),
            presentation.DetailText,
            presentation.SelectText,
            presentation.CanSelect,
            new ActionCommand(() => _ = EditInstallOptionAsync(isExistingInstance, optionTitle)),
            CanClearInstallSelection(isExistingInstance, optionTitle),
            new ActionCommand(() => ClearInstallOption(isExistingInstance, optionTitle)));
    }

    private InstallOptionPresentation GetInstallOptionPresentation(bool isExistingInstance, string optionTitle)
    {
        var minecraftVersion = GetEffectiveMinecraftVersion(isExistingInstance).Replace("Minecraft ", string.Empty, StringComparison.Ordinal);
        if (!FrontendInstallWorkflowService.IsFrontendManagedOption(optionTitle))
        {
            return new InstallOptionPresentation(
                SD("instance.install.option.unsupported"),
                SD("instance.install.option.unavailable"),
                false);
        }

        var unavailableReason = GetInstallOptionStaticUnavailableReason(isExistingInstance, optionTitle, minecraftVersion);
        if (unavailableReason is not null)
        {
            return new InstallOptionPresentation(
                BuildInstallOptionUnavailableDetail(optionTitle, minecraftVersion, unavailableReason),
                SD("instance.install.option.unavailable"),
                false);
        }

        return new InstallOptionPresentation(
            BuildInstallOptionPreviewDetail(isExistingInstance, optionTitle, minecraftVersion),
            ResolveInstallOptionPreviewSelectText(isExistingInstance, optionTitle),
            true);
    }

    private IReadOnlyList<string> GetEffectiveInstallHints(bool isExistingInstance)
    {
        var hints = new List<string>();
        if (HasInstallMinecraftVersionChanged(isExistingInstance))
        {
            hints.Add(SD(
                "instance.install.hints.minecraft_changed",
                ("version", GetEffectiveMinecraftVersion(isExistingInstance))));
        }

        if (HasInstallSelection(isExistingInstance, "Fabric")
            && !HasInstallSelection(isExistingInstance, "Fabric API"))
        {
            hints.Add(SD("instance.install.hints.fabric_api_missing"));
        }

        if (HasInstallSelection(isExistingInstance, "Quilt")
            && !HasInstallSelection(isExistingInstance, "QFAPI / QSL")
            && !HasInstallSelection(isExistingInstance, "Fabric API"))
        {
            hints.Add(SD("instance.install.hints.qsl_missing"));
        }

        if ((HasInstallSelection(isExistingInstance, "Fabric") || HasInstallSelection(isExistingInstance, "Legacy Fabric"))
            && HasInstallSelection(isExistingInstance, "OptiFine")
            && !HasInstallSelection(isExistingInstance, "OptiFabric"))
        {
            if (IsOptiFabricOriginsOnlyVersion(GetEffectiveMinecraftVersion(isExistingInstance)))
            {
                hints.Add(SD("instance.install.hints.optifabric_origins"));
            }
            else if (HasInstallSelection(isExistingInstance, "Legacy Fabric"))
            {
                hints.Add(SD("instance.install.hints.legacy_optifabric"));
            }
            else
            {
                hints.Add(SD("instance.install.hints.optifabric"));
            }
        }

        if (HasInstallSelection(isExistingInstance, "OptiFine")
            && IsVersionGreaterThan(GetEffectiveMinecraftVersion(isExistingInstance), "Minecraft 1.20.4")
            && (HasInstallSelection(isExistingInstance, "Forge") || HasInstallSelection(isExistingInstance, "Fabric")))
        {
            hints.Add(SD("instance.install.hints.optifine_mod_compatibility"));
        }

        return hints;
    }

    private string BuildInstallOptionPreviewDetail(
        bool isExistingInstance,
        string optionTitle,
        string minecraftVersion)
    {
        var selectionText = GetEffectiveSelectionText(isExistingInstance, optionTitle);
        var parts = new List<string>();

        if (string.IsNullOrWhiteSpace(selectionText)
            || string.Equals(selectionText, SD("instance.common.not_installed"), StringComparison.Ordinal)
            || string.Equals(selectionText, SD("instance.install.option.can_add"), StringComparison.Ordinal))
        {
            parts.Add(SD("instance.install.option.preview_load_candidates", ("version", minecraftVersion)));
        }
        else
        {
            parts.Add(SD("instance.install.option.preview_current_selection", ("selection", selectionText), ("version", minecraftVersion)));
        }

        parts.Add(GetInstallOptionRuleHint(optionTitle, minecraftVersion));
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string BuildInstallOptionAvailableDetail(
        bool isExistingInstance,
        string optionTitle,
        string minecraftVersion,
        IReadOnlyList<FrontendInstallChoice> selectableChoices)
    {
        var effectiveChoice = ResolveEffectiveChoice(isExistingInstance, optionTitle, minecraftVersion);
        var recommendedChoice = selectableChoices[0];
        var parts = new List<string>();

        if (effectiveChoice is null)
        {
            parts.Add(SD("instance.install.option.recommended", ("title", recommendedChoice.Title)));
        }
        else if (string.Equals(effectiveChoice.Id, recommendedChoice.Id, StringComparison.Ordinal))
        {
            parts.Add(SD("instance.install.option.current_is_recommended", ("title", effectiveChoice.Title)));
        }
        else
        {
            parts.Add(SD("instance.install.option.current_and_recommended", ("current", effectiveChoice.Title), ("recommended", recommendedChoice.Title)));
        }

        if (!string.IsNullOrWhiteSpace(recommendedChoice.Summary))
        {
            parts.Add(recommendedChoice.Summary);
        }

        parts.Add(SD("instance.install.option.available_count", ("count", selectableChoices.Count)));
        parts.Add(GetInstallOptionRuleHint(optionTitle, minecraftVersion));
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string ResolveInstallOptionPreviewSelectText(bool isExistingInstance, string optionTitle)
    {
        return HasInstallSelection(isExistingInstance, optionTitle)
            ? SD("instance.install.option.change_version")
            : SD("instance.install.option.select_version");
    }

    private string ResolveInstallOptionSelectText(
        bool isExistingInstance,
        string optionTitle,
        string minecraftVersion,
        IReadOnlyList<FrontendInstallChoice> selectableChoices)
    {
        var effectiveChoice = ResolveEffectiveChoice(isExistingInstance, optionTitle, minecraftVersion);
        if (effectiveChoice is null)
        {
            return selectableChoices.Count > 0 ? SD("instance.install.option.install_recommended") : SD("instance.install.option.unavailable");
        }

        return "更换版本";
    }

    private string BuildInstallOptionUnresolvedDetail(
        bool isExistingInstance,
        string optionTitle,
        string minecraftVersion,
        IReadOnlyList<FrontendInstallChoice> selectableChoices)
    {
        var baselineText = GetBaselineSelection(isExistingInstance, optionTitle);
        var parts = new List<string>
        {
            SD("instance.install.option.unresolved_current", ("selection", baselineText), ("version", minecraftVersion))
        };

        if (selectableChoices.Count > 0)
        {
            parts.Add(SD("instance.install.option.reselect_available", ("count", selectableChoices.Count)));
            parts.Add(SD("instance.install.option.recommended", ("title", selectableChoices[0].Title)));
        }
        else
        {
            parts.Add(SD("instance.install.option.no_candidates"));
        }

        parts.Add(GetInstallOptionRuleHint(optionTitle, minecraftVersion));
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string BuildInstallOptionUnavailableDetail(string optionTitle, string minecraftVersion, string unavailableReason)
    {
        var parts = new List<string>
        {
            SD("instance.install.option.unavailable_detail", ("version", minecraftVersion), ("reason", unavailableReason))
        };

        parts.Add(GetInstallOptionRuleHint(optionTitle, minecraftVersion));
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string GetInstallOptionRuleHint(string optionTitle, string minecraftVersion)
    {
        return optionTitle switch
        {
            "Forge" or "NeoForge" or "Cleanroom" or "Fabric" or "Legacy Fabric" or "Quilt" or "LabyMod" =>
                SD("instance.install.rules.primary_loader"),
            "Fabric API" =>
                SD("instance.install.rules.fabric_api"),
            "Legacy Fabric API" =>
                SD("instance.install.rules.legacy_fabric_api"),
            "QFAPI / QSL" =>
                SD("instance.install.rules.qsl"),
            "OptiFine" when IsVersionGreaterThan(minecraftVersion, "1.20.4") =>
                SD("instance.install.rules.optifine_newer_than_1204"),
            "OptiFine" =>
                SD("instance.install.rules.optifine"),
            "OptiFabric" when IsOptiFabricOriginsOnlyVersion(minecraftVersion) =>
                SD("instance.install.rules.optifabric_origins"),
            "OptiFabric" =>
                SD("instance.install.rules.optifabric"),
            "LiteLoader" =>
                SD("instance.install.rules.liteloader"),
            _ => string.Empty
        };
    }

    private IReadOnlyList<FrontendInstallChoice> GetSelectableInstallChoices(bool isExistingInstance, string optionTitle, string minecraftVersion)
    {
        var choices = FrontendInstallWorkflowService.GetSupportedChoices(optionTitle, minecraftVersion);
        return optionTitle switch
        {
            "Forge" => FilterForgeChoicesForCurrentState(isExistingInstance, choices),
            "OptiFine" => FilterOptiFineChoicesForCurrentState(isExistingInstance, choices),
            _ => choices
        };
    }

    private IReadOnlyList<FrontendInstallChoice> FilterForgeChoicesForCurrentState(bool isExistingInstance, IReadOnlyList<FrontendInstallChoice> choices)
    {
        var optiFineChoice = ResolveEffectiveChoice(
            isExistingInstance,
            "OptiFine",
            GetEffectiveMinecraftVersion(isExistingInstance).Replace("Minecraft ", string.Empty, StringComparison.Ordinal));
        if (optiFineChoice is null)
        {
            return choices;
        }

        return choices
            .Where(choice => IsOptiFineCompatibleWithForgeChoice(optiFineChoice, choice))
            .ToArray();
    }

    private IReadOnlyList<FrontendInstallChoice> FilterOptiFineChoicesForCurrentState(bool isExistingInstance, IReadOnlyList<FrontendInstallChoice> choices)
    {
        var forgeChoice = ResolveEffectiveChoice(
            isExistingInstance,
            "Forge",
            GetEffectiveMinecraftVersion(isExistingInstance).Replace("Minecraft ", string.Empty, StringComparison.Ordinal));
        if (forgeChoice is null)
        {
            return choices;
        }

        return choices
            .Where(choice => IsOptiFineCompatibleWithForgeChoice(choice, forgeChoice))
            .ToArray();
    }

    private string? GetInstallOptionStaticUnavailableReason(
        bool isExistingInstance,
        string optionTitle,
        string minecraftVersion)
    {
        var currentPrimary = GetCurrentPrimaryInstallTitle(isExistingInstance);
        var currentApi = GetCurrentApiInstallTitle(isExistingInstance);
        var hasOptiFine = HasInstallSelection(isExistingInstance, "OptiFine");
        var hasForge = HasInstallSelection(isExistingInstance, "Forge");
        var hasCleanroom = HasInstallSelection(isExistingInstance, "Cleanroom");
        var hasFabric = HasInstallSelection(isExistingInstance, "Fabric");
        var hasLegacyFabric = HasInstallSelection(isExistingInstance, "Legacy Fabric");
        var hasQuilt = HasInstallSelection(isExistingInstance, "Quilt");
        var hasLabyMod = HasInstallSelection(isExistingInstance, "LabyMod");
        var hasLiteLoader = HasInstallSelection(isExistingInstance, "LiteLoader");

        return optionTitle switch
        {
            "OptiFine" when currentPrimary is "NeoForge" or "Quilt" or "LabyMod" => SD("instance.install.unavailable.incompatible_with", ("target", currentPrimary)),
            "OptiFine" when hasCleanroom => SD("instance.install.unavailable.incompatible_with", ("target", "Cleanroom")),
            "OptiFine" when hasForge && IsVersionAtLeast(minecraftVersion, "1.13") && IsVersionAtMost(minecraftVersion, "1.14.3") => SD("instance.install.unavailable.incompatible_with", ("target", "Forge")),
            "OptiFine" when hasFabric && IsVersionGreaterThan(minecraftVersion, "1.20.4") => SD("instance.install.unavailable.incompatible_with", ("target", "Fabric")),

            "Forge" when IsVersionAtLeast("1.5.1", minecraftVersion) && IsVersionAtLeast(minecraftVersion, "1.1") => SD("instance.install.unavailable.no_versions"),
            "Forge" when currentPrimary is not null && !string.Equals(currentPrimary, "Forge", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentPrimary)),
            "Forge" when hasOptiFine && IsVersionAtLeast(minecraftVersion, "1.13") && IsVersionAtMost(minecraftVersion, "1.14.3") => SD("instance.install.unavailable.incompatible_with", ("target", "OptiFine")),

            "NeoForge" when hasOptiFine => SD("instance.install.unavailable.incompatible_with", ("target", "OptiFine")),
            "NeoForge" when currentPrimary is not null && !string.Equals(currentPrimary, "NeoForge", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentPrimary)),

            "Cleanroom" when !minecraftVersion.StartsWith("1.", StringComparison.Ordinal) => SD("instance.install.unavailable.no_versions"),
            "Cleanroom" when hasOptiFine => SD("instance.install.unavailable.incompatible_with", ("target", "OptiFine")),
            "Cleanroom" when currentPrimary is not null && !string.Equals(currentPrimary, "Cleanroom", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentPrimary)),

            "Fabric" when hasOptiFine && IsVersionGreaterThan(minecraftVersion, "1.20.4") => SD("instance.install.unavailable.incompatible_with", ("target", "OptiFine")),
            "Fabric" when currentPrimary is not null && !string.Equals(currentPrimary, "Fabric", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentPrimary)),

            "Legacy Fabric" when hasLiteLoader => SD("instance.install.unavailable.incompatible_with", ("target", "LiteLoader")),
            "Legacy Fabric" when currentPrimary is not null && !string.Equals(currentPrimary, "Legacy Fabric", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentPrimary)),

            "Quilt" when hasOptiFine => SD("instance.install.unavailable.incompatible_with", ("target", "OptiFine")),
            "Quilt" when currentPrimary is not null
                            && !string.Equals(currentPrimary, "Quilt", StringComparison.Ordinal)
                            && !string.Equals(currentPrimary, "Fabric", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentPrimary)),

            "LabyMod" when hasOptiFine => SD("instance.install.unavailable.incompatible_with", ("target", "OptiFine")),
            "LabyMod" when currentPrimary is not null && !string.Equals(currentPrimary, "LabyMod", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentPrimary)),

            "Fabric API" when currentApi is not null && !string.Equals(currentApi, "Fabric API", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentApi)),
            "Fabric API" when !hasFabric && !hasQuilt => SD("instance.install.unavailable.requires_fabric"),

            "Legacy Fabric API" when currentApi is not null && !string.Equals(currentApi, "Legacy Fabric API", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentApi)),
            "Legacy Fabric API" when !hasLegacyFabric => SD("instance.install.unavailable.requires_legacy_fabric"),

            "QFAPI / QSL" when currentApi is not null && !string.Equals(currentApi, "QFAPI / QSL", StringComparison.Ordinal) => SD("instance.install.unavailable.incompatible_with", ("target", currentApi)),
            "QFAPI / QSL" when !hasQuilt => SD("instance.install.unavailable.requires_quilt"),

            "OptiFabric" when IsOptiFabricOriginsOnlyVersion(minecraftVersion) => SD("instance.install.unavailable.optifabric_origins"),
            "OptiFabric" when !hasFabric && !hasOptiFine => SD("instance.install.unavailable.requires_optifine_and_fabric"),
            "OptiFabric" when !hasFabric => SD("instance.install.unavailable.requires_fabric"),
            "OptiFabric" when !hasOptiFine => SD("instance.install.unavailable.requires_optifine"),
            _ => null
        };
    }

    private string? GetInstallOptionUnavailableReason(
        bool isExistingInstance,
        string optionTitle,
        string minecraftVersion,
        IReadOnlyList<FrontendInstallChoice> selectableChoices)
    {
        var staticUnavailableReason = GetInstallOptionStaticUnavailableReason(isExistingInstance, optionTitle, minecraftVersion);
        if (staticUnavailableReason is not null)
        {
            return staticUnavailableReason;
        }

        var hasOptiFine = HasInstallSelection(isExistingInstance, "OptiFine");
        var hasForge = HasInstallSelection(isExistingInstance, "Forge");

        switch (optionTitle)
        {
            case "OptiFine":
                if (selectableChoices.Count == 0 && hasForge)
                {
                    return SD("instance.install.unavailable.specific_forge_only");
                }

                return selectableChoices.Count == 0 ? SD("instance.install.unavailable.no_versions") : null;

            case "LiteLoader":
                return selectableChoices.Count == 0 ? SD("instance.install.unavailable.no_versions") : null;

            case "Forge":
                return selectableChoices.Count == 0
                    ? hasOptiFine ? SD("instance.install.unavailable.incompatible_with", ("target", "OptiFine")) : SD("instance.install.unavailable.no_versions")
                    : null;

            default:
                return selectableChoices.Count == 0 ? SD("instance.install.unavailable.no_versions") : null;
        }
    }

    private bool HasInstallSelection(bool isExistingInstance, string optionTitle)
    {
        var selection = GetEffectiveSelectionText(isExistingInstance, optionTitle);
        return !string.IsNullOrWhiteSpace(selection)
               && !string.Equals(selection, SD("instance.common.not_installed"), StringComparison.Ordinal)
               && !string.Equals(selection, SD("instance.install.option.can_add"), StringComparison.Ordinal);
    }

    private bool CanClearInstallSelection(bool isExistingInstance, string optionTitle)
    {
        return HasInstallSelection(isExistingInstance, optionTitle);
    }

    private string? GetCurrentPrimaryInstallTitle(bool isExistingInstance)
    {
        return ManagedPrimaryInstallTitles.FirstOrDefault(title => HasInstallSelection(isExistingInstance, title));
    }

    private string? GetCurrentApiInstallTitle(bool isExistingInstance)
    {
        return ManagedApiInstallTitles.FirstOrDefault(title => HasInstallSelection(isExistingInstance, title));
    }

    private bool IsOptiFineCompatibleWithForgeChoice(FrontendInstallChoice optiFineChoice, FrontendInstallChoice forgeChoice)
    {
        var optiFineMinecraft = optiFineChoice.Metadata?["minecraftVersion"]?.GetValue<string>();
        var forgeMinecraft = forgeChoice.Metadata?["minecraftVersion"]?.GetValue<string>();
        if (!string.Equals(optiFineMinecraft, forgeMinecraft, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requiredForge = optiFineChoice.Metadata?["requiredForgeVersion"]?.GetValue<string>();
        if (requiredForge is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(requiredForge))
        {
            return true;
        }

        if (requiredForge.Contains('.', StringComparison.Ordinal))
        {
            return string.Equals(forgeChoice.Version, requiredForge, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(forgeChoice.Version.Split('.').LastOrDefault(), requiredForge, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOptiFabricOriginsOnlyVersion(string minecraftVersion)
    {
        return IsVersionAtLeast(minecraftVersion, "1.14")
               && IsVersionAtMost(minecraftVersion, "1.15");
    }

    private static bool IsVersionGreaterThan(string left, string right)
    {
        return CompareReleaseVersions(left, right) > 0;
    }

    private static bool IsVersionAtLeast(string left, string right)
    {
        return CompareReleaseVersions(left, right) >= 0;
    }

    private static bool IsVersionAtMost(string left, string right)
    {
        return CompareReleaseVersions(left, right) <= 0;
    }

    private static int CompareReleaseVersions(string left, string right)
    {
        static string Normalize(string value)
        {
            value = value.Replace("Minecraft ", string.Empty, StringComparison.Ordinal).Trim();
            return new string(value.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
        }

        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        if (Version.TryParse(normalizedLeft, out var leftVersion)
            && Version.TryParse(normalizedRight, out var rightVersion))
        {
            return leftVersion.CompareTo(rightVersion);
        }

        return string.Compare(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record InstallOptionPresentation(
        string DetailText,
        string SelectText,
        bool CanSelect);
}
