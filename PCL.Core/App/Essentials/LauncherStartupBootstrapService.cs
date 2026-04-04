using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupBootstrapService
{
    private static readonly string[] _configKeysToLoad =
    [
        "SystemDebugMode",
        "SystemDebugAnim",
        "SystemHttpProxy",
        "SystemHttpProxyCustomUsername",
        "SystemHttpProxyType",
        "ToolDownloadThread",
        "ToolDownloadSpeed",
        "UiFont"
    ];

    public static LauncherStartupBootstrapResult Build(LauncherStartupBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executableDirectory = request.ExecutableDirectory ?? string.Empty;
        var tempDirectory = request.TempDirectory ?? string.Empty;
        var appDataDirectory = request.AppDataDirectory ?? string.Empty;

        var directoriesToCreate = new List<string>
        {
            CombinePath(executableDirectory, "PCL", "Pictures"),
            CombinePath(executableDirectory, "PCL", "Musics"),
            CombinePath(tempDirectory, "Cache"),
            CombinePath(tempDirectory, "Download"),
            appDataDirectory
        };

        var legacyLogs = Enumerable.Range(1, 5)
            .Select(i => CombinePath(executableDirectory, "PCL", $"Log-CE{i}.log"))
            .ToArray();

        var warningMessage = request.EnvironmentWarnings is { Count: > 0 }
            ? "PCL CE 在启动时检测到环境问题：" + Environment.NewLine + Environment.NewLine +
              string.Join(Environment.NewLine, request.EnvironmentWarnings) + Environment.NewLine + Environment.NewLine +
              "不解决这些问题可能会导致部分功能无法正常工作……"
            : null;

        return new LauncherStartupBootstrapResult(
            directoriesToCreate,
            _configKeysToLoad,
            legacyLogs,
            request.IsBetaVersion ? UpdateChannel.Beta : UpdateChannel.Release,
            warningMessage);
    }

    private static string CombinePath(string basePath, params string[] segments)
    {
        if (string.IsNullOrEmpty(basePath))
        {
            return Path.Combine(segments);
        }

        var separator = GetPreferredSeparator(basePath);
        var normalizedBase = NormalizeSeparators(basePath, separator).TrimEnd(separator);
        var normalizedSegments = segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Select(segment => NormalizeSeparators(segment, separator).Trim(separator))
            .ToArray();

        return normalizedSegments.Length == 0
            ? normalizedBase
            : normalizedBase + separator + string.Join(separator, normalizedSegments);
    }

    private static char GetPreferredSeparator(string path)
    {
        return path.Contains('\\') ? '\\' : Path.DirectorySeparatorChar;
    }

    private static string NormalizeSeparators(string path, char separator)
    {
        return path.Replace('\\', separator).Replace('/', separator);
    }
}
