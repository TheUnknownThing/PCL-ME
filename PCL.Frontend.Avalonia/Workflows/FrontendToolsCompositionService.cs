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
            BuildTestState(sharedConfig, runtimePaths, locale));
    }

    public static FrontendToolsHelpState LoadHelpState(FrontendRuntimePaths runtimePaths, string locale)
    {
        return BuildHelpState(runtimePaths, locale);
    }

    private static FrontendToolsTestState BuildTestState(JsonFileProvider sharedConfig, FrontendRuntimePaths runtimePaths, string locale)
    {
        var configuredFolder = ReadValue(sharedConfig, "CacheDownloadFolder", string.Empty).Trim();
        var downloadFolder = string.IsNullOrWhiteSpace(configuredFolder)
            ? Path.Combine(runtimePaths.DataDirectory, "MyDownload")
            : configuredFolder;

        return new FrontendToolsTestState(
            ToolboxActions:
            [
                new FrontendToolboxActionDefinition(
                    "memory-optimize",
                    LocalizeToolboxTitle(locale, "Mem Opt", "内存优化", "記憶體最佳化"),
                    LocalizeToolboxTooltip(
                        locale,
                        "Memory optimization is tuned for the PCL-ME build.\n\nIt can reduce physical memory pressure by roughly one third, not just for Minecraft.\nOn a mechanical drive this may cause a brief period of heavy stutter.\nStarting PCL with the --memory option can run the optimization silently.",
                        "内存优化为 PCL-ME 特供版，效果加强！\n\n将物理内存占用降低约 1/3，不仅限于 MC！\n如果使用机械硬盘，这可能会导致一小段时间的严重卡顿。\n使用 --memory 参数启动 PCL 可以静默执行内存优化。",
                        "記憶體最佳化為 PCL-ME 特供版，效果加強！\n\n將物理記憶體占用降低約 1/3，不僅限於 MC！\n如果使用機械硬碟，這可能會導致一小段時間的嚴重卡頓。\n使用 --memory 參數啟動 PCL 可以靜默執行記憶體最佳化。"),
                    100,
                    false),
                new FrontendToolboxActionDefinition(
                    "clear-rubbish",
                    LocalizeToolboxTitle(locale, "Clear Game Junk", "清理游戏垃圾", "清理遊戲垃圾"),
                    LocalizeToolboxTooltip(
                        locale,
                        "Clean PCL cache, Minecraft logs, crash reports, and other junk files.",
                        "清理 PCL 的缓存与 MC 的日志、崩溃报告等垃圾文件",
                        "清理 PCL 的快取與 MC 的日誌、崩潰報告等垃圾檔案"),
                    120,
                    false),
                new FrontendToolboxActionDefinition(
                    "daily-luck",
                    LocalizeToolboxTitle(locale, "Today's Luck", "今日人品", "今日人品"),
                    LocalizeToolboxTooltip(
                        locale,
                        "Check today's luck score.",
                        "查看今天的人品值。",
                        "查看今天的人品值。"),
                    100,
                    false),
                new FrontendToolboxActionDefinition(
                    "crash-test",
                    LocalizeToolboxTitle(locale, "Crash Test", "崩溃测试", "崩潰測試"),
                    LocalizeToolboxTooltip(
                        locale,
                        "Pressing this button will crash the launcher immediately. Don't use it unless you really mean to, and any resulting issues will be closed.",
                        "点这个按钮会让启动器直接崩掉，没事别点，造成的一切问题均不受理，相关 issue 会被直接关闭",
                        "點這個按鈕會讓啟動器直接崩掉，沒事別點，造成的一切問題均不受理，相關 issue 會被直接關閉"),
                    100,
                    true),
                new FrontendToolboxActionDefinition(
                    "create-shortcut",
                    LocalizeToolboxTitle(locale, "Create Shortcut", "创建快捷方式", "建立快捷方式"),
                    LocalizeToolboxTooltip(
                        locale,
                        "Create a shortcut that points to the PCL-ME executable.",
                        "创建一个指向 PCL-ME 可执行文件的快捷方式",
                        "建立一個指向 PCL-ME 可執行檔的快捷方式"),
                    120,
                    false),
                new FrontendToolboxActionDefinition(
                    "launch-count",
                    LocalizeToolboxTitle(locale, "Launch Count", "查看启动计数", "查看啟動計數"),
                    LocalizeToolboxTooltip(
                        locale,
                        "See how many times PCL has started the game for you.",
                        "查看 PCL 已经为你启动了多少次游戏。",
                        "查看 PCL 已經為你啟動了多少次遊戲。"),
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
        var overrideRoot = Path.Combine(runtimePaths.DataDirectory, "Help");
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
                new FrontendToolsHelpEntry(["指南"], LocalizeHelpTitle(locale, "How to choose an instance", "如何选择实例", "如何選擇實例"), LocalizeHelpBody(locale, "Open the instance picker from the launch page, then return to the main launcher panel and continue launching.", "从启动页进入实例选择，然后再返回主启动面板继续启动。", "從啟動頁進入實例選擇，然後再返回主啟動面板繼續啟動。"), LocalizeHelpKeywords(locale, "instance, launch, version", "实例, 启动, 版本", "實例, 啟動, 版本"), null, "fallback://launch/select-instance", "fallback://launch/select-instance", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["指南"], LocalizeHelpTitle(locale, "Java download tips", "Java 下载提示", "Java 下載提示"), LocalizeHelpBody(locale, "If Java is missing, you can download a compatible runtime from the prompt and select it.", "Java 缺失时，可以按提示下载并选择可用运行时。", "Java 缺失時，可以按提示下載並選擇可用執行環境。"), LocalizeHelpKeywords(locale, "Java, runtime, download", "Java, 运行时, 下载", "Java, 執行環境, 下載"), null, "fallback://launch/java-runtime", "fallback://launch/java-runtime", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["启动器"], LocalizeHelpTitle(locale, "Export logs", "导出日志", "匯出日誌"), LocalizeHelpBody(locale, "You can export the current log or the full history archive from the log page in settings.", "可以在设置的日志页导出当前日志或全部历史日志压缩包。", "可以在設定的日誌頁匯出目前日誌或全部歷史日誌壓縮檔。"), LocalizeHelpKeywords(locale, "log, export, diagnostics", "日志, 导出, 诊断", "日誌, 匯出, 診斷"), null, "fallback://diagnostics/log-export", "fallback://diagnostics/log-export", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["启动器"], LocalizeHelpTitle(locale, "Crash recovery tips", "崩溃恢复提示", "崩潰恢復提示"), LocalizeHelpBody(locale, "After a crash you can review the log, export a report, and follow the recovery prompt.", "发生崩溃后，可以查看日志、导出报告并按提示恢复。", "發生崩潰後，可以查看日誌、匯出報告並按提示恢復。"), LocalizeHelpKeywords(locale, "crash, recovery, log", "崩溃, 恢复, 日志", "崩潰, 恢復, 日誌"), null, "fallback://diagnostics/crash-recovery", "fallback://diagnostics/crash-recovery", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["帮助"], LocalizeHelpTitle(locale, "Page layout notes", "页面布局说明", "頁面布局說明"), LocalizeHelpBody(locale, "The new page layout tries to keep common actions in a familiar order and grouping so it is easier to keep using.", "新版页面会尽量保持常用操作的顺序与分组，方便继续使用。", "新版頁面會盡量保持常用操作的順序與分組，方便繼續使用。"), LocalizeHelpKeywords(locale, "page, layout, actions", "页面, 布局, 操作", "頁面, 佈局, 操作"), null, "fallback://help/page-layout", "fallback://help/page-layout", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["帮助"], LocalizeHelpTitle(locale, "What to check before launch", "启动前检查什么", "啟動前檢查什麼"), LocalizeHelpBody(locale, "Before launching, make sure the instance, account, Java, and prompt messages are correct.", "启动前建议确认实例、账号、Java 和提示信息是否正确。", "啟動前建議確認實例、帳號、Java 和提示訊息是否正確。"), LocalizeHelpKeywords(locale, "launch, checklist, Java", "启动, 检查, Java", "啟動, 檢查, Java"), null, "fallback://help/launch-checklist", "fallback://help/launch-checklist", true, true, true, false, null, null, null)
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
        var overrideRoot = Path.Combine(runtimePaths.DataDirectory, "Help");
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

    private static string LocalizeToolboxTitle(string locale, string english, string simplifiedChinese, string traditionalChinese)
    {
        return IsTraditionalChinese(locale)
            ? traditionalChinese
            : IsChinese(locale)
                ? simplifiedChinese
                : english;
    }

    private static string LocalizeToolboxTooltip(string locale, string english, string simplifiedChinese, string traditionalChinese)
    {
        return IsTraditionalChinese(locale)
            ? traditionalChinese
            : IsChinese(locale)
                ? simplifiedChinese
                : english;
    }

    private static string LocalizeHelpTitle(string locale, string english, string simplifiedChinese, string traditionalChinese)
        => LocalizeToolboxTitle(locale, english, simplifiedChinese, traditionalChinese);

    private static string LocalizeHelpBody(string locale, string english, string simplifiedChinese, string traditionalChinese)
        => LocalizeToolboxTooltip(locale, english, simplifiedChinese, traditionalChinese);

    private static string LocalizeHelpKeywords(string locale, string english, string simplifiedChinese, string traditionalChinese)
        => LocalizeToolboxTooltip(locale, english, simplifiedChinese, traditionalChinese);

    private static bool IsChinese(string locale)
    {
        return locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTraditionalChinese(string locale)
    {
        return locale.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)
               || locale.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
               || locale.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase);
    }
}
