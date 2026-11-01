using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendIsolationPolicyService
{
    public static int NormalizeGlobalIsolationMode(int value)
    {
        return Math.Clamp(value, 0, 4);
    }

    public static bool ShouldIsolateByGlobalMode(int rawMode, bool isModable, bool isNonRelease)
    {
        return NormalizeGlobalIsolationMode(rawMode) switch
        {
            0 => false,
            1 => isModable,
            2 => isNonRelease,
            3 => isModable || isNonRelease,
            _ => true
        };
    }

    public static bool IsNonReleaseVersionType(string? versionType)
    {
        if (string.IsNullOrWhiteSpace(versionType))
        {
            return false;
        }

        return !string.Equals(versionType, "release", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNonReleaseMinecraftChoice(FrontendInstallChoice choice)
    {
        var metadataGroup = TryGetMetadataValue(choice.Metadata, "catalogGroup");
        if (!string.IsNullOrWhiteSpace(metadataGroup))
        {
            return !string.Equals(metadataGroup, "release", StringComparison.OrdinalIgnoreCase);
        }

        var metadataType = TryGetMetadataValue(choice.Metadata, "versionType");
        if (!string.IsNullOrWhiteSpace(metadataType))
        {
            return IsNonReleaseVersionType(metadataType);
        }

        var rawVersion = TryGetMetadataValue(choice.Metadata, "rawVersion");
        return IsLikelyNonReleaseMinecraftVersion(
            string.IsNullOrWhiteSpace(rawVersion)
                ? choice.Version
                : rawVersion);
    }

    public static bool IsLikelyNonReleaseMinecraftVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var normalized = version.Trim();
        if (StableReleaseRegex().IsMatch(normalized))
        {
            return false;
        }

        // Non-standard identifiers such as snapshots / pre-releases / RCs are treated as non-release.
        return true;
    }

    private static string? TryGetMetadataValue(JsonObject? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetPropertyValue(key, out var node))
        {
            return null;
        }

        if (node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToString();
        }
    }

    [GeneratedRegex(@"^\d+(\.\d+){1,3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex StableReleaseRegex();
}
