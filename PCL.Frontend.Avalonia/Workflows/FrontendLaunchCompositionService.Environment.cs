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
    private static IReadOnlyDictionary<string, string> BuildLaunchEnvironmentOverrides(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var environmentVariables = ParseLaunchEnvironmentVariables(
            ReadValue(localConfig, "LaunchAdvanceEnvironmentVariables", string.Empty));
        if (instanceConfig is not null)
        {
            MergeEnvironmentVariables(
                environmentVariables,
                ParseLaunchEnvironmentVariables(ReadValue(instanceConfig, "VersionAdvanceEnvironmentVariables", string.Empty)));
        }

        if (ShouldForceX11OnWayland(localConfig, instanceConfig))
        {
            environmentVariables["XDG_SESSION_TYPE"] = "x11";
            environmentVariables["WAYLAND_DISPLAY"] = string.Empty;
        }

        return environmentVariables;
    }

    private static Dictionary<string, string> ParseLaunchEnvironmentVariables(string rawValue)
    {
        var environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return environmentVariables;
        }

        foreach (var rawLine in rawValue.Replace("\r", string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line[7..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            var key = separatorIndex >= 0
                ? line[..separatorIndex].Trim()
                : line;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = separatorIndex >= 0
                ? line[(separatorIndex + 1)..]
                : string.Empty;
            environmentVariables[key] = value;
        }

        return environmentVariables;
    }

    private static void MergeEnvironmentVariables(
        IDictionary<string, string> target,
        IReadOnlyDictionary<string, string> overrides)
    {
        foreach (var (key, value) in overrides)
        {
            target[key] = value;
        }
    }

    private static bool ShouldForceX11OnWayland(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var isEnabled = ResolveForceX11OnWayland(localConfig, instanceConfig);
        if (!isEnabled)
        {
            return false;
        }

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        return string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(display);
    }

    private static bool ResolveForceX11OnWayland(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var globalEnabled = ReadValue(localConfig, "LaunchAdvanceForceX11OnWayland", false);
        if (instanceConfig is null)
        {
            return globalEnabled;
        }

        var instanceMode = Math.Clamp(ReadValue(instanceConfig, "VersionAdvanceForceX11OnWayland", 0), 0, 2);
        return instanceMode switch
        {
            1 => true,
            2 => false,
            _ => globalEnabled
        };
    }

}
