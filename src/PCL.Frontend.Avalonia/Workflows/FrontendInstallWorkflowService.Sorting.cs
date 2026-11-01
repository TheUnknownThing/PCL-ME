using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstallWorkflowService
{

    private static IReadOnlyList<FrontendInstallChoice> SortInstallChoicesDescending(
        IEnumerable<FrontendInstallChoice> choices)
    {
        var ordered = choices.ToList();
        ordered.Sort(CompareInstallChoicesDescending);
        return ordered;
    }


    private static IReadOnlyList<FrontendInstallChoice> SortInstallChoicesByVersionDescending(
        IEnumerable<FrontendInstallChoice> choices)
    {
        var ordered = choices.ToList();
        ordered.Sort((left, right) =>
        {
            var versionCompare = CompareLooseVersions(right.Version, left.Version);
            if (versionCompare != 0)
            {
                return versionCompare;
            }

            return CompareInstallChoicesDescending(left, right);
        });

        return ordered;
    }


    private static int CompareInstallChoicesDescending(FrontendInstallChoice? left, FrontendInstallChoice? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        var leftReleaseTime = GetInstallChoiceReleaseTime(left);
        var rightReleaseTime = GetInstallChoiceReleaseTime(right);
        if (leftReleaseTime is not null && rightReleaseTime is not null)
        {
            var releaseCompare = rightReleaseTime.Value.CompareTo(leftReleaseTime.Value);
            if (releaseCompare != 0)
            {
                return releaseCompare;
            }
        }

        var versionCompare = CompareLooseVersions(right.Version, left.Version);
        if (versionCompare != 0)
        {
            return versionCompare;
        }

        return string.Compare(right.Title, left.Title, StringComparison.OrdinalIgnoreCase);
    }


    private static DateTimeOffset? GetInstallChoiceReleaseTime(FrontendInstallChoice choice)
    {
        var rawValue = choice.Metadata?["releaseTime"]?.GetValue<string>();
        return DateTimeOffset.TryParse(rawValue, out var parsed) ? parsed : null;
    }


    private static int CompareLooseVersions(string? left, string? right)
    {
        var (leftCore, leftSuffix) = SplitVersionCoreAndSuffix(left);
        var (rightCore, rightSuffix) = SplitVersionCoreAndSuffix(right);

        var coreCompare = CompareVersionNumberSequences(
            ExtractVersionNumbers(leftCore),
            ExtractVersionNumbers(rightCore));
        if (coreCompare != 0)
        {
            return coreCompare;
        }

        var stabilityCompare = GetVersionStabilityRank(leftSuffix).CompareTo(GetVersionStabilityRank(rightSuffix));
        if (stabilityCompare != 0)
        {
            return stabilityCompare;
        }

        var suffixCompare = CompareVersionNumberSequences(
            ExtractVersionNumbers(leftSuffix),
            ExtractVersionNumbers(rightSuffix));
        if (suffixCompare != 0)
        {
            return suffixCompare;
        }

        return string.Compare(
            NormalizeVersionText(leftSuffix),
            NormalizeVersionText(rightSuffix),
            StringComparison.OrdinalIgnoreCase);
    }


    private static (string Core, string Suffix) SplitVersionCoreAndSuffix(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return (string.Empty, string.Empty);
        }

        var match = Regex.Match(
            rawValue,
            @"alpha|beta|preview|pre|rc|snapshot|nightly|dev|experimental|test",
            RegexOptions.IgnoreCase);
        return !match.Success
            ? (rawValue, string.Empty)
            : (rawValue[..match.Index], rawValue[match.Index..]);
    }


    private static IReadOnlyList<long> ExtractVersionNumbers(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return Regex.Matches(rawValue, @"\d+")
            .Select(match => long.TryParse(match.Value, out var value) ? value : 0L)
            .ToArray();
    }


    private static int CompareVersionNumberSequences(
        IReadOnlyList<long> left,
        IReadOnlyList<long> right)
    {
        var maxLength = Math.Max(left.Count, right.Count);
        for (var index = 0; index < maxLength; index++)
        {
            var leftValue = index < left.Count ? left[index] : 0L;
            var rightValue = index < right.Count ? right[index] : 0L;
            var compare = leftValue.CompareTo(rightValue);
            if (compare != 0)
            {
                return compare;
            }
        }

        return 0;
    }


    private static int GetVersionStabilityRank(string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return 5;
        }

        var normalized = suffix.ToLowerInvariant();
        if (normalized.Contains("rc", StringComparison.Ordinal))
        {
            return 4;
        }

        if (normalized.Contains("preview", StringComparison.Ordinal)
            || normalized.Contains("pre", StringComparison.Ordinal))
        {
            return 3;
        }

        if (normalized.Contains("beta", StringComparison.Ordinal))
        {
            return 2;
        }

        if (normalized.Contains("alpha", StringComparison.Ordinal))
        {
            return 1;
        }

        return 0;
    }


    private static string NormalizeVersionText(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? string.Empty
            : rawValue.Trim().Replace('_', '-');
    }

}
