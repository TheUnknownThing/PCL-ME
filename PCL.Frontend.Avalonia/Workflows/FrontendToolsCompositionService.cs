using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendToolsCompositionService
{
    private static readonly string LauncherRootDirectory = FrontendLauncherAssetLocator.RootDirectory;

    public static FrontendToolsComposition Compose(FrontendRuntimePaths runtimePaths, FrontendInstanceComposition instanceComposition)
    {
        var sharedConfig = new JsonFileProvider(runtimePaths.SharedConfigPath);
        return new FrontendToolsComposition(
            BuildGameLinkState(sharedConfig, instanceComposition),
            BuildHelpState(runtimePaths),
            BuildTestState(sharedConfig, runtimePaths));
    }

    private static FrontendToolsGameLinkState BuildGameLinkState(JsonFileProvider sharedConfig, FrontendInstanceComposition instanceComposition)
    {
        var configuredUserName = ReadValue(sharedConfig, "LinkUsername", string.Empty).Trim();
        var hasAcceptedTerms = ReadValue(sharedConfig, "LinkEula", false);
        var worldOptions = BuildWorldOptions(instanceComposition);

        var announcement = hasAcceptedTerms
            ? worldOptions.Count == 0
                ? "尚未检测到可用于创建大厅的世界，请先确认当前实例中存在可分享的存档。"
                : $"已检测到 {worldOptions.Count} 个可用于创建大厅的世界。"
            : "请先阅读并同意联机大厅说明与条款。";

        return new FrontendToolsGameLinkState(
            Announcement: announcement,
            NatStatus: "点击测试",
            AccountStatus: string.IsNullOrWhiteSpace(configuredUserName) ? "点击登录 Natayark 账户" : configuredUserName,
            LobbyId: string.Empty,
            SessionPing: "-ms",
            SessionId: "尚未创建大厅",
            ConnectionType: hasAcceptedTerms ? "未连接" : "等待授权",
            ConnectedUserName: string.IsNullOrWhiteSpace(configuredUserName) ? "未登录" : configuredUserName,
            ConnectedUserType: string.IsNullOrWhiteSpace(configuredUserName) ? "大厅访客" : "已配置用户名",
            WorldOptions: worldOptions.Count == 0 ? ["未检测到可用存档"] : worldOptions,
            SelectedWorldIndex: 0,
            PolicyEntries:
            [
                new FrontendToolsSimpleEntry("PCL-ME 大厅相关隐私政策", "了解 PCL-ME 如何处理您的个人信息"),
                new FrontendToolsSimpleEntry("Natayark Network 用户协议与隐私政策", "查看 Natayark OpenID 服务条款")
            ],
            PlayerEntries: []);
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
            SelectedHeadSkinPath: "尚未选择皮肤");
    }

    private static FrontendToolsHelpState BuildHelpState(FrontendRuntimePaths runtimePaths)
    {
        var entries = new List<FrontendToolsHelpEntry>();
        var ignorePatterns = ReadHelpIgnorePatterns(runtimePaths);
        var overrideRoot = Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Help");

        if (Directory.Exists(overrideRoot))
        {
            foreach (var filePath in Directory.EnumerateFiles(overrideRoot, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    entries.Add(ReadHelpEntry(File.ReadAllText(filePath), filePath));
                }
                catch
                {
                    // Ignore malformed override entries and keep loading the rest.
                }
            }
        }

        var bundledZipPath = Path.Combine(LauncherRootDirectory, "Resources", "Help.zip");
        if (File.Exists(bundledZipPath))
        {
            try
            {
                using var archive = ZipFile.OpenRead(bundledZipPath);
                foreach (var entry in archive.Entries.Where(item => item.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    if (MatchesIgnorePattern(entry.FullName, ignorePatterns))
                    {
                        continue;
                    }

                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    entries.Add(ReadHelpEntry(reader.ReadToEnd(), $"{bundledZipPath}::{entry.FullName}"));
                }
            }
            catch
            {
                // Fall back to the hard-coded emergency topics below.
            }
        }

        if (entries.Count == 0)
        {
            entries.AddRange(
            [
                new FrontendToolsHelpEntry(["指南"], "如何选择实例", "从启动页进入实例选择，然后再返回主启动面板继续启动。", "实例, 启动, 版本", "fallback://launch/select-instance", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["指南"], "Java 下载提示", "Java 缺失时，可以按提示下载并选择可用运行时。", "Java, 运行时, 下载", "fallback://launch/java-runtime", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["启动器"], "导出日志", "可以在设置的日志页导出当前日志或全部历史日志压缩包。", "日志, 导出, 诊断", "fallback://diagnostics/log-export", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["启动器"], "崩溃恢复提示", "发生崩溃后，可以查看日志、导出报告并按提示恢复。", "崩溃, 恢复, 日志", "fallback://diagnostics/crash-recovery", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["帮助"], "页面布局说明", "新版页面会尽量保持常用操作的顺序与分组，方便继续使用。", "页面, 布局, 操作", "fallback://help/page-layout", true, true, true, false, null, null, null),
                new FrontendToolsHelpEntry(["帮助"], "启动前检查什么", "启动前建议确认实例、账号、Java 和提示信息是否正确。", "启动, 检查, Java", "fallback://help/launch-checklist", true, true, true, false, null, null, null)
            ]);
        }

        return new FrontendToolsHelpState(entries);
    }

    private static IReadOnlyList<string> BuildWorldOptions(FrontendInstanceComposition instanceComposition)
    {
        return instanceComposition.World.Entries
            .Select((entry, index) => $"{entry.Title} - {25565 + index}")
            .ToArray();
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

    private static bool MatchesIgnorePattern(string relativePath, IReadOnlyList<string> ignorePatterns)
    {
        foreach (var pattern in ignorePatterns)
        {
            try
            {
                if (Regex.IsMatch(relativePath, pattern, RegexOptions.IgnoreCase))
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

    private static FrontendToolsHelpEntry ReadHelpEntry(string json, string rawPath)
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
            RawPath: rawPath,
            ShowInSearch: ReadBool(root, "ShowInSearch", defaultValue: true),
            ShowInPublic: ReadBool(root, "ShowInPublic", defaultValue: true),
            ShowInSnapshot: ReadBool(root, "ShowInSnapshot", defaultValue: true),
            IsEvent: ReadBool(root, "IsEvent"),
            EventType: ReadString(root, "EventType"),
            EventData: ReadString(root, "EventData"),
            DetailContent: ReadDetailContent(rawPath));
    }

    private static string? ReadDetailContent(string rawPath)
    {
        try
        {
            var zipMarkerIndex = rawPath.IndexOf("::", StringComparison.Ordinal);
            if (zipMarkerIndex >= 0)
            {
                var zipPath = rawPath[..zipMarkerIndex];
                var entryPath = rawPath[(zipMarkerIndex + 2)..];
                var xamlEntryPath = Path.ChangeExtension(entryPath, ".xaml")?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(xamlEntryPath) || !File.Exists(zipPath))
                {
                    return null;
                }

                using var archive = ZipFile.OpenRead(zipPath);
                var xamlEntry = archive.Entries.FirstOrDefault(item =>
                    string.Equals(item.FullName, xamlEntryPath, StringComparison.OrdinalIgnoreCase));
                if (xamlEntry is null)
                {
                    return null;
                }

                using var stream = xamlEntry.Open();
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }

            var xamlPath = Path.ChangeExtension(rawPath, ".xaml");
            return !string.IsNullOrWhiteSpace(xamlPath) && File.Exists(xamlPath)
                ? File.ReadAllText(xamlPath)
                : null;
        }
        catch
        {
            return null;
        }
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
}
