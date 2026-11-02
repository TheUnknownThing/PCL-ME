using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;
using fNbt;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstanceCompositionService
{
    private static List<FrontendInstanceJavaOption> BuildJavaOptions(
        IReadOnlyList<FrontendJavaEntry> javaEntries,
        string launcherDirectory,
        FrontendJavaPreference preference,
        II18nService? i18n)
    {
        var options = new List<FrontendInstanceJavaOption>
        {
            new("global", Text(i18n, "instance.settings.options.follow_global", "Follow global setting")),
            new("auto", Text(i18n, "instance.settings.java_options.auto_select", "Automatically choose a suitable Java"))
        };

        if (preference.Kind == FrontendJavaPreferenceKind.RelativePath && !string.IsNullOrWhiteSpace(preference.Value))
        {
            options.Add(new FrontendInstanceJavaOption(
                $"relative:{preference.Value}",
                Text(i18n, "instance.settings.java_options.launcher_relative_selected", "Java under launcher directory | {relative_path}", ("relative_path", preference.Value))));
        }
        else
        {
            options.Add(new FrontendInstanceJavaOption("relative", Text(i18n, "instance.settings.java_options.launcher_relative", "Choose Java under launcher directory")));
        }

        foreach (var entry in javaEntries)
        {
            options.Add(new FrontendInstanceJavaOption($"existing:{entry.ExecutablePath}", entry.DisplayName));
        }

        return options;
    }

    private static string ResolveSelectedJavaKey(FrontendJavaPreference preference, string launcherDirectory)
    {
        return preference.Kind switch
        {
            FrontendJavaPreferenceKind.Global => "global",
            FrontendJavaPreferenceKind.Auto => "auto",
            FrontendJavaPreferenceKind.RelativePath => $"relative:{preference.Value}",
            FrontendJavaPreferenceKind.Existing => $"existing:{preference.Value}",
            _ => "global"
        };
    }

    private static FrontendJavaEntry? ResolveSelectedJava(
        FrontendJavaPreference preference,
        IReadOnlyList<FrontendJavaEntry> javaEntries,
        string launcherDirectory)
    {
        return preference.Kind switch
        {
            FrontendJavaPreferenceKind.Existing => javaEntries.FirstOrDefault(entry =>
                string.Equals(entry.ExecutablePath, preference.Value, StringComparison.OrdinalIgnoreCase)),
            FrontendJavaPreferenceKind.RelativePath => javaEntries.FirstOrDefault(entry =>
                string.Equals(entry.ExecutablePath, Path.GetFullPath(Path.Combine(launcherDirectory, preference.Value ?? string.Empty)), StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    private static FrontendJavaPreference ReadJavaPreference(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || IsGlobalJavaPreferenceValue(rawValue))
        {
            return new FrontendJavaPreference(FrontendJavaPreferenceKind.Global, null);
        }

        try
        {
            using var document = JsonDocument.Parse(rawValue);
            var kind = GetString(document.RootElement, "kind")?.ToLowerInvariant();
            return kind switch
            {
                "auto" => new FrontendJavaPreference(FrontendJavaPreferenceKind.Auto, null),
                "exist" => new FrontendJavaPreference(
                    FrontendJavaPreferenceKind.Existing,
                    GetString(document.RootElement, "JavaExePath")),
                "relative" => new FrontendJavaPreference(
                    FrontendJavaPreferenceKind.RelativePath,
                    GetString(document.RootElement, "RelativePath")),
                _ => new FrontendJavaPreference(FrontendJavaPreferenceKind.Global, null)
            };
        }
        catch
        {
            return new FrontendJavaPreference(FrontendJavaPreferenceKind.Global, null);
        }
    }

    private static List<FrontendJavaEntry> ParseJavaEntries(string rawJson)
    {
        return FrontendJavaInventoryService.ParseStoredJavaRuntimes(rawJson)
            .Select(runtime => new FrontendJavaEntry(
                runtime.ExecutablePath,
                runtime.DisplayName,
                runtime.Is64Bit))
            .ToList();
    }

    private static bool IsGlobalJavaPreferenceValue(string rawValue)
    {
        return string.Equals(rawValue, LegacyGlobalJavaPreferenceLabel, StringComparison.Ordinal)
               || string.Equals(rawValue, "Follow global setting", StringComparison.Ordinal)
               || string.Equals(rawValue, "global", StringComparison.OrdinalIgnoreCase);
    }

}
