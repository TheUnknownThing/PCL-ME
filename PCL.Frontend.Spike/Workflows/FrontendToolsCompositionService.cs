using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendToolsCompositionService
{
    public static FrontendToolsComposition Compose(FrontendRuntimePaths runtimePaths)
    {
        var sharedConfig = new JsonFileProvider(runtimePaths.SharedConfigPath);
        return new FrontendToolsComposition(
            BuildTestState(sharedConfig, runtimePaths));
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
