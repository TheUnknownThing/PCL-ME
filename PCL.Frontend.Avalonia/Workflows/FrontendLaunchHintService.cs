using PCL.Core.App.I18n;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLaunchHintService
{
    private static readonly string[] FallbackHintKeys =
    [
        "launch.dialog.hints.create_hints_file",
        "launch.dialog.hints.time_tick",
        "launch.dialog.hints.jeb_sheep",
        "launch.dialog.hints.lodestone_compass",
        "launch.dialog.hints.parrots_dance",
        "launch.dialog.hints.map_lock",
        "launch.dialog.hints.baby_turtle_scute",
        "launch.dialog.hints.potion_damage",
        "launch.dialog.hints.dinnerbone",
        "launch.dialog.hints.best_selling"
    ];

    public static string GetRandomHint(FrontendRuntimePaths runtimePaths, II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        ArgumentNullException.ThrowIfNull(i18n);

        var hints = GetHintLines(
            EnumerateExternalHintPaths(runtimePaths.DataDirectory, i18n.Locale),
            GetBundledHintDirectory(),
            i18n);

        return hints.Count > 0
            ? hints[Random.Shared.Next(hints.Count)]
            : i18n.T("launch.homepage.random_hint.fallback");
    }

    public static IReadOnlyList<string> GetHintLines(
        IEnumerable<string> candidatePaths,
        string bundledHintDirectory,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(candidatePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundledHintDirectory);
        ArgumentNullException.ThrowIfNull(i18n);

        foreach (var path in candidatePaths.Concat(EnumerateBundledHintPaths(bundledHintDirectory, i18n.Locale)))
        {
            var lines = TryReadHintLines(path);
            if (lines.Count > 0)
            {
                return lines;
            }
        }

        return FallbackHintKeys
            .Select(i18n.T)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    public static IEnumerable<string> EnumerateExternalHintPaths(string dataDirectory, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        foreach (var fileName in EnumerateHintFileNames(locale))
        {
            yield return Path.Combine(dataDirectory, fileName);
        }
    }

    public static string GetBundledHintDirectory()
    {
        return FrontendLauncherAssetLocator.GetPath("Resources", "Hints");
    }

    private static IReadOnlyList<string> TryReadHintLines(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateBundledHintPaths(string bundledHintDirectory, string locale)
    {
        foreach (var fileName in EnumerateHintFileNames(locale))
        {
            yield return Path.Combine(bundledHintDirectory, fileName);
        }
    }

    private static IEnumerable<string> EnumerateHintFileNames(string locale)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(locale))
        {
            candidates.Add($"hints.{locale}.txt");
            var language = locale.Split('-', 2)[0];
            if (!string.Equals(language, locale, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add($"hints.{language}.txt");
            }
        }

        candidates.Add("hints.txt");
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
