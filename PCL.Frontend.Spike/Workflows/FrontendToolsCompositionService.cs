using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

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
                new FrontendToolsSimpleEntry("PCL CE 大厅相关隐私政策", "了解 PCL CE 如何处理您的个人信息"),
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
                    "内存优化为 PCL CE 特供版，效果加强！\n\n将物理内存占用降低约 1/3，不仅限于 MC！\n如果使用机械硬盘，这可能会导致一小段时间的严重卡顿。\n使用 --memory 参数启动 PCL 可以静默执行内存优化。",
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
                    string.Empty,
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
                    "创建一个指向 PCL 社区版可执行文件的快捷方式",
                    120,
                    false),
                new FrontendToolboxActionDefinition(
                    "launch-count",
                    "查看启动计数",
                    string.Empty,
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
                new FrontendToolsHelpEntry("启动与版本", "如何选择实例", "从启动页进入实例选择，然后再返回主启动面板继续启动。", "fallback://launch/select-instance", false, null, null),
                new FrontendToolsHelpEntry("启动与版本", "Java 下载提示", "Java 缺失时由后端给出下载提示，前端只负责渲染选择与跳转。", "fallback://launch/java-runtime", false, null, null),
                new FrontendToolsHelpEntry("诊断与恢复", "导出日志", "可以在设置的日志页导出当前日志或全部历史日志压缩包。", "fallback://diagnostics/log-export", false, null, null),
                new FrontendToolsHelpEntry("诊断与恢复", "崩溃恢复提示", "崩溃报告、导出与恢复动作都通过可移植提示合同提供给壳层。", "fallback://diagnostics/crash-recovery", false, null, null),
                new FrontendToolsHelpEntry("迁移说明", "为什么先复制原页面", "当前目标是保持 PCL 的页面结构和控件语言，而不是重新设计。", "fallback://migration/copy-original", false, null, null),
                new FrontendToolsHelpEntry("迁移说明", "哪些逻辑不应放回前端", "启动、登录、Java 与崩溃策略仍应保留在后端服务中。", "fallback://migration/backend-boundary", false, null, null)
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
            GroupTitle: types.FirstOrDefault() ?? "帮助",
            Title: title,
            Summary: ReadString(root, "Description"),
            RawPath: rawPath,
            IsEvent: ReadBool(root, "IsEvent"),
            EventType: ReadString(root, "EventType"),
            EventData: ReadString(root, "EventData"));
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            && property.GetBoolean();
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
