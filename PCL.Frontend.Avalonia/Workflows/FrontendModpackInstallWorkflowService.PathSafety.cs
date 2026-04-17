using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendModpackInstallWorkflowService
{
    private static string BuildValidatedTargetPath(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var expectedRoot = NormalizeDirectoryRoot(Path.Combine(Path.GetTempPath(), "placeholder"));
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.file_path_outside_instance", ("relative_path", relativePath)));
        }

        var combined = Path.GetFullPath(Path.Combine(expectedRoot, normalized));
        if (!IsPathWithinDirectory(combined, expectedRoot))
        {
            throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.file_path_outside_instance", ("relative_path", relativePath)));
        }

        return Path.GetRelativePath(expectedRoot, combined);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }
    private static string NormalizeDirectoryRoot(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsPathWithinDirectory(string path, string directory)
    {
        var normalizedDirectory = EnsureTrailingSeparator(NormalizeDirectoryRoot(directory));
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedDirectory, GetPathComparison());
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
