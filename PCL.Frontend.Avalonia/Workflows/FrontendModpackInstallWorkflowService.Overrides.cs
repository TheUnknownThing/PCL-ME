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
    private static void ApplyOverrides(
        FrontendModpackPackage package,
        string extractRoot,
        string targetDirectory)
    {
        var normalizedExtractRoot = NormalizeDirectoryRoot(extractRoot);
        foreach (var source in package.OverrideSources)
        {
            if (string.IsNullOrWhiteSpace(source.RelativePath))
            {
                continue;
            }

            var normalizedRelativePath = source.RelativePath is "." or "./"
                ? string.Empty
                : NormalizeRelativePath(source.RelativePath);
            var sourcePath = string.IsNullOrWhiteSpace(normalizedRelativePath)
                ? normalizedExtractRoot
                : Path.GetFullPath(Path.Combine(normalizedExtractRoot, normalizedRelativePath));
            if (!string.IsNullOrWhiteSpace(normalizedRelativePath) && !IsPathWithinDirectory(sourcePath, normalizedExtractRoot))
            {
                continue;
            }

            if (!Directory.Exists(sourcePath))
            {
                continue;
            }

            CopyDirectoryContents(sourcePath, targetDirectory);
        }
    }
    private static void CopyDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, true);
        }
    }
}
