using System;
using System.Collections.Generic;
using PCL.Core.App.I18n;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchJavaPromptService
{
    public static MinecraftLaunchJavaPrompt BuildMissingJavaPrompt(MinecraftLaunchJavaPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MinimumVersion >= new Version(1, 9))
        {
            var downloadMajorVersion = GetDisplayJavaMajorVersion(request.MinimumVersion).ToString();
            return CreateAutomaticDownloadPrompt(
                $"Java {downloadMajorVersion}",
                request.RecommendedComponent ?? downloadMajorVersion);
        }

        if (request.MaximumVersion < new Version(1, 8))
        {
            return new MinecraftLaunchJavaPrompt(
                I18nText.Plain(
                    request.HasForge
                        ? "launch.prompts.java_missing.manual_java7_with_legacy_fixer.message"
                        : "launch.prompts.java_missing.manual_java7.message"),
                I18nText.Plain("launch.prompts.java_missing.title"),
                [
                    new MinecraftLaunchJavaPromptOption(
                        I18nText.Plain("launch.prompts.java_missing.actions.confirm"),
                        MinecraftLaunchJavaPromptDecision.Abort)
                ]);
        }

        if (request.MinimumVersion > new Version(1, 8, 0, 140) &&
            request.MaximumVersion < new Version(1, 8, 0, 321))
        {
            return new MinecraftLaunchJavaPrompt(
                I18nText.Plain("launch.prompts.java_missing.manual_java8u141_to_320.message"),
                I18nText.Plain("launch.prompts.java_missing.title"),
                [
                    new MinecraftLaunchJavaPromptOption(
                        I18nText.Plain("launch.prompts.java_missing.actions.confirm"),
                        MinecraftLaunchJavaPromptDecision.Abort)
                ]);
        }

        if (request.MinimumVersion > new Version(1, 8, 0, 140))
        {
            return new MinecraftLaunchJavaPrompt(
                I18nText.Plain("launch.prompts.java_missing.manual_java8u141_plus.message"),
                I18nText.Plain("launch.prompts.java_missing.title"),
                [
                    new MinecraftLaunchJavaPromptOption(
                        I18nText.Plain("launch.prompts.java_missing.actions.confirm"),
                        MinecraftLaunchJavaPromptDecision.Abort)
                ]);
        }

        return CreateAutomaticDownloadPrompt("Java 8", "8");
    }

    private static MinecraftLaunchJavaPrompt CreateAutomaticDownloadPrompt(string versionDescription, string downloadTarget)
    {
        return new MinecraftLaunchJavaPrompt(
            I18nText.WithArgs(
                "launch.prompts.java_missing.auto_download.message",
                I18nTextArgument.String("version", versionDescription)),
            I18nText.Plain("launch.prompts.java_missing.auto_download.title"),
            [
                new MinecraftLaunchJavaPromptOption(
                    I18nText.Plain("launch.prompts.java_missing.auto_download.actions.download"),
                    MinecraftLaunchJavaPromptDecision.Download),
                new MinecraftLaunchJavaPromptOption(
                    I18nText.Plain("launch.prompts.java_missing.auto_download.actions.cancel"),
                    MinecraftLaunchJavaPromptDecision.Abort)
            ],
            downloadTarget);
    }

    private static int GetDisplayJavaMajorVersion(Version version)
    {
        return version.Major <= 1 ? version.Minor : version.Major;
    }
}

public sealed record MinecraftLaunchJavaPromptRequest(
    Version MinimumVersion,
    Version MaximumVersion,
    bool HasForge,
    string? RecommendedComponent);

public sealed record MinecraftLaunchJavaPrompt(
    I18nText Message,
    I18nText Title,
    IReadOnlyList<MinecraftLaunchJavaPromptOption> Options,
    string? DownloadTarget = null);

public sealed record MinecraftLaunchJavaPromptOption(
    I18nText Label,
    MinecraftLaunchJavaPromptDecision Decision);

public enum MinecraftLaunchJavaPromptDecision
{
    Download = 0,
    Abort = 1
}
