using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchJavaRequirementService
{
    private static readonly Version MinimumVersion = new(0, 0, 0, 0);
    private static readonly Version MaximumVersion = new(999, 999, 999, 999);

    public static MinecraftLaunchJavaRequirementResult Evaluate(MinecraftLaunchJavaRequirementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var minVersion = MinimumVersion;
        var maxVersion = MaximumVersion;

        if ((!request.IsVersionInfoValid && request.ReleaseTime >= new DateTime(2024, 4, 2)) ||
            (request.IsVersionInfoValid && request.VanillaVersion is not null && request.VanillaVersion >= new Version(20, 0, 5)))
        {
            minVersion = new Version(21, 0, 0, 0);
        }
        else if ((!request.IsVersionInfoValid && request.ReleaseTime >= new DateTime(2021, 11, 16)) ||
                 (request.IsVersionInfoValid && request.VanillaVersion?.Major >= 18))
        {
            minVersion = new Version(17, 0, 0, 0);
        }
        else if ((!request.IsVersionInfoValid && request.ReleaseTime >= new DateTime(2021, 5, 11)) ||
                 (request.IsVersionInfoValid && request.VanillaVersion?.Major >= 17))
        {
            minVersion = new Version(16, 0, 0, 0);
        }
        else if (request.ReleaseTime.Year >= 2017)
        {
            minVersion = new Version(1, 8, 0, 0);
        }
        else if (request.ReleaseTime <= new DateTime(2013, 5, 1) && request.ReleaseTime.Year >= 2001)
        {
            maxVersion = new Version(1, 8, 999, 999);
        }

        var recommendedMajorVersion = request.MojangRecommendedMajorVersion;
        var recommendedComponent = string.IsNullOrWhiteSpace(request.MojangRecommendedComponent)
            ? null
            : request.MojangRecommendedComponent;
        if (recommendedMajorVersion >= 22)
        {
            minVersion = Max(minVersion, new Version(recommendedMajorVersion, 0, 0, 0));
        }

        if (request.HasOptiFine && request.IsVersionInfoValid && request.VanillaVersion is not null)
        {
            if (request.VanillaVersion.Major < 7)
            {
                maxVersion = Min(maxVersion, new Version(1, 8, 999, 999));
            }
            else if (request.VanillaVersion.Major >= 8 && request.VanillaVersion.Major < 12)
            {
                minVersion = Max(minVersion, new Version(1, 8, 0, 0));
                maxVersion = Min(maxVersion, new Version(1, 8, 999, 999));
            }
            else if (request.VanillaVersion.Major == 12)
            {
                maxVersion = Min(maxVersion, new Version(1, 8, 999, 999));
            }
        }

        if (request.HasForge)
        {
            if (request.VanillaVersion is not null &&
                request.VanillaVersion >= new Version(6, 0, 1) &&
                request.VanillaVersion <= new Version(7, 0, 2))
            {
                minVersion = Max(minVersion, new Version(1, 7, 0, 0));
                maxVersion = Min(maxVersion, new Version(1, 7, 999, 999));
            }
            else if (!request.IsVersionInfoValid || request.VanillaVersion?.Major <= 12)
            {
                maxVersion = Min(maxVersion, new Version(1, 8, 999, 999));
            }
            else if (request.VanillaVersion?.Major <= 14)
            {
                minVersion = Max(minVersion, new Version(1, 8, 0, 0));
                maxVersion = Min(maxVersion, new Version(1, 10, 999, 999));
            }
            else if (request.VanillaVersion?.Major == 15)
            {
                minVersion = Max(minVersion, new Version(1, 8, 0, 0));
                maxVersion = Min(maxVersion, new Version(1, 15, 999, 999));
            }
            else if (IsVersionBetween(request.ForgeVersion, "34.0.0", "36.2.25"))
            {
                maxVersion = Min(maxVersion, new Version(1, 8, 0, 321));
            }
            else if (request.VanillaVersion?.Major >= 18 &&
                     request.VanillaVersion.Major < 19 &&
                     request.HasOptiFine)
            {
                maxVersion = Min(maxVersion, new Version(1, 18, 999, 999));
            }
        }

        if (request.HasCleanroom)
        {
            minVersion = Max(minVersion, new Version(21, 0, 0, 0));
        }

        if (request.HasFabric && request.IsVersionInfoValid && request.VanillaVersion is not null)
        {
            if (request.VanillaVersion.Major >= 15 && request.VanillaVersion.Major <= 16)
            {
                minVersion = Max(minVersion, new Version(1, 8, 0, 0));
            }
            else if (request.VanillaVersion.Major >= 18)
            {
                minVersion = Max(minVersion, new Version(17, 0, 0, 0));
            }
        }

        if (request.HasLiteLoader && request.IsVersionInfoValid)
        {
            maxVersion = Min(maxVersion, new Version(8, 999, 999, 999));
        }

        if (request.HasLabyMod)
        {
            minVersion = Max(minVersion, new Version(21, 0, 0, 0));
            maxVersion = MaximumVersion;
        }

        if (request.JsonRequiredMajorVersion is not null)
        {
            var jsonMajorVersion = request.JsonRequiredMajorVersion.Value;
            minVersion = Max(
                minVersion,
                jsonMajorVersion <= 8
                    ? new Version(1, jsonMajorVersion, 0, 0)
                    : new Version(jsonMajorVersion, 0, 0, 0));

            if (maxVersion < minVersion)
            {
                maxVersion = MaximumVersion;
            }
        }

        return new MinecraftLaunchJavaRequirementResult(
            minVersion,
            maxVersion,
            recommendedMajorVersion,
            recommendedComponent);
    }

    private static bool IsVersionBetween(string? versionText, string minInclusive, string maxInclusive)
    {
        if (!Version.TryParse(versionText, out var version) ||
            !Version.TryParse(minInclusive, out var min) ||
            !Version.TryParse(maxInclusive, out var max))
        {
            return false;
        }

        return version >= min && version <= max;
    }

    private static Version Max(Version left, Version right)
    {
        return left >= right ? left : right;
    }

    private static Version Min(Version left, Version right)
    {
        return left <= right ? left : right;
    }
}

public sealed record MinecraftLaunchJavaRequirementRequest(
    bool IsVersionInfoValid,
    DateTime ReleaseTime,
    Version? VanillaVersion,
    bool HasOptiFine,
    bool HasForge,
    string? ForgeVersion,
    bool HasCleanroom,
    bool HasFabric,
    bool HasLiteLoader,
    bool HasLabyMod,
    int? JsonRequiredMajorVersion,
    int MojangRecommendedMajorVersion,
    string? MojangRecommendedComponent);

public sealed record MinecraftLaunchJavaRequirementResult(
    Version MinimumVersion,
    Version MaximumVersion,
    int RecommendedMajorVersion,
    string? RecommendedComponent);
