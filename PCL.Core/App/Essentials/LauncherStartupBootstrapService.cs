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
            Path.Combine(executableDirectory, "PCL", "Pictures"),
            Path.Combine(executableDirectory, "PCL", "Musics"),
            Path.Combine(tempDirectory, "Cache"),
            Path.Combine(tempDirectory, "Download"),
            appDataDirectory
        };

        var legacyLogs = Enumerable.Range(1, 5)
            .Select(i => Path.Combine(executableDirectory, "PCL", $"Log-CE{i}.log"))
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
}
