using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.Cli;

internal static class SpikeCommandParser
{
    public static SpikeParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return Success(new SpikeCommandOptions(
                SpikeCommandKind.All,
                Scenario: "modern-fabric",
                Mode: SpikeOutputMode.Plan,
                Format: SpikeOutputFormat.Json,
                UseHostEnvironment: false,
                JavaPromptDecision: MinecraftLaunchJavaPromptDecision.Download,
                JavaDownloadState: SpikeJavaDownloadSessionState.Finished,
                CrashAction: MinecraftCrashOutputPromptActionKind.ExportReport,
                SaveBatchPath: null,
                WorkspaceRoot: null,
                InputRoot: null,
                ExportArchivePath: null));
        }

        if (IsHelpToken(args[0]))
        {
            return new SpikeParseResult(null, ShowHelp: true, ErrorMessage: null);
        }

        if (!TryParseCommand(args[0], out var command))
        {
            return Error($"Unknown command '{args[0]}'.");
        }

        var scenario = "modern-fabric";
        var mode = SpikeOutputMode.Plan;
        var format = SpikeOutputFormat.Json;
        var useHostEnvironment = false;
        var javaPromptDecision = MinecraftLaunchJavaPromptDecision.Download;
        var javaDownloadState = SpikeJavaDownloadSessionState.Finished;
        var crashAction = MinecraftCrashOutputPromptActionKind.ExportReport;
        string? saveBatchPath = null;
        string? workspaceRoot = null;
        string? inputRoot = null;
        string? exportArchivePath = null;

        var index = 1;
        if (command is SpikeCommandKind.Launch or SpikeCommandKind.All &&
            index < args.Length &&
            !args[index].StartsWith("--", StringComparison.Ordinal))
        {
            scenario = args[index].Trim().ToLowerInvariant();
            index++;
        }

        while (index < args.Length)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                return Error($"Unexpected positional argument '{token}'.");
            }

            var (name, value, nextIndex, errorMessage) = ReadOption(args, index);
            if (errorMessage is not null)
            {
                return Error(errorMessage);
            }

            switch (name)
            {
                case "mode":
                    if (!TryParseMode(value!, out mode))
                    {
                        return Error($"Unknown mode '{value}'.");
                    }
                    break;
                case "format":
                    if (!TryParseFormat(value!, out format))
                    {
                        return Error($"Unknown format '{value}'.");
                    }
                    break;
                case "host-env":
                    if (!TryParseBooleanFlag(value!, out useHostEnvironment))
                    {
                        return Error($"Unknown host environment flag '{value}'.");
                    }
                    break;
                case "java-prompt":
                    if (!TryParseJavaPromptDecision(value!, out javaPromptDecision))
                    {
                        return Error($"Unknown Java prompt decision '{value}'.");
                    }
                    break;
                case "java-download-state":
                    if (!TryParseJavaDownloadState(value!, out javaDownloadState))
                    {
                        return Error($"Unknown Java download state '{value}'.");
                    }
                    break;
                case "crash-action":
                    if (!TryParseCrashAction(value!, out crashAction))
                    {
                        return Error($"Unknown crash action '{value}'.");
                    }
                    break;
                case "scenario":
                    scenario = value!.Trim().ToLowerInvariant();
                    break;
                case "workspace":
                    workspaceRoot = value;
                    break;
                case "save-batch":
                    saveBatchPath = value;
                    break;
                case "input-root":
                    inputRoot = value;
                    break;
                case "export-path":
                    exportArchivePath = value;
                    break;
                default:
                    return Error($"Unknown option '--{name}'.");
            }

            index = nextIndex;
        }

        return Success(new SpikeCommandOptions(
            command,
            scenario,
            mode,
            format,
            useHostEnvironment,
            javaPromptDecision,
            javaDownloadState,
            crashAction,
            saveBatchPath,
            workspaceRoot,
            inputRoot,
            exportArchivePath));
    }

    public static string GetUsageText()
    {
        return """
PCL.Frontend.Spike

Usage:
  app [--scenario modern-fabric|legacy-forge] [--host-env true|false] [--input-root path]
  startup [--mode plan|run|execute] [--format json|text] [--host-env true|false] [--workspace path] [--input-root path]
  shell [--mode plan|run|execute] [--format json|text] [--host-env true|false] [--workspace path] [--input-root path]
  launch [modern-fabric|legacy-forge] [--mode plan|run|execute] [--format json|text] [--host-env true|false] [--java-prompt download|abort] [--java-download-state finished|failed|aborted] [--save-batch path] [--workspace path] [--input-root path]
  crash [--mode plan|run|execute] [--format json|text] [--host-env true|false] [--crash-action close|view-log|open-settings|export] [--workspace path] [--input-root path] [--export-path path]
  all [modern-fabric|legacy-forge] [--mode plan|run|execute] [--format json|text] [--host-env true|false] [--java-prompt download|abort] [--java-download-state finished|failed|aborted] [--save-batch path] [--crash-action close|view-log|open-settings|export] [--workspace path] [--input-root path] [--export-path path]
  help

Defaults:
  command: all
  scenario: modern-fabric
  mode: plan
  format: json
  host env: false
  java prompt: download
  java download state: finished
  crash action: export
""";
    }

    private static SpikeParseResult Success(SpikeCommandOptions options) =>
        new(options, ShowHelp: false, ErrorMessage: null);

    private static SpikeParseResult Error(string message) =>
        new(null, ShowHelp: false, ErrorMessage: message);

    private static bool IsHelpToken(string token) =>
        token is "help" or "--help" or "-h";

    private static bool TryParseCommand(string token, out SpikeCommandKind command)
    {
        switch (token.Trim().ToLowerInvariant())
        {
            case "startup":
                command = SpikeCommandKind.Startup;
                return true;
            case "launch":
                command = SpikeCommandKind.Launch;
                return true;
            case "crash":
                command = SpikeCommandKind.Crash;
                return true;
            case "all":
                command = SpikeCommandKind.All;
                return true;
            case "shell":
                command = SpikeCommandKind.Shell;
                return true;
            case "app":
                command = SpikeCommandKind.App;
                return true;
            default:
                command = default;
                return false;
        }
    }

    private static bool TryParseMode(string value, out SpikeOutputMode mode)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "plan":
                mode = SpikeOutputMode.Plan;
                return true;
            case "run":
                mode = SpikeOutputMode.Run;
                return true;
            case "execute":
                mode = SpikeOutputMode.Execute;
                return true;
            default:
                mode = default;
                return false;
        }
    }

    private static bool TryParseFormat(string value, out SpikeOutputFormat format)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "json":
                format = SpikeOutputFormat.Json;
                return true;
            case "text":
                format = SpikeOutputFormat.Text;
                return true;
            default:
                format = default;
                return false;
        }
    }

    private static bool TryParseBooleanFlag(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
            case "yes":
            case "on":
                result = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static bool TryParseJavaPromptDecision(string value, out MinecraftLaunchJavaPromptDecision decision)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "download":
                decision = MinecraftLaunchJavaPromptDecision.Download;
                return true;
            case "abort":
                decision = MinecraftLaunchJavaPromptDecision.Abort;
                return true;
            default:
                decision = default;
                return false;
        }
    }

    private static bool TryParseJavaDownloadState(string value, out SpikeJavaDownloadSessionState state)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "finished":
                state = SpikeJavaDownloadSessionState.Finished;
                return true;
            case "failed":
                state = SpikeJavaDownloadSessionState.Failed;
                return true;
            case "aborted":
                state = SpikeJavaDownloadSessionState.Aborted;
                return true;
            default:
                state = default;
                return false;
        }
    }

    private static bool TryParseCrashAction(string value, out MinecraftCrashOutputPromptActionKind action)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "close":
                action = MinecraftCrashOutputPromptActionKind.Close;
                return true;
            case "view-log":
                action = MinecraftCrashOutputPromptActionKind.ViewLog;
                return true;
            case "open-settings":
                action = MinecraftCrashOutputPromptActionKind.OpenInstanceSettings;
                return true;
            case "export":
                action = MinecraftCrashOutputPromptActionKind.ExportReport;
                return true;
            default:
                action = default;
                return false;
        }
    }

    private static (string Name, string? Value, int NextIndex, string? ErrorMessage) ReadOption(string[] args, int index)
    {
        var token = args[index];
        var optionText = token[2..];
        var separatorIndex = optionText.IndexOf('=');
        if (separatorIndex >= 0)
        {
            return (
                optionText[..separatorIndex].Trim().ToLowerInvariant(),
                optionText[(separatorIndex + 1)..].Trim(),
                index + 1,
                null);
        }

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return (
                optionText.Trim().ToLowerInvariant(),
                null,
                index + 1,
                $"Option '{token}' requires a value.");
        }

        return (
            optionText.Trim().ToLowerInvariant(),
            args[index + 1],
            index + 2,
            null);
    }
}
