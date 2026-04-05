using Avalonia.Media.Imaging;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string[] InstallHintOptiFabric =
    [
        "必须安装 OptiFabric 才能正常使用 OptiFine！"
    ];

    private static readonly string[] InstallHintOptiFabricOld =
    [
        "安装结束后，请在 Mod 下载中搜索 OptiFabric Origins 并下载，否则 OptiFine 会无法使用！"
    ];

    private static readonly string[] InstallHintLegacyOptiFabric =
    [
        "安装结束后，请在 Mod 下载中搜索 LegacyOptiFabric 并下载，否则 OptiFine 会无法使用！"
    ];

    private static readonly string[] InstallHintModOptiFine =
    [
        "OptiFine 与一部分 Mod 的兼容性不佳，请谨慎安装。"
    ];

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
        var selectableChoices = GetSelectableInstallChoices(isExistingInstance, optionTitle, minecraftVersion);
        var unavailableReason = GetInstallOptionUnavailableReason(isExistingInstance, optionTitle, minecraftVersion, selectableChoices);
        if (unavailableReason is not null)
        {
            return new InstallOptionPresentation(unavailableReason, "当前不可用", false);
        }

        return new InstallOptionPresentation(
            isExistingInstance
                ? "继续沿用原版实例安装页的卡片选择结构，并直接对当前实例的安装清单执行修改。"
                : "继续沿用原版安装页的卡片选择结构，并直接从当前可用候选中选定版本。",
            "选择版本",
            true);
    }

    private IReadOnlyList<string> GetEffectiveInstallHints(bool isExistingInstance)
    {
        var hints = new List<string>();
        if (HasInstallSelection(isExistingInstance, "Fabric")
            && !HasInstallSelection(isExistingInstance, "Fabric API"))
        {
            hints.Add("你尚未选择安装 Fabric API，这会导致大多数 Mod 无法使用！");
        }

        if (HasInstallSelection(isExistingInstance, "Quilt")
            && !HasInstallSelection(isExistingInstance, "QFAPI / QSL")
            && !HasInstallSelection(isExistingInstance, "Fabric API"))
        {
            hints.Add("你尚未选择安装 QFAPI / QSL，这会导致大多数 Mod 无法使用！");
        }

        if ((HasInstallSelection(isExistingInstance, "Fabric") || HasInstallSelection(isExistingInstance, "Legacy Fabric"))
            && HasInstallSelection(isExistingInstance, "OptiFine")
            && !HasInstallSelection(isExistingInstance, "OptiFabric"))
        {
            if (IsOptiFabricOriginsOnlyVersion(GetEffectiveMinecraftVersion(isExistingInstance)))
            {
                hints.AddRange(InstallHintOptiFabricOld);
            }
            else if (HasInstallSelection(isExistingInstance, "Legacy Fabric"))
            {
                hints.AddRange(InstallHintLegacyOptiFabric);
            }
            else
            {
                hints.AddRange(InstallHintOptiFabric);
            }
        }

        if (HasInstallSelection(isExistingInstance, "OptiFine")
            && IsVersionGreaterThan(GetEffectiveMinecraftVersion(isExistingInstance), "Minecraft 1.20.4")
            && (HasInstallSelection(isExistingInstance, "Forge") || HasInstallSelection(isExistingInstance, "Fabric")))
        {
            hints.AddRange(InstallHintModOptiFine);
        }

        return hints;
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

    private string? GetInstallOptionUnavailableReason(
        bool isExistingInstance,
        string optionTitle,
        string minecraftVersion,
        IReadOnlyList<FrontendInstallChoice> selectableChoices)
    {
        var currentPrimary = GetCurrentPrimaryInstallTitle(isExistingInstance);
        var currentApi = GetCurrentApiInstallTitle(isExistingInstance);
        var hasOptiFine = HasInstallSelection(isExistingInstance, "OptiFine");
        var hasForge = HasInstallSelection(isExistingInstance, "Forge");
        var hasNeoForge = HasInstallSelection(isExistingInstance, "NeoForge");
        var hasCleanroom = HasInstallSelection(isExistingInstance, "Cleanroom");
        var hasFabric = HasInstallSelection(isExistingInstance, "Fabric");
        var hasLegacyFabric = HasInstallSelection(isExistingInstance, "Legacy Fabric");
        var hasQuilt = HasInstallSelection(isExistingInstance, "Quilt");
        var hasLabyMod = HasInstallSelection(isExistingInstance, "LabyMod");
        var hasLiteLoader = HasInstallSelection(isExistingInstance, "LiteLoader");

        switch (optionTitle)
        {
            case "OptiFine":
                if (currentPrimary is "NeoForge" or "Quilt" or "LabyMod")
                {
                    return $"与 {currentPrimary} 不兼容";
                }

                if (hasCleanroom)
                {
                    return "与 Cleanroom 不兼容";
                }

                if (hasForge && IsVersionAtLeast(minecraftVersion, "1.13") && IsVersionAtMost(minecraftVersion, "1.14.3"))
                {
                    return "与 Forge 不兼容";
                }

                if (hasFabric && IsVersionGreaterThan(minecraftVersion, "1.20.4"))
                {
                    return "与 Fabric 不兼容";
                }

                if (selectableChoices.Count == 0 && hasForge)
                {
                    return "仅兼容特定版本的 Forge";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "LiteLoader":
                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "Forge":
                if (IsVersionAtLeast("1.5.1", minecraftVersion) && IsVersionAtLeast(minecraftVersion, "1.1"))
                {
                    return "无可用版本";
                }

                if (currentPrimary is not null && !string.Equals(currentPrimary, "Forge", StringComparison.Ordinal))
                {
                    return $"与 {currentPrimary} 不兼容";
                }

                if (hasOptiFine && IsVersionAtLeast(minecraftVersion, "1.13") && IsVersionAtMost(minecraftVersion, "1.14.3"))
                {
                    return "与 OptiFine 不兼容";
                }

                return selectableChoices.Count == 0
                    ? hasOptiFine ? "与 OptiFine 不兼容" : "无可用版本"
                    : null;

            case "NeoForge":
                if (hasOptiFine)
                {
                    return "与 OptiFine 不兼容";
                }

                if (currentPrimary is not null && !string.Equals(currentPrimary, "NeoForge", StringComparison.Ordinal))
                {
                    return $"与 {currentPrimary} 不兼容";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "Cleanroom":
                if (!minecraftVersion.StartsWith("1.", StringComparison.Ordinal))
                {
                    return "没有可用版本";
                }

                if (hasOptiFine)
                {
                    return "与 OptiFine 不兼容";
                }

                if (currentPrimary is not null && !string.Equals(currentPrimary, "Cleanroom", StringComparison.Ordinal))
                {
                    return $"与 {currentPrimary} 不兼容";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "Fabric":
                if (hasOptiFine && IsVersionGreaterThan(minecraftVersion, "1.20.4"))
                {
                    return "与 OptiFine 不兼容";
                }

                if (currentPrimary is not null && !string.Equals(currentPrimary, "Fabric", StringComparison.Ordinal))
                {
                    return $"与 {currentPrimary} 不兼容";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "Legacy Fabric":
                if (hasLiteLoader)
                {
                    return "与 LiteLoader 不兼容";
                }

                if (currentPrimary is not null && !string.Equals(currentPrimary, "Legacy Fabric", StringComparison.Ordinal))
                {
                    return $"与 {currentPrimary} 不兼容";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "Quilt":
                if (hasOptiFine)
                {
                    return "与 OptiFine 不兼容";
                }

                if (currentPrimary is not null
                    && !string.Equals(currentPrimary, "Quilt", StringComparison.Ordinal)
                    && !string.Equals(currentPrimary, "Fabric", StringComparison.Ordinal))
                {
                    return $"与 {currentPrimary} 不兼容";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "LabyMod":
                if (hasOptiFine)
                {
                    return "与 OptiFine 不兼容";
                }

                if (currentPrimary is not null && !string.Equals(currentPrimary, "LabyMod", StringComparison.Ordinal))
                {
                    return $"与 {currentPrimary} 不兼容";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "Fabric API":
                if (currentApi is not null && !string.Equals(currentApi, "Fabric API", StringComparison.Ordinal))
                {
                    return $"与 {currentApi} 不兼容";
                }

                if (!hasFabric && !hasQuilt)
                {
                    return "需要安装 Fabric";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "Legacy Fabric API":
                if (currentApi is not null && !string.Equals(currentApi, "Legacy Fabric API", StringComparison.Ordinal))
                {
                    return $"与 {currentApi} 不兼容";
                }

                if (!hasLegacyFabric)
                {
                    return "需要安装 LegacyFabric";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            case "QFAPI / QSL":
                if (currentApi is not null && !string.Equals(currentApi, "QFAPI / QSL", StringComparison.Ordinal))
                {
                    return $"与 {currentApi} 不兼容";
                }

                if (!hasQuilt)
                {
                    return "需要安装 Quilt";
                }

                return selectableChoices.Count == 0 ? "没有可用版本" : null;

            case "OptiFabric":
                if (IsOptiFabricOriginsOnlyVersion(minecraftVersion))
                {
                    return "不兼容老版本 Fabric，请手动下载 OptiFabric Origins";
                }

                if (!hasFabric && !hasOptiFine)
                {
                    return "需要安装 OptiFine 与 Fabric";
                }

                if (!hasFabric)
                {
                    return "需要安装 Fabric";
                }

                if (!hasOptiFine)
                {
                    return "需要安装 OptiFine";
                }

                return selectableChoices.Count == 0 ? "无可用版本" : null;

            default:
                return null;
        }
    }

    private bool HasInstallSelection(bool isExistingInstance, string optionTitle)
    {
        var selection = GetEffectiveSelectionText(isExistingInstance, optionTitle);
        return !string.IsNullOrWhiteSpace(selection)
               && !string.Equals(selection, "未安装", StringComparison.Ordinal)
               && !string.Equals(selection, "可以添加", StringComparison.Ordinal);
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
