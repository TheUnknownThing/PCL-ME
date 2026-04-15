using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendToolsCompositionService
{
    private static readonly string LauncherRootDirectory = FrontendLauncherAssetLocator.RootDirectory;
    private const string EnglishFallbackLocale = "en-US";

    public static FrontendToolsComposition Compose(FrontendRuntimePaths runtimePaths, string locale)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        return new FrontendToolsComposition(
            LoadHelpState(runtimePaths, locale),
            BuildTestState(sharedConfig, runtimePaths));
    }

    public static FrontendToolsHelpState LoadHelpState(FrontendRuntimePaths runtimePaths, string locale)
    {
        return BuildHelpState(runtimePaths, locale);
    }

    private static FrontendToolsTestState BuildTestState(JsonFileProvider sharedConfig, FrontendRuntimePaths runtimePaths)
    {
        var configuredFolder = ReadValue(sharedConfig, "CacheDownloadFolder", string.Empty).Trim();
        var downloadFolder = string.IsNullOrWhiteSpace(configuredFolder)
            ? Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "MyDownload")
            : configuredFolder;

        return new FrontendToolsTestState(
            ToolboxActions:
            [
                new FrontendToolboxActionDefinition(
                    "memory-optimize",
                    "内存优化",
                    "内存优化为 PCL-ME 特供版，效果加强！\n\n将物理内存占用降低约 1/3，不仅限于 MC！\n如果使用机械硬盘，这可能会导致一小段时间的严重卡顿。\n使用 --memory 参数启动 PCL 可以静默执行内存优化。",
                    100,
                    false),
                new FrontendToolboxActionDefinition(
                    "clear-rubbish",
                    "清理游戏垃圾",
                    "清理 PCL 的缓存与 MC 的日志、崩溃报告等垃圾文件",
                    120,
                    false),
                new FrontendToolboxActionDefinition(
                    "daily-luck",
                    "今日人品",
                    "查看今天的人品值。",
                    100,
                    false),
                new FrontendToolboxActionDefinition(
                    "crash-test",
                    "崩溃测试",
                    "点这个按钮会让启动器直接崩掉，没事别点，造成的一切问题均不受理，相关 issue 会被直接关闭",
                    100,
                    true),
                new FrontendToolboxActionDefinition(
                    "create-shortcut",
                    "创建快捷方式",
                    "创建一个指向 PCL-ME 可执行文件的快捷方式",
                    120,
                    false),
                new FrontendToolboxActionDefinition(
                    "launch-count",
                    "查看启动计数",
                    "查看 PCL 已经为你启动了多少次游戏。",
                    120,
                    false)
            ],
            DownloadUrl: string.Empty,
            DownloadUserAgent: ReadValue(sharedConfig, "ToolDownloadCustomUserAgent", string.Empty),
            DownloadFolder: downloadFolder,
            DownloadName: string.Empty,
            OfficialSkinPlayerName: string.Empty,
            AchievementBlockId: string.Empty,
            AchievementTitle: string.Empty,
            AchievementFirstLine: string.Empty,
            AchievementSecondLine: string.Empty,
            ShowAchievementPreview: false,
            SelectedHeadSizeIndex: 0,
            SelectedHeadSkinPath: string.Empty);
    }

    private static FrontendToolsHelpState BuildHelpState(FrontendRuntimePaths runtimePaths, string locale)
    {
        var entryCandidates = new List<HelpEntryCandidate>();
        var detailCandidates = new List<HelpDetailCandidate>();
        var ignorePatterns = ReadHelpIgnorePatterns(runtimePaths);
        var overrideRoot = Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Help");
        var bundledHelpRoot = Path.Combine(LauncherRootDirectory, "Resources", "Help");

        if (Directory.Exists(overrideRoot))
        {
            CollectHelpDirectoryCandidates(
                overrideRoot,
                ignorePatterns,
                priority: 0,
                entryCandidates,
                detailCandidates);
        }

        if (Directory.Exists(bundledHelpRoot))
        {
            CollectHelpDirectoryCandidates(
                bundledHelpRoot,
                ignorePatterns,
                priority: 1,
                entryCandidates,
                detailCandidates);
        }

        var bundledZipPath = Path.Combine(LauncherRootDirectory, "Resources", "Help.zip");
        if (File.Exists(bundledZipPath))
        {
            try
            {
                using var archive = ZipFile.OpenRead(bundledZipPath);
                foreach (var entry in archive.Entries.Where(item => item.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    CollectZipHelpEntryCandidate(
                        archive,
                        entry,
                        bundledZipPath,
                        ignorePatterns,
                        priority: 2,
                        entryCandidates,
                        detailCandidates);
                }
            }
            catch
            {
                // Fall back to the hard-coded emergency topics below.
            }
        }

        var entries = BuildResolvedHelpEntries(entryCandidates, detailCandidates, locale);
        if (entries.Count == 0)
        {
            entries.AddRange(
            [
                new FrontendToolsHelpEntry(["指南"], "如何选择实例", "从启动页进入实例选择，然后再返回主启动面板继续启动。", "实例, 启动, 版本", null, "fallback://launch/select-instance", "fallback://launch/select-instance", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["指南"], "Java 下载提示", "Java 缺失时，可以按提示下载并选择可用运行时。", "Java, 运行时, 下载", null, "fallback://launch/java-runtime", "fallback://launch/java-runtime", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["启动器"], "导出日志", "可以在设置的日志页导出当前日志或全部历史日志压缩包。", "日志, 导出, 诊断", null, "fallback://diagnostics/log-export", "fallback://diagnostics/log-export", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["启动器"], "崩溃恢复提示", "发生崩溃后，可以查看日志、导出报告并按提示恢复。", "崩溃, 恢复, 日志", null, "fallback://diagnostics/crash-recovery", "fallback://diagnostics/crash-recovery", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["帮助"], "页面布局说明", "新版页面会尽量保持常用操作的顺序与分组，方便继续使用。", "页面, 布局, 操作", null, "fallback://help/page-layout", "fallback://help/page-layout", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["帮助"], "启动前检查什么", "启动前建议确认实例、账号、Java 和提示信息是否正确。", "启动, 检查, Java", null, "fallback://help/launch-checklist", "fallback://help/launch-checklist", true, true, true, false, null, null, null)
            ]);
        }

        return new FrontendToolsHelpState(entries);
    }

    private static List<FrontendToolsHelpEntry> BuildResolvedHelpEntries(
        IReadOnlyList<HelpEntryCandidate> entryCandidates,
        IReadOnlyList<HelpDetailCandidate> detailCandidates,
        string locale)
    {
        var resolvedEntries = new List<FrontendToolsHelpEntry>();
        var groupedEntries = entryCandidates
            .GroupBy(candidate => candidate.ReferencePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groupedEntries)
        {
            var selectedEntry = SelectBestLocalizedCandidate(group, locale);
            if (selectedEntry is null)
            {
                continue;
            }

            var selectedDetail = SelectBestLocalizedCandidate(
                detailCandidates.Where(candidate =>
                    string.Equals(candidate.ReferencePath, group.Key, StringComparison.OrdinalIgnoreCase)),
                locale);

            try
            {
                resolvedEntries.Add(ReadHelpEntry(
                    selectedEntry.JsonContent,
                    selectedEntry.ReferencePath,
                    selectedEntry.SourcePath,
                    selectedDetail?.Content));
            }
            catch
            {
                // Ignore malformed entries and keep loading the rest.
            }
        }

        return resolvedEntries;
    }

    private static void CollectHelpDirectoryCandidates(
        string rootDirectory,
        IReadOnlyList<string> ignorePatterns,
        int priority,
        ICollection<HelpEntryCandidate> entryCandidates,
        ICollection<HelpDetailCandidate> detailCandidates)
    {
        foreach (var filePath in Directory.EnumerateFiles(rootDirectory, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var relativePath = Path.GetRelativePath(rootDirectory, filePath).Replace('\\', '/');
                var assetPath = ParseHelpAssetPath(relativePath);
                if (MatchesIgnorePattern(relativePath, ignorePatterns, assetPath.ReferencePath))
                {
                    continue;
                }

                entryCandidates.Add(new HelpEntryCandidate(
                    assetPath.ReferencePath,
                    filePath,
                    assetPath.Locale,
                    priority,
                    File.ReadAllText(filePath)));

                var xamlPath = Path.ChangeExtension(filePath, ".xaml");
                if (!string.IsNullOrWhiteSpace(xamlPath) && File.Exists(xamlPath))
                {
                    detailCandidates.Add(new HelpDetailCandidate(
                        assetPath.ReferencePath,
                        xamlPath,
                        assetPath.Locale,
                        priority,
                        File.ReadAllText(xamlPath)));
                }
            }
            catch
            {
                // Ignore malformed override entries and keep loading the rest.
            }
        }
    }

    private static void CollectZipHelpEntryCandidate(
        ZipArchive archive,
        ZipArchiveEntry entry,
        string zipPath,
        IReadOnlyList<string> ignorePatterns,
        int priority,
        ICollection<HelpEntryCandidate> entryCandidates,
        ICollection<HelpDetailCandidate> detailCandidates)
    {
        var assetPath = ParseHelpAssetPath(entry.FullName);
        if (MatchesIgnorePattern(entry.FullName, ignorePatterns, assetPath.ReferencePath))
        {
            return;
        }

        using (var stream = entry.Open())
        using (var reader = new StreamReader(stream))
        {
            entryCandidates.Add(new HelpEntryCandidate(
                assetPath.ReferencePath,
                $"{zipPath}::{entry.FullName}",
                assetPath.Locale,
                priority,
                reader.ReadToEnd()));
        }

        var xamlEntryPath = Path.ChangeExtension(entry.FullName, ".xaml")?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(xamlEntryPath))
        {
            return;
        }

        var xamlEntry = archive.Entries.FirstOrDefault(item =>
            string.Equals(item.FullName, xamlEntryPath, StringComparison.OrdinalIgnoreCase));
        if (xamlEntry is null)
        {
            return;
        }

        using var xamlStream = xamlEntry.Open();
        using var xamlReader = new StreamReader(xamlStream);
        detailCandidates.Add(new HelpDetailCandidate(
            assetPath.ReferencePath,
            $"{zipPath}::{xamlEntry.FullName}",
            assetPath.Locale,
            priority,
            xamlReader.ReadToEnd()));
    }

    private static IReadOnlyList<string> ReadHelpIgnorePatterns(FrontendRuntimePaths runtimePaths)
    {
        var overrideRoot = Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Help");
        if (!Directory.Exists(overrideRoot))
        {
            return [];
        }

        var patterns = new List<string>();
        foreach (var filePath in Directory.EnumerateFiles(overrideRoot, ".helpignore", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(filePath))
            {
                var content = line.Split('#', 2)[0].Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    patterns.Add(content);
                }
            }
        }

        return patterns;
    }

    private static bool MatchesIgnorePattern(string relativePath, IReadOnlyList<string> ignorePatterns, string? referencePath = null)
    {
        foreach (var pattern in ignorePatterns)
        {
            try
            {
                if (Regex.IsMatch(relativePath, pattern, RegexOptions.IgnoreCase)
                    || (!string.IsNullOrWhiteSpace(referencePath) && Regex.IsMatch(referencePath, pattern, RegexOptions.IgnoreCase)))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore invalid patterns copied from user override folders.
            }
        }

        return false;
    }

    private static FrontendToolsHelpEntry ReadHelpEntry(
        string json,
        string rawPath,
        string sourcePath,
        string? detailContent)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var title = ReadString(root, "Title");
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidDataException($"Help entry is missing a title: {rawPath}");
        }

        var types = root.TryGetProperty("Types", out var typesElement) && typesElement.ValueKind == JsonValueKind.Array
            ? typesElement.EnumerateArray()
                .Select(item => item.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray()
            : [];

        return new FrontendToolsHelpEntry(
            GroupTitles: types,
            Title: title,
            Summary: ReadString(root, "Description"),
            Keywords: ReadString(root, "Keywords"),
            Logo: ReadString(root, "Logo"),
            RawPath: rawPath,
            SourcePath: sourcePath,
            ShowInSearch: ReadBool(root, "ShowInSearch", defaultValue: true),
            ShowInPublic: ReadBool(root, "ShowInPublic", defaultValue: true),
            ShowInSnapshot: ReadBool(root, "ShowInSnapshot", defaultValue: true),
            IsEvent: ReadBool(root, "IsEvent"),
            EventType: ReadString(root, "EventType"),
            EventData: ReadString(root, "EventData"),
            DetailContent: detailContent);
    }

    private static TCandidate? SelectBestLocalizedCandidate<TCandidate>(
        IEnumerable<TCandidate> candidates,
        string locale)
        where TCandidate : IHelpAssetCandidate
    {
        var candidateList = candidates.ToArray();
        if (candidateList.Length == 0)
        {
            return default;
        }

        foreach (var preferredLocale in EnumerateHelpLocalePreferences(locale))
        {
            var match = candidateList
                .Where(candidate => string.Equals(candidate.Locale, preferredLocale, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.Priority)
                .FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return candidateList
            .OrderBy(candidate => candidate.Priority)
            .First();
    }

    private static IReadOnlyList<string?> EnumerateHelpLocalePreferences(string locale)
    {
        var normalizedLocale = NormalizeHelpLocale(locale);
        var locales = new List<string?>();
        if (!string.IsNullOrWhiteSpace(normalizedLocale))
        {
            locales.Add(normalizedLocale);
            var languageOnly = normalizedLocale.Split('-', 2)[0];
            if (!string.Equals(languageOnly, normalizedLocale, StringComparison.OrdinalIgnoreCase))
            {
                locales.Add(languageOnly);
            }
        }

        if (!string.Equals(normalizedLocale, EnglishFallbackLocale, StringComparison.OrdinalIgnoreCase))
        {
            locales.Add(EnglishFallbackLocale);
            locales.Add("en");
        }

        locales.Add(null);
        return locales
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HelpAssetPath ParseHelpAssetPath(string relativePath)
    {
        var normalized = NormalizeHelpReference(relativePath);
        var slashIndex = normalized.IndexOf('/');
        if (slashIndex > 0)
        {
            var leadingSegment = normalized[..slashIndex];
            var normalizedLocale = NormalizeHelpLocale(leadingSegment);
            if (!string.IsNullOrWhiteSpace(normalizedLocale))
            {
                return new HelpAssetPath(normalized[(slashIndex + 1)..], normalizedLocale);
            }
        }

        return new HelpAssetPath(normalized, null);
    }

    private static string NormalizeHelpReference(string reference)
    {
        return reference.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static string? NormalizeHelpLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var rawSegments = locale
            .Trim()
            .Replace('_', '-')
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rawSegments.Length == 0
            || rawSegments.Any(segment => !segment.All(char.IsLetterOrDigit))
            || rawSegments[0].Length is < 2 or > 3
            || !rawSegments[0].All(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
        {
            return null;
        }

        var normalizedSegments = new List<string>(rawSegments.Length);
        for (var i = 0; i < rawSegments.Length; i++)
        {
            var segment = rawSegments[i];
            if (i == 0)
            {
                normalizedSegments.Add(segment.ToLowerInvariant());
            }
            else if (segment.Length == 4 && segment.All(char.IsLetter))
            {
                normalizedSegments.Add(char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant());
            }
            else if ((segment.Length == 2 || segment.Length == 3) && segment.All(char.IsLetter))
            {
                normalizedSegments.Add(segment.ToUpperInvariant());
            }
            else
            {
                normalizedSegments.Add(segment);
            }
        }

        return string.Join('-', normalizedSegments);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return defaultValue;
        }

        return property.GetBoolean();
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private interface IHelpAssetCandidate
    {
        string? Locale { get; }

        int Priority { get; }
    }

    private sealed record HelpAssetPath(string ReferencePath, string? Locale);

    private sealed record HelpEntryCandidate(
        string ReferencePath,
        string SourcePath,
        string? Locale,
        int Priority,
        string JsonContent) : IHelpAssetCandidate;

    private sealed record HelpDetailCandidate(
        string ReferencePath,
        string SourcePath,
        string? Locale,
        int Priority,
        string Content) : IHelpAssetCandidate;
}
