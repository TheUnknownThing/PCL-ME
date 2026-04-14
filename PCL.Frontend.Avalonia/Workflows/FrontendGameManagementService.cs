using System.IO;
using System.Text;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendGameManagementService
{
    private const string DefaultCommunityResourceFileName = "community-resource-download";

    public static string ResolveCommunityResourceFileName(
        string? projectTitle,
        string? suggestedFileName,
        string fallbackTitle,
        int formatIndex)
    {
        var normalizedFileName = SanitizeFileName(string.IsNullOrWhiteSpace(suggestedFileName) ? fallbackTitle : suggestedFileName.Trim());
        if (string.IsNullOrWhiteSpace(normalizedFileName))
        {
            normalizedFileName = DefaultCommunityResourceFileName;
        }

        if (formatIndex >= 4)
        {
            return NormalizeArtifactFileName(normalizedFileName);
        }

        var normalizedProjectTitle = SanitizeProjectTitle(projectTitle);
        if (string.IsNullOrWhiteSpace(normalizedProjectTitle))
        {
            return NormalizeArtifactFileName(normalizedFileName);
        }

        var extension = Path.GetExtension(normalizedFileName);
        var baseName = string.IsNullOrEmpty(extension)
            ? normalizedFileName
            : normalizedFileName[..^extension.Length];
        var projectKey = NormalizeComparisonKey(normalizedProjectTitle);
        var baseNameKey = NormalizeComparisonKey(baseName);
        if (string.IsNullOrWhiteSpace(projectKey)
            || string.IsNullOrWhiteSpace(baseNameKey)
            || BaseNameAlreadyContainsProjectTitle(baseName, normalizedProjectTitle))
        {
            return NormalizeArtifactFileName(normalizedFileName);
        }

        var formattedBaseName = formatIndex switch
        {
            0 => $"【{normalizedProjectTitle}】{baseName}",
            1 => $"[{normalizedProjectTitle}] {baseName}",
            2 => $"{normalizedProjectTitle}-{baseName}",
            3 => $"{baseName}-{normalizedProjectTitle}",
            _ => baseName
        };
        return NormalizeArtifactFileName($"{formattedBaseName}{extension}");
    }

    public static FrontendLocalModDisplay ResolveLocalModDisplay(FrontendInstanceResourceEntry entry, int styleIndex)
    {
        var fileName = ResolveLocalModFileName(entry.Path);
        var translatedName = string.IsNullOrWhiteSpace(entry.Title) ? fileName : entry.Title.Trim();
        var summary = string.IsNullOrWhiteSpace(entry.Summary) ? string.Empty : entry.Summary.Trim();

        return styleIndex switch
        {
            1 => new FrontendLocalModDisplay(
                fileName,
                JoinDistinctSegments(translatedName, summary)),
            _ => new FrontendLocalModDisplay(
                translatedName,
                JoinDistinctSegments(fileName, summary))
        };
    }

    private static string ResolveLocalModFileName(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        fileName = RemoveTrailingSuffix(fileName, ".disabled");
        fileName = RemoveTrailingSuffix(fileName, ".old");
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string RemoveTrailingSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    private static string SanitizeProjectTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        var splitIndex = normalized.IndexOf(" (", StringComparison.Ordinal);
        if (splitIndex >= 0)
        {
            normalized = normalized[..splitIndex];
        }

        splitIndex = normalized.IndexOf(" - ", StringComparison.Ordinal);
        if (splitIndex >= 0)
        {
            normalized = normalized[..splitIndex];
        }

        return SanitizeFileName(normalized);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(invalidCharacters.Contains(character) ? '-' : character);
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeArtifactFileName(string value)
    {
        return value.Replace("~", "-", StringComparison.Ordinal);
    }

    private static string NormalizeComparisonKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static bool BaseNameAlreadyContainsProjectTitle(string baseName, string projectTitle)
    {
        var projectKey = NormalizeComparisonKey(projectTitle);
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            return true;
        }

        return EnumerateComparisonSegments(baseName)
            .Any(segment => string.Equals(segment, projectKey, StringComparison.Ordinal));
    }

    private static IEnumerable<string> EnumerateComparisonSegments(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static string JoinDistinctSegments(params string[] values)
    {
        var segments = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var segment = value.Trim();
            if (!segments.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                segments.Add(segment);
            }
        }

        return string.Join(" • ", segments);
    }
}

internal readonly record struct FrontendLocalModDisplay(
    string Title,
    string Summary);
