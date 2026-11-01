using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Java.Runtime;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java.Parser;

public sealed class CommandJavaParser(IJavaRuntimeEnvironment runtime, ICommandRunner commandRunner) : IJavaParser
{
    private static readonly Regex PropertyRegex = new(@"^\s*(?<key>[A-Za-z0-9._-]+)\s*=\s*(?<value>.+?)\s*$", RegexOptions.Compiled);

    public JavaInstallation? Parse(string javaExePath)
    {
        try
        {
            if (!File.Exists(javaExePath)) return null;

            _TryEnsureExecutablePermission(javaExePath, runtime.IsWindows);
            var command = commandRunner.Run(javaExePath, "-XshowSettings:properties -version");
            if (command.ExitCode != 0 && string.IsNullOrWhiteSpace(command.StandardError) && string.IsNullOrWhiteSpace(command.StandardOutput))
                return null;

            var properties = _ParseProperties(command.StandardOutput + Environment.NewLine + command.StandardError);
            var version = _ParseVersion(_GetValue(properties, "java.version"));
            if (version == null) return null;

            var vendor = _GetValue(properties, "java.vendor");
            var architectureText = _GetValue(properties, "os.arch");
            var executableName = Path.GetFileName(javaExePath);
            var javaFolder = Path.GetDirectoryName(javaExePath)!;
            var compilerExecutableName = Path.GetExtension(executableName).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                ? "javac.exe"
                : runtime.JavacExecutableName;
            var windowedExecutableName = runtime.JavaWindowExecutableName;
            if (windowedExecutableName != null && !File.Exists(Path.Combine(javaFolder, windowedExecutableName)))
                windowedExecutableName = null;

            return new JavaInstallation(
                javaFolder,
                version,
                JavaBrandDetector.Detect(vendor),
                _MapArchitecture(architectureText),
                _Is64BitArchitecture(architectureText),
                !File.Exists(Path.Combine(javaFolder, compilerExecutableName)),
                executableName,
                windowedExecutableName,
                compilerExecutableName
            );
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", $"An error occurred while parsing {javaExePath}");
            return null;
        }
    }

    private static void _TryEnsureExecutablePermission(string javaExePath, bool isWindows)
    {
        if (isWindows || OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(javaExePath) || !File.Exists(javaExePath))
        {
            return;
        }

        try
        {
            var currentMode = File.GetUnixFileMode(javaExePath);
            var executableBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((currentMode & executableBits) == executableBits)
            {
                return;
            }

            File.SetUnixFileMode(javaExePath, currentMode | executableBits);
        }
        catch
        {
            // Best effort for portable or freshly-downloaded runtimes on Unix-like systems.
        }
    }

    private static Dictionary<string, string> _ParseProperties(string output)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = PropertyRegex.Match(rawLine);
            if (!match.Success) continue;
            results[match.Groups["key"].Value] = match.Groups["value"].Value.Trim();
        }
        return results;
    }

    private static string? _GetValue(Dictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out var value) ? value.Trim('"') : null;

    private static Version? _ParseVersion(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion)) return null;
        var matches = Regex.Matches(rawVersion, @"\d+");
        if (matches.Count == 0) return null;

        var parts = new int[Math.Min(4, matches.Count)];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = int.Parse(matches[i].Value);
        }

        return parts.Length switch
        {
            1 => new Version(parts[0], 0),
            2 => new Version(parts[0], parts[1]),
            3 => new Version(parts[0], parts[1], parts[2]),
            _ => new Version(parts[0], parts[1], parts[2], parts[3])
        };
    }

    private static MachineType _MapArchitecture(string? architectureText)
    {
        return architectureText?.ToLowerInvariant() switch
        {
            "x86_64" or "amd64" => MachineType.AMD64,
            "aarch64" or "arm64" => MachineType.ARM64,
            "x86" or "i386" or "i486" or "i586" or "i686" => MachineType.I386,
            "arm" => MachineType.ARM,
            _ => MachineType.Unknown
        };
    }

    private static bool _Is64BitArchitecture(string? architectureText)
    {
        return architectureText?.ToLowerInvariant() switch
        {
            "x86_64" or "amd64" or "aarch64" or "arm64" => true,
            _ => false
        };
    }
}
