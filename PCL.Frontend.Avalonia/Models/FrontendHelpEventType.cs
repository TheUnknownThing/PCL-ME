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
            "open_web" => FrontendHelpEventType.OpenWeb,
            "open_file" => FrontendHelpEventType.OpenFile,
            "open_help" => FrontendHelpEventType.OpenHelp,
            "copy_text" => FrontendHelpEventType.CopyText,
            "download_file" => FrontendHelpEventType.DownloadFile,
            "popup" => FrontendHelpEventType.Popup,
            "launch_game" => FrontendHelpEventType.LaunchGame,
            "memory_optimize" => FrontendHelpEventType.MemoryOptimize,
            "clear_rubbish" => FrontendHelpEventType.ClearRubbish,
            "refresh_homepage" => FrontendHelpEventType.RefreshHomepage,
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
