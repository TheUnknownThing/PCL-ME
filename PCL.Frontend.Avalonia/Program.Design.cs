using Avalonia;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop;

public partial class Program
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AvaloniaDesktopHost.BuildAvaloniaApp(CreateDesignTimeOptions());
    }

    private static AvaloniaCommandOptions CreateDesignTimeOptions()
    {
        return new AvaloniaCommandOptions(
            AvaloniaCommandKind.App,
            Scenario: "modern-fabric",
            Mode: AvaloniaOutputMode.Plan,
            Format: AvaloniaOutputFormat.Json,
            UseHostEnvironment: false,
            JavaPromptDecision: MinecraftLaunchJavaPromptDecision.Download,
            JavaDownloadState: AvaloniaJavaDownloadSessionState.Finished,
            CrashAction: MinecraftCrashOutputPromptActionKind.ExportReport,
            ForceCjkFontWarning: false,
            SaveBatchPath: null,
            WorkspaceRoot: null,
            InputRoot: null,
            ExportArchivePath: null);
    }
}
