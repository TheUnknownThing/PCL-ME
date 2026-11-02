using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using PCL.Core.App;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.I18n;
using PCL.Core.Logging;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendLaunchCompositionService
{
    private static bool ResolveIsolationEnabled(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (instanceConfig is not null && instanceConfig.Exists("VersionArgumentIndieV2"))
        {
            return ReadValue(instanceConfig, "VersionArgumentIndieV2", false);
        }

        var globalMode = ReadValue(localConfig, "LaunchArgumentIndieV2", 4);
        return FrontendIsolationPolicyService.ShouldIsolateByGlobalMode(
            globalMode,
            IsModable(manifestSummary),
            FrontendIsolationPolicyService.IsNonReleaseVersionType(manifestSummary.VersionType));
    }

    private static bool IsModable(FrontendVersionManifestSummary manifestSummary)
    {
        return manifestSummary.HasForgeLike
               || manifestSummary.HasCleanroom
               || manifestSummary.HasFabricLike
               || manifestSummary.HasLiteLoader
               || manifestSummary.HasLabyMod
               || manifestSummary.HasOptiFine;
    }

    private static MinecraftLaunchLoginRequirement ResolveLoginRequirement(YamlFileProvider? instanceConfig)
    {
        if (instanceConfig is null)
        {
            return MinecraftLaunchLoginRequirement.None;
        }

        return (MinecraftLaunchLoginRequirement)Math.Clamp(
            ReadValue(instanceConfig, "VersionServerLoginRequire", 0),
            0,
            3);
    }

    private static string ResolveVersionType(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        var instanceCustomInfo = instanceConfig is null
            ? null
            : FirstNonEmpty(
                NullIfWhiteSpace(ReadValue(instanceConfig, "VersionArgumentInfo", string.Empty)),
                NullIfWhiteSpace(ReadValue(instanceConfig, "CustomInfo", string.Empty)));
        if (!string.IsNullOrWhiteSpace(instanceCustomInfo))
        {
            return instanceCustomInfo;
        }

        var globalCustomInfo = NullIfWhiteSpace(ReadValue(localConfig, "LaunchArgumentInfo", "PCLME"));
        if (!string.IsNullOrWhiteSpace(globalCustomInfo))
        {
            return globalCustomInfo;
        }

        return manifestSummary.VersionType ?? "PCL-ME";
    }

    private static string BuildIgnoredJavaCompatibilityWarning(
        FrontendStoredJavaRuntime runtime,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var runtimeLabel = string.IsNullOrWhiteSpace(runtime.DisplayName)
            ? Path.GetFileName(Path.GetDirectoryName(runtime.ExecutablePath)) ?? runtime.ExecutablePath
            : runtime.DisplayName;
        return $"Java compatibility checks were skipped. Current runtime: {runtimeLabel}. Recommended range: {javaWorkflow.MinimumVersion} - {javaWorkflow.MaximumVersion}";
    }

    private static MinecraftLaunchPrompt BuildJavaCompatibilityPrompt(
        FrontendStoredJavaRuntime runtime,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var runtimeLabel = string.IsNullOrWhiteSpace(runtime.DisplayName)
            ? Path.GetFileName(Path.GetDirectoryName(runtime.ExecutablePath)) ?? runtime.ExecutablePath
            : runtime.DisplayName;

        return new MinecraftLaunchPrompt(
            I18nText.WithArgs(
                "launch.prompts.java_compatibility.message",
                I18nTextArgument.String("runtime_label", runtimeLabel),
                I18nTextArgument.String("minimum_version", javaWorkflow.MinimumVersion.ToString()),
                I18nTextArgument.String("maximum_version", javaWorkflow.MaximumVersion.ToString())),
            I18nText.Plain("launch.prompts.java_compatibility.title"),
            [
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.java_compatibility.actions.force_current"),
                    [
                        new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.IgnoreJavaCompatibilityOnce),
                        new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)
                    ]),
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.java_compatibility.actions.use_compatible"),
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)]),
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.java_compatibility.actions.abort"),
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Abort)])
            ],
            IsWarning: true);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Version? TryParseVanillaVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(rawValue.Trim(), @"\d+(?:\.\d+){1,3}");
        if (!match.Success || !Version.TryParse(match.Value, out var version))
        {
            return null;
        }

        return version.Major > 99 ? null : version;
    }

    private static DateTime? TryParseReleaseTime(string? rawValue)
    {
        return DateTime.TryParse(rawValue, out var releaseTime) ? releaseTime : null;
    }

    private static bool IsAscii(string value)
    {
        return value.All(character => character <= sbyte.MaxValue);
    }

}
