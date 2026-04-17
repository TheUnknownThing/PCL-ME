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
    private static readonly string LauncherRootDirectory = FrontendLauncherAssetLocator.RootDirectory;
    private static readonly string[] ScreenshotPatterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff"];
    private static readonly string[] EnabledModExtensions = [".jar", ".litemod"];
    private static readonly string[] DisabledModExtensions = [".disabled", ".old"];
    private static readonly string[] ArchiveExtensions = [".zip", ".rar", ".7z"];
    private static readonly string[] SchematicExtensions = [".litematic", ".nbt", ".schematic", ".schem"];
    private static readonly Regex TomlQuotedValueRegex = new("\"(?<value>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TomlSingleQuotedValueRegex = new("'(?<value>[^']*)'", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const int MaxEmbeddedIconBytes = 2 * 1024 * 1024;
    private const string LegacyGlobalJavaPreferenceLabel = "\u4f7f\u7528\u5168\u5c40\u8bbe\u7f6e";

    internal enum LoadMode
    {
        Lightweight = 0,
        InstallAware = 1,
        Full = 2
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }

}
