using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft;


public static partial class MinecraftCrashAnalysisService
{
    private sealed record AnalyzedFile(
        string Path,
        string[] Lines);

    private enum AnalyzeFileType
    {
        HsErr,
        MinecraftLog,
        ExtraLogFile,
        ExtraReportFile,
        CrashReport
    }

    private enum CrashReason
    {
        ExtractedModJar,
        MissingMixinBootstrap,
        OutOfMemory,
        UsingJdk,
        UnsupportedOpenGl,
        UsingOpenJ9,
        JavaTooHigh,
        IncompatibleJava,
        SpecialCharsInModName,
        PixelFormatUnsupported,
        VeryShortOutput,
        IntelDriverAccessViolation,
        AmdDriverAccessViolation,
        NvidiaDriverAccessViolation,
        ManualDebugCrash,
        OpenGl1282FromShadersOrResources,
        FileValidationFailed,
        ConfirmedModCrash,
        SuspectedModCrash,
        ModConfigCrash,
        ModMixinFailure,
        ModLoaderFailure,
        ModInitializationFailure,
        StackKeyword,
        StackModName,
        OptiFineWorldLoadFailure,
        ProblematicBlock,
        ProblematicEntity,
        TooLargeResourcePackOrWeakGpu,
        NoAnalysisFiles,
        Java32BitInsufficientMemory,
        DuplicateMods,
        IncompatibleMods,
        OptiFineForgeIncompatible,
        FabricError,
        FabricSolution,
        ForgeError,
        LegacyForgeHighJavaIncompatible,
        MultipleForgeInJson,
        ExceededIdLimit,
        NightConfigBug,
        ShadersModWithOptiFine,
        IncompleteForgeInstall,
        ModNeedsJava11,
        MissingDependencyOrWrongMcVersion
    }
}

public sealed record MinecraftCrashAnalysisRequest(
    IReadOnlyList<string> SourceFilePaths,
    string? CurrentLauncherLogFilePath);


public sealed record MinecraftCrashAnalysisResult(
    string ResultText,
    bool HasKnownReason,
    bool HasDirectFile,
    string? DirectFilePath,
    bool HasModLoaderVersionMismatch = false);
