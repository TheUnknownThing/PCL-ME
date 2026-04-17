using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.I18n;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.Processes;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendShellActionService
{
    public FrontendInstanceRepairResult RepairInstance(
        FrontendInstanceRepairRequest request,
        Action<FrontendInstanceRepairProgressSnapshot>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return FrontendInstanceRepairService.Repair(
            request,
            onProgress,
            GetDownloadProvider(),
            GetDownloadTransferOptions(),
            cancellationToken);
    }

    public bool TryOpenExternalTarget(string target, out string? error)
    {
        return PlatformAdapter.TryOpenExternalTarget(target, out error);
    }

    public bool TryRevealExternalTarget(string target, out string? error)
    {
        return PlatformAdapter.TryRevealExternalTarget(target, out error);
    }

    public bool TryStartDetachedScript(string scriptPath, out string? error)
    {
        return PlatformAdapter.TryStartDetachedScript(scriptPath, out error);
    }

    public string CreateLauncherShortcut(string displayName)
    {
        var desktopDirectory = PlatformAdapter.TryGetDesktopDirectory()
            ?? throw new InvalidOperationException("The current system did not provide a desktop directory.");
        return CreateLauncherShortcutAt(desktopDirectory, displayName);
    }

    public string CreateLauncherShortcutAt(string targetDirectory, string displayName)
    {
        var executablePath = Environment.ProcessPath ?? Path.Combine(RuntimePaths.ExecutableDirectory, "PCL.Frontend.Avalonia");
        return PlatformAdapter.CreateLauncherShortcut(targetDirectory, executablePath, displayName).ShortcutPath;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary frontend artifacts.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup for stale launch scripts.
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(left, right, comparison);
    }
}
