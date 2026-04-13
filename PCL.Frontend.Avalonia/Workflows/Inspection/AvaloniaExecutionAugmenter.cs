using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class AvaloniaExecutionAugmenter
{
    public static StartupAvaloniaExecution AddInputArtifact(StartupAvaloniaExecution execution, AvaloniaExecutionArtifact artifact)
    {
        return execution with
        {
            Execution = AddArtifact(execution.Execution, artifact),
            Transcript = AddArtifactSection(execution.Transcript, artifact)
        };
    }

    public static ShellAvaloniaExecution AddInputArtifact(ShellAvaloniaExecution execution, AvaloniaExecutionArtifact artifact)
    {
        return execution with
        {
            Execution = AddArtifact(execution.Execution, artifact),
            Transcript = AddArtifactSection(execution.Transcript, artifact)
        };
    }

    public static LaunchAvaloniaExecution AddInputArtifact(LaunchAvaloniaExecution execution, AvaloniaExecutionArtifact artifact)
    {
        return execution with
        {
            Execution = AddArtifact(execution.Execution, artifact),
            Transcript = AddArtifactSection(execution.Transcript, artifact)
        };
    }

    public static CrashAvaloniaExecution AddInputArtifact(CrashAvaloniaExecution execution, AvaloniaExecutionArtifact artifact)
    {
        return execution with
        {
            Execution = AddArtifact(execution.Execution, artifact),
            Transcript = AddArtifactSection(execution.Transcript, artifact)
        };
    }

    private static AvaloniaExecutionSummary AddArtifact(AvaloniaExecutionSummary summary, AvaloniaExecutionArtifact artifact)
    {
        return summary with
        {
            WrittenFiles = [..summary.WrittenFiles, artifact.Path],
            Artifacts = [..summary.Artifacts, artifact]
        };
    }

    private static AvaloniaTranscript AddArtifactSection(AvaloniaTranscript transcript, AvaloniaExecutionArtifact artifact)
    {
        return transcript with
        {
            Sections =
            [
                ..transcript.Sections,
                new AvaloniaTranscriptSection("Input Snapshot", [$"{artifact.Label}: {artifact.Path}"])
            ]
        };
    }
}
