using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Core.App;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchJvmArgumentService
{
    public static string BuildLegacyArguments(MinecraftLaunchLegacyJvmArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var data = new List<string>
        {
            NormalizeCustomJvmArguments(request.VariableJvmArguments, addLog4jNoLookupsWhenMissing: true),
            "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump",
            $"-Xmn{request.YoungGenerationMegabytes}m",
            $"-Xmx{request.TotalMemoryMegabytes}m",
            $"\"-Djava.library.path={request.NativesDirectory}\"",
            "-cp ${classpath}"
        };

        ApplySharedJvmEnhancements(data, request);
        data.Add(request.MainClass);
        return string.Join(" ", data.Where(entry => !string.IsNullOrWhiteSpace(entry)));
    }

    public static string BuildModernArguments(MinecraftLaunchModernJvmArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BaseArguments);

        var data = new List<string>(request.BaseArguments);
        data.Insert(0, NormalizeCustomJvmArguments(request.VariableJvmArguments, addLog4jNoLookupsWhenMissing: false));
        ApplyIpStackPreference(data, request.PreferredIpStack);
        data.Add($"-Xmn{request.YoungGenerationMegabytes}m");
        data.Add($"-Xmx{request.TotalMemoryMegabytes}m");
        if (!data.Any(entry => entry.Contains("-Dlog4j2.formatMsgNoLookups=true", StringComparison.Ordinal)))
        {
            data.Add("-Dlog4j2.formatMsgNoLookups=true");
        }

        ApplySharedJvmEnhancements(data, request);
        if (request.UseRetroWrapper)
        {
            data.Add("-Dretrowrapper.doUpdateCheck=false");
        }

        var normalized = MergeAndDeduplicateArguments(data);
        normalized.Remove("-XX:MaxDirectMemorySize=256M");
        normalized.Add(request.MainClass);
        return string.Join(" ", normalized);
    }

    private static void ApplySharedJvmEnhancements(List<string> data, MinecraftLaunchJvmArgumentRequestBase request)
    {
        if (!string.IsNullOrWhiteSpace(request.AuthlibInjectorArgument))
        {
            data.Insert(0, request.AuthlibInjectorArgument);
            if (request.JavaMajorVersion >= 6)
            {
                data.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.DebugLog4jConfigurationFilePath))
        {
            data.Insert(0, $"-Dlog4j.configurationFile=\"{request.DebugLog4jConfigurationFilePath}\"");
        }

        if (!string.IsNullOrWhiteSpace(request.RendererAgentArgument))
        {
            data.Insert(0, request.RendererAgentArgument);
        }

        if (!string.IsNullOrWhiteSpace(request.ProxyScheme) &&
            !string.IsNullOrWhiteSpace(request.ProxyHost) &&
            request.ProxyPort.HasValue)
        {
            if (request.ProxyScheme.StartsWith("socks", StringComparison.OrdinalIgnoreCase))
            {
                data.Add($"-DsocksProxyHost={request.ProxyHost}");
                data.Add($"-DsocksProxyPort={request.ProxyPort.Value}");
                if (!string.IsNullOrWhiteSpace(request.ProxyUsername))
                {
                    data.Add($"-Djava.net.socks.username={request.ProxyUsername}");
                }

                if (!string.IsNullOrEmpty(request.ProxyPassword))
                {
                    data.Add($"-Djava.net.socks.password={request.ProxyPassword}");
                }
            }
            else
            {
                data.Add($"-Dhttp.proxyHost={request.ProxyHost}");
                data.Add($"-Dhttp.proxyPort={request.ProxyPort.Value}");
                data.Add($"-Dhttps.proxyHost={request.ProxyHost}");
                data.Add($"-Dhttps.proxyPort={request.ProxyPort.Value}");
                if (!string.IsNullOrWhiteSpace(request.ProxyUsername))
                {
                    data.Add($"-Dhttp.proxyUser={request.ProxyUsername}");
                    data.Add($"-Dhttps.proxyUser={request.ProxyUsername}");
                }

                if (!string.IsNullOrEmpty(request.ProxyPassword))
                {
                    data.Add($"-Dhttp.proxyPassword={request.ProxyPassword}");
                    data.Add($"-Dhttps.proxyPassword={request.ProxyPassword}");
                }
            }
        }

        if (MinecraftLaunchJavaWrapperService.ShouldUse(
                new MinecraftLaunchJavaWrapperRequest(
                    request.UseJavaWrapper,
                    OperatingSystem.IsWindows(),
                    request.JavaMajorVersion,
                    request.JavaWrapperTempDirectory,
                    request.JavaWrapperPath)))
        {
            if (request.JavaMajorVersion >= 9)
            {
                data.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
            }

            data.Add($"-Doolloo.jlw.tmpdir=\"{request.JavaWrapperTempDirectory}\"");
            data.Add($"-jar \"{request.JavaWrapperPath}\"");
        }
    }

    private static void ApplyIpStackPreference(List<string> data, JvmPreferredIpStack preferredIpStack)
    {
        switch (preferredIpStack)
        {
            case JvmPreferredIpStack.PreferV4:
                data.Add("-Djava.net.preferIPv4Stack=true");
                data.Add("-Djava.net.preferIPv4Addresses=true");
                break;
            case JvmPreferredIpStack.PreferV6:
                data.Add("-Djava.net.preferIPv6Stack=true");
                data.Add("-Djava.net.preferIPv6Addresses=true");
                break;
        }
    }

    private static string NormalizeCustomJvmArguments(string value, bool addLog4jNoLookupsWhenMissing)
    {
        var normalized = value.Replace(" -XX:MaxDirectMemorySize=256M", string.Empty, StringComparison.Ordinal).Trim();
        if (addLog4jNoLookupsWhenMissing &&
            !normalized.Contains("-Dlog4j2.formatMsgNoLookups=true", StringComparison.Ordinal))
        {
            normalized += " -Dlog4j2.formatMsgNoLookups=true";
        }

        return normalized.Trim();
    }

    private static List<string> MergeAndDeduplicateArguments(List<string> entries)
    {
        var merged = new List<string>();
        for (var index = 0; index < entries.Count; index++)
        {
            var currentEntry = entries[index];
            if (string.IsNullOrWhiteSpace(currentEntry))
            {
                continue;
            }

            if (currentEntry.StartsWith("-", StringComparison.Ordinal))
            {
                while (index < entries.Count - 1 &&
                       !string.IsNullOrWhiteSpace(entries[index + 1]) &&
                       !entries[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    index++;
                    currentEntry += " " + entries[index];
                }
            }

            merged.Add(currentEntry.Trim().Replace("McEmu= ", "McEmu=", StringComparison.Ordinal));
        }

        return merged.Distinct(StringComparer.Ordinal).ToList();
    }
}

public abstract record MinecraftLaunchJvmArgumentRequestBase(
    int JavaMajorVersion,
    string? AuthlibInjectorArgument,
    string? DebugLog4jConfigurationFilePath,
    string? RendererAgentArgument,
    string? ProxyScheme,
    string? ProxyHost,
    int? ProxyPort,
    string? ProxyUsername,
    string? ProxyPassword,
    bool UseJavaWrapper,
    string? JavaWrapperTempDirectory,
    string? JavaWrapperPath,
    string MainClass);

public sealed record MinecraftLaunchLegacyJvmArgumentRequest(
    string VariableJvmArguments,
    int YoungGenerationMegabytes,
    int TotalMemoryMegabytes,
    string NativesDirectory,
    int JavaMajorVersion,
    string? AuthlibInjectorArgument,
    string? DebugLog4jConfigurationFilePath,
    string? RendererAgentArgument,
    string? ProxyScheme,
    string? ProxyHost,
    int? ProxyPort,
    string? ProxyUsername,
    string? ProxyPassword,
    bool UseJavaWrapper,
    string? JavaWrapperTempDirectory,
    string? JavaWrapperPath,
    string MainClass)
    : MinecraftLaunchJvmArgumentRequestBase(
        JavaMajorVersion,
        AuthlibInjectorArgument,
        DebugLog4jConfigurationFilePath,
        RendererAgentArgument,
        ProxyScheme,
        ProxyHost,
        ProxyPort,
        ProxyUsername,
        ProxyPassword,
        UseJavaWrapper,
        JavaWrapperTempDirectory,
        JavaWrapperPath,
        MainClass);

public sealed record MinecraftLaunchModernJvmArgumentRequest(
    IReadOnlyList<string> BaseArguments,
    string VariableJvmArguments,
    JvmPreferredIpStack PreferredIpStack,
    int YoungGenerationMegabytes,
    int TotalMemoryMegabytes,
    bool UseRetroWrapper,
    int JavaMajorVersion,
    string? AuthlibInjectorArgument,
    string? DebugLog4jConfigurationFilePath,
    string? RendererAgentArgument,
    string? ProxyScheme,
    string? ProxyHost,
    int? ProxyPort,
    string? ProxyUsername,
    string? ProxyPassword,
    bool UseJavaWrapper,
    string? JavaWrapperTempDirectory,
    string? JavaWrapperPath,
    string MainClass)
    : MinecraftLaunchJvmArgumentRequestBase(
        JavaMajorVersion,
        AuthlibInjectorArgument,
        DebugLog4jConfigurationFilePath,
        RendererAgentArgument,
        ProxyScheme,
        ProxyHost,
        ProxyPort,
        ProxyUsername,
        ProxyPassword,
        UseJavaWrapper,
        JavaWrapperTempDirectory,
        JavaWrapperPath,
        MainClass);
