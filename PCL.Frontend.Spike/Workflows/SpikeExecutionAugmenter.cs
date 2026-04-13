using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class SpikeExecutionAugmenter
{
    public static StartupSpikeExecution AddInputArtifact(StartupSpikeExecution execution, SpikeExecutionArtifact artifact)
    {
        return execution with
        {
            Execution = AddArtifact(execution.Execution, artifact),
            Transcript = AddArtifactSection(execution.Transcript, artifact)
        };
    }

    public static ShellSpikeExecution AddInputArtifact(ShellSpikeExecution execution, SpikeExecutionArtifact artifact)
    {
        return execution with
        {
            Execution = AddArtifact(execution.Execution, artifact),
            Transcript = AddArtifactSection(execution.Transcript, artifact)
        };
    }

    public static LaunchSpikeExecution AddInputArtifact(LaunchSpikeExecution execution, SpikeExecutionArtifact artifact)
    {
        return execution with
        {
            Execution = AddArtifact(execution.Execution, artifact),
            Transcript = AddArtifactSection(execution.Transcript, artifact)
        };
    }

    public static CrashSpikeExecution AddInputArtifact(CrashSpikeExecution execution, SpikeExecutionArtifact artifact)
    {
        return execution with
        {
            Execution = AddArtifact(execution.Execution, artifact),
            Transcript = AddArtifactSection(execution.Transcript, artifact)
        };
    }

    private static SpikeExecutionSummary AddArtifact(SpikeExecutionSummary summary, SpikeExecutionArtifact artifact)
    {
        return summary with
        {
            WrittenFiles = [..summary.WrittenFiles, artifact.Path],
            Artifacts = [..summary.Artifacts, artifact]
        };
    }

    private static SpikeTranscript AddArtifactSection(SpikeTranscript transcript, SpikeExecutionArtifact artifact)
    {
        return transcript with
        {
            Sections =
            [
                ..transcript.Sections,
                new SpikeTranscriptSection("Input Snapshot", [$"{artifact.Label}: {artifact.Path}"])
            ]
        };
    }
}
