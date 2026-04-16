namespace PCL.Frontend.Avalonia.Models;

internal enum FrontendHelpEventType
{
    Unknown = 0,
    OpenWeb,
    OpenFile,
    OpenHelp,
    CopyText,
    DownloadFile,
    Popup,
    LaunchGame,
    MemoryOptimize,
    ClearRubbish,
    RefreshHomepage
}

internal static class FrontendHelpEventTypeResolver
{
    public static FrontendHelpEventType Resolve(string? rawEventType)
    {
        return rawEventType?.Trim() switch
        {
            "open_web" or "打开网页" => FrontendHelpEventType.OpenWeb,
            "open_file" or "打开文件" or "执行命令" => FrontendHelpEventType.OpenFile,
            "open_help" or "打开帮助" => FrontendHelpEventType.OpenHelp,
            "copy_text" or "复制文本" => FrontendHelpEventType.CopyText,
            "download_file" or "下载文件" => FrontendHelpEventType.DownloadFile,
            "popup" or "弹出窗口" => FrontendHelpEventType.Popup,
            "launch_game" or "启动游戏" => FrontendHelpEventType.LaunchGame,
            "memory_optimize" or "内存优化" => FrontendHelpEventType.MemoryOptimize,
            "clear_rubbish" or "清理垃圾" => FrontendHelpEventType.ClearRubbish,
            "refresh_homepage" or "刷新主页" => FrontendHelpEventType.RefreshHomepage,
            _ => FrontendHelpEventType.Unknown
        };
    }

    public static bool UsesResolvedTarget(string? rawEventType)
    {
        return Resolve(rawEventType) is FrontendHelpEventType.OpenWeb
            or FrontendHelpEventType.OpenFile
            or FrontendHelpEventType.DownloadFile;
    }
}
