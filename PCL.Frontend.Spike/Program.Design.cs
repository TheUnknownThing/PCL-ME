using Avalonia;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Desktop;

public partial class Program
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return SpikeDesktopHost.BuildAvaloniaApp(CreateDesignTimeOptions());
    }

    private static SpikeCommandOptions CreateDesignTimeOptions()
    {
        return new SpikeCommandOptions(
            SpikeCommandKind.App,
            Scenario: "modern-fabric",
            Mode: SpikeOutputMode.Plan,
            Format: SpikeOutputFormat.Json,
            UseHostEnvironment: false,
            JavaPromptDecision: MinecraftLaunchJavaPromptDecision.Download,
            JavaDownloadState: SpikeJavaDownloadSessionState.Finished,
            CrashAction: MinecraftCrashOutputPromptActionKind.ExportReport,
            ForceCjkFontWarning: false,
            SaveBatchPath: null,
            WorkspaceRoot: null,
            InputRoot: null,
            ExportArchivePath: null);
    }
}
