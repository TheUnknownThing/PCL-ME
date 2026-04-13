using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Icons;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly Dictionary<string, Bitmap?> LauncherBitmapCache = new(StringComparer.Ordinal);
    private static readonly Lock LauncherBitmapCacheLock = new();

    private static SurfaceFactViewModel CreateSurfaceFact(LauncherFrontendPageFact fact, int index)
    {
        var palette = GetSurfacePalette(index);
        return new SurfaceFactViewModel(
            fact.Label,
            fact.Value,
            palette.Accent,
            palette.Background,
            palette.Border,
            palette.Foreground);
    }

    private static SurfaceSectionViewModel CreateSurfaceSection(LauncherFrontendPageSection section, int index)
    {
        var palette = GetSurfacePalette(index);
        return new SurfaceSectionViewModel(
            section.Eyebrow,
            section.Title,
            section.Lines.Select(line => new SurfaceLineViewModel(line, palette.Accent, palette.Foreground)).ToArray(),
            palette.Accent,
            palette.Background,
            palette.Border,
            palette.Foreground);
    }

    private static SurfacePalette GetSurfacePalette(int index)
    {
        return (index % 4) switch
        {
            0 => new SurfacePalette(
                Brush.Parse("#FFF3EA"),
                Brush.Parse("#F1D2BD"),
                Brush.Parse("#B05A2C"),
                Brush.Parse("#3D2C21")),
            1 => new SurfacePalette(
                Brush.Parse("#EAF7F4"),
                Brush.Parse("#C8E6DF"),
                Brush.Parse("#1B7A6F"),
                Brush.Parse("#234744")),
            2 => new SurfacePalette(
                Brush.Parse("#EEF3FE"),
                Brush.Parse("#D3DDF8"),
                Brush.Parse("#3E67B0"),
                Brush.Parse("#223855")),
            _ => new SurfacePalette(
                Brush.Parse("#F6F0FD"),
                Brush.Parse("#E2D6F6"),
                Brush.Parse("#6D4AA4"),
                Brush.Parse("#352645"))
        };
    }

    private static MinecraftLaunchPrecheckResult BuildLaunchPrecheckResult(
        string scenario,
        bool isNonAsciiGamePathWarningDisabled)
    {
        var requiresAuth = string.Equals(scenario, "legacy-forge", StringComparison.OrdinalIgnoreCase);
        return MinecraftLaunchPrecheckService.Evaluate(new MinecraftLaunchPrecheckRequest(
            InstanceName: requiresAuth ? "Legacy Forge Demo" : "Modern Fabric Demo",
            InstancePathIndie: "/Users/demo/.pcl/instances/示例实例",
            InstancePath: "/Users/demo/.minecraft/instances/示例实例",
            IsInstanceSelected: true,
            IsInstanceError: false,
            InstanceErrorDescription: null,
            IsUtf8CodePage: true,
            IsNonAsciiPathWarningDisabled: isNonAsciiGamePathWarningDisabled,
            IsInstancePathAscii: false,
            ProfileValidationMessage: string.Empty,
            SelectedProfileKind: requiresAuth ? MinecraftLaunchProfileKind.Auth : MinecraftLaunchProfileKind.Microsoft,
            HasLabyMod: false,
            LoginRequirement: requiresAuth ? MinecraftLaunchLoginRequirement.Auth : MinecraftLaunchLoginRequirement.Microsoft,
            RequiredAuthServer: requiresAuth ? "https://auth.example.invalid/authserver" : null,
            SelectedAuthServer: requiresAuth ? "https://auth.example.invalid/authserver" : null,
            HasMicrosoftProfile: false,
            IsRestrictedFeatureAllowed: true));
    }

    private static NavigationPalette GetNavigationPalette(bool isSelected, NavigationVisualStyle style)
    {
        return style switch
        {
            NavigationVisualStyle.TopLevel when isSelected => new NavigationPalette(
                Brushes.White,
                Brushes.White,
                Brush.Parse("#1370F3"),
                Brush.Parse("#1370F3")),
            NavigationVisualStyle.Sidebar when isSelected => new NavigationPalette(
                Brush.Parse("#EAF2FE"),
                Brush.Parse("#D5E6FD"),
                Brush.Parse("#343D4A"),
                Brush.Parse("#1370F3")),
            NavigationVisualStyle.Utility when isSelected => new NavigationPalette(
                Brush.Parse("#1370F3"),
                Brush.Parse("#1370F3"),
                Brushes.White,
                Brush.Parse("#EAF2FE")),
            NavigationVisualStyle.TopLevel => new NavigationPalette(
                Brush.Parse("#01EAF2FE"),
                Brush.Parse("#01EAF2FE"),
                Brushes.White,
                Brush.Parse("#FFFFFF")),
            NavigationVisualStyle.Sidebar => new NavigationPalette(
                Brush.Parse("#01FFFFFF"),
                Brush.Parse("#01FFFFFF"),
                Brush.Parse("#404040"),
                Brush.Parse("#D5E6FD")),
            _ => new NavigationPalette(
                Brush.Parse("#1370F3"),
                Brush.Parse("#1370F3"),
                Brushes.White,
                Brush.Parse("#EAF2FE"))
        };
    }

    private static (string IconPath, double IconScale) GetNavigationIcon(string title)
    {
        var icon = FrontendIconCatalog.GetNavigationIcon(title);
        return (icon.Data, icon.Scale);
    }

    private static string GetUtilityIcon(string id)
    {
        return FrontendIconCatalog.GetUtilityIcon(id).Data;
    }

    private static string GetSidebarSectionTitle(LauncherFrontendPageKey page, LauncherFrontendSubpageKey subpage)
    {
        return page switch
        {
            LauncherFrontendPageKey.Download when subpage == LauncherFrontendSubpageKey.DownloadInstall => string.Empty,
            LauncherFrontendPageKey.Download when subpage is LauncherFrontendSubpageKey.DownloadMod
                or LauncherFrontendSubpageKey.DownloadPack
                or LauncherFrontendSubpageKey.DownloadDataPack
                or LauncherFrontendSubpageKey.DownloadResourcePack
                or LauncherFrontendSubpageKey.DownloadShader
                or LauncherFrontendSubpageKey.DownloadWorld
                or LauncherFrontendSubpageKey.DownloadCompFavorites => "社区资源",
            LauncherFrontendPageKey.Download => "安装器",
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupLaunch
                or LauncherFrontendSubpageKey.SetupJava
                or LauncherFrontendSubpageKey.SetupGameManage => "游戏",
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupLink
                or LauncherFrontendSubpageKey.SetupGameLink => "工具",
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupUI
                or LauncherFrontendSubpageKey.SetupLauncherMisc => "启动器",
            LauncherFrontendPageKey.Setup => "关于",
            LauncherFrontendPageKey.Tools when subpage == LauncherFrontendSubpageKey.ToolsGameLink => "联机",
            LauncherFrontendPageKey.Tools => "奇妙小工具",
            LauncherFrontendPageKey.InstanceSetup when subpage is LauncherFrontendSubpageKey.VersionOverall
                or LauncherFrontendSubpageKey.VersionSetup
                or LauncherFrontendSubpageKey.VersionExport => "实例",
            LauncherFrontendPageKey.InstanceSetup when subpage is LauncherFrontendSubpageKey.VersionWorld
                or LauncherFrontendSubpageKey.VersionScreenshot => "内容",
            LauncherFrontendPageKey.InstanceSetup when subpage is LauncherFrontendSubpageKey.VersionMod
                or LauncherFrontendSubpageKey.VersionModDisabled
                or LauncherFrontendSubpageKey.VersionResourcePack
                or LauncherFrontendSubpageKey.VersionShader
                or LauncherFrontendSubpageKey.VersionSchematic => "资源",
            LauncherFrontendPageKey.InstanceSetup => "其他",
            LauncherFrontendPageKey.VersionSaves => "存档",
            _ => string.Empty
        };
    }

    private static (string IconPath, double IconScale) GetSidebarIcon(LauncherFrontendPageKey page, LauncherFrontendSubpageKey subpage, string title)
    {
        var icon = FrontendIconCatalog.GetSidebarIcon(title);
        return (icon.Data, icon.Scale);
    }

    private static (string ToolTip, string IconPath, string ActionLabel, string? Command) GetSidebarAccessory(LauncherFrontendPageKey page, LauncherFrontendSubpageKey subpage, string title)
    {
        return page switch
        {
            LauncherFrontendPageKey.Download => ("刷新", FrontendIconCatalog.Refresh.Data, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Tools => ("刷新", FrontendIconCatalog.Refresh.Data, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupJava
                or LauncherFrontendSubpageKey.SetupFeedback
                or LauncherFrontendSubpageKey.SetupUpdate => ("刷新", FrontendIconCatalog.Refresh.Data, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Setup when title is "关于" or "日志" => (string.Empty, string.Empty, string.Empty, null),
            LauncherFrontendPageKey.Setup => ("初始化设置", FrontendIconCatalog.Reset.Data, "重置", $"初始化 {title} 页面设置"),
            _ => (string.Empty, string.Empty, string.Empty, null)
        };
    }

    private static string FormatMaxRealTimeLog(double value)
    {
        var rounded = Math.Round(value);
        return rounded switch
        {
            <= 5 => $"{rounded * 10 + 50}",
            <= 13 => $"{rounded * 50 - 150}",
            <= 28 => $"{rounded * 100 - 800}",
            _ => "无限制"
        };
    }

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private static string GetLauncherAssetPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine([LauncherRootDirectory, .. segments]));
    }

    private static Bitmap? LoadLauncherBitmap(params string[] segments)
    {
        var filePath = GetLauncherAssetPath(segments);
        lock (LauncherBitmapCacheLock)
        {
            if (LauncherBitmapCache.TryGetValue(filePath, out var bitmap))
            {
                return bitmap;
            }

            bitmap = File.Exists(filePath) ? new Bitmap(filePath) : null;
            LauncherBitmapCache[filePath] = bitmap;
            return bitmap;
        }
    }
}
