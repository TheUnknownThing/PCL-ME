using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;

namespace PCL.Frontend.Avalonia.ViewModels;

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
                FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3"),
                FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3")),
            NavigationVisualStyle.Sidebar when isSelected => new NavigationPalette(
                FrontendThemeResourceResolver.GetBrush("ColorBrushEntrySelectedBackground", "#EAF2FE"),
                FrontendThemeResourceResolver.GetBrush("ColorBrush6", "#D5E6FD"),
                FrontendThemeResourceResolver.GetBrush("ColorBrush1", "#343D4A"),
                FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3")),
            NavigationVisualStyle.Utility when isSelected => new NavigationPalette(
                FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3"),
                FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3"),
                Brushes.White,
                FrontendThemeResourceResolver.GetBrush("ColorBrush8", "#EAF2FE")),
            NavigationVisualStyle.TopLevel => new NavigationPalette(
                FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent", "#01EAF2FE"),
                FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent", "#01EAF2FE"),
                Brushes.White,
                FrontendThemeResourceResolver.GetBrush("ColorBrushWhite", "#FFFFFF")),
            NavigationVisualStyle.Sidebar => new NavigationPalette(
                FrontendThemeResourceResolver.GetBrush("ColorBrushTransparent", "#01FFFFFF"),
                FrontendThemeResourceResolver.GetBrush("ColorBrushTransparent", "#01FFFFFF"),
                FrontendThemeResourceResolver.GetBrush("ColorBrushGray1", "#404040"),
                FrontendThemeResourceResolver.GetBrush("ColorBrush6", "#D5E6FD")),
            _ => new NavigationPalette(
                FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3"),
                FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3"),
                Brushes.White,
                FrontendThemeResourceResolver.GetBrush("ColorBrush8", "#EAF2FE"))
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
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupUI
                or LauncherFrontendSubpageKey.SetupLauncherMisc => "启动器",
            LauncherFrontendPageKey.Setup => "关于",
            LauncherFrontendPageKey.Tools => "奇妙小工具",
            LauncherFrontendPageKey.InstanceSetup when subpage is LauncherFrontendSubpageKey.VersionOverall
                or LauncherFrontendSubpageKey.VersionSetup
                or LauncherFrontendSubpageKey.VersionInstall
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
        switch ((page, subpage))
        {
            case (LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsLauncherHelp):
                return (
                "M520.6 620.3c-11.3-0.6-20.1-4-26.9-10.5-6.6-6.3-8.1-11-10.1-20.8-1.9-9.5-1.5-16.7-1-24.9l0.3-5.3c0.5-9.9 3.5-19.6 8.8-28.1 5.7-9 12.5-17.2 20.8-25.1 8.6-8 17.7-15.7 27-22.9 9.4-7.2 18.7-14.8 27.7-22.7 9-7.9 16.2-15.8 22.1-24.3 6.2-9 9.3-18.8 9.3-29.4 1.2-20.3-6.5-37.7-22.8-51.3-16-13.3-37.4-20-63.5-20-13.7 0-26.3 3.3-37.4 10-10.9 6.5-20.2 14.5-27.7 23.8-7.5 9.2-13.5 19-17.9 29.2-4.5 10.6-6.7 19.4-6.7 26.8 0 9.2-3.3 16-10.1 20.8-6.9 4.8-14.6 7.3-22.8 7.3h-1.3c-9-0.3-17-3.3-24.6-9.2-7.3-5.6-11.1-14.6-11.6-27.5-0.6-11.5 1.9-26.1 7.2-43.5 5.4-17.4 14.8-34.4 28.1-50.5 13.4-16.3 31.2-30.4 52.8-41.8 21.6-11.4 49.2-17.2 81.9-17.2 26.4 0 49.6 3.9 69.1 11.7 19.4 7.7 35.4 18.2 47.5 31.1l1.6 1.7c11.8 12.6 20.3 21.7 29.4 44.4 11.6 28.9 7.1 52.1 5.5 58.5-5.8 22.6-12.6 37.7-22.7 50.4-11.7 14.6-24.9 28.2-39.2 40.4-14 11.7-39.2 33.3-39.2 33.3-13.3 11.4-19.4 22.1-18.6 32.9V587c0.5 8.7-2.4 16.1-8.8 22.8-6.2 6.4-14.8 10-26.2 10.5zM519 766.1c-13 0-23.6-4.2-32.2-12.9-8.7-8.7-13-19-13-31.4 0-12.9 4.2-23.5 12.9-32.2 8.7-8.7 19.2-12.9 32.2-12.9 13 0 23.5 4.2 32.2 12.9s12.9 19.2 12.9 32.2c0 12.4-4.2 22.7-12.9 31.4-8.6 8.6-19.2 12.9-32.1 12.9z M515 928.3c-228.1 0-413.7-185.6-413.7-413.7S286.9 100.9 515 100.9s413.7 185.6 413.7 413.7c0.1 228.1-185.5 413.7-413.7 413.7z m0-747.4c-184 0-333.7 149.7-333.7 333.7S331 848.3 515 848.3s333.7-149.7 333.7-333.7S699 180.9 515 180.9z",
                0.97);
            case (LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsTest):
                return (
                "M511 995a128 128 0 0 1-57-13L70 791a126 126 0 0 1-70-113V311a126 126 0 0 1 15-60V248c1-2 3-5 5-8a127 127 0 0 1 49-42L454 13a128 128 0 0 1 112 0l383 190a126 126 0 0 1 72 113v360a126 126 0 0 1-70 115L568 984c-17 7-37 11-57 11z m42-470v370l360-178c14-7 23-21 23-38v-335L554 524zM85 330v347a42 42 0 0 0 23 38l360 178V523L85 330zM135 260l375 189 137-65L286 188 135 260z m245-118l363 197 150-71-365-180a42 42 0 0 0-37 0l-111 53z",
                0.9);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall):
                return (
                "M12 0l-11 6v12.131l11 5.869 11-5.869v-12.066l-11-6.065zm-1 21.2l-6.664-3.555 4.201-2.801c1.08-.719-.066-2.359-1.243-1.575l-4.294 2.862v-7.901l8 4.363v8.607zm-6.867-14.63l6.867-3.746v4.426c0 1.323 2 1.324 2 0v-4.415l6.91 3.811-7.905 4.218-7.872-4.294zm8.867 6.03l8-4.269v7.8l-4.263-2.842c-1.181-.785-2.323.855-1.245 1.574l4.172 2.781-6.664 3.556v-8.6z",
                1.0);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionSetup):
                return (
                "M1000 241c-2-5-4-11-6-17-0-6-4-12-8-17-7-9-18-15-31-15-6 0-13 1-19 5l-15 15-17 17-21 21L749 381c-14 14-38 14-52 0l-54-54c-14-14-14-38 0-52l132-132 14-14 20-20 16-16c3-5 5-11 5-19 0-12-5-24-16-32-5-4-11-6-18-7-5-2-10-4-16-5-36-12-76-19-117-19-7 0-14 0-22 0-188 11-337 168-337 360 0 16 0 32 3 48L56 663c-73 74-73 193 0 267l33 33c73 73 193 73 267 0l240-241c21 4 42 5 64 5 190 0 346-146 360-333 0-8 0-17 0-27 0-44-6-86-21-125zM658 639c-28 0-54-4-79-11-4 7-10 14-16 21l-258 258c-43 43-115 43-158 0l-29-29c-43-44-43-115 0-158l212-212 46-46c5-5 12-11 19-15-8-26-12-54-12-83v-1c0-152 123-275 276-275 18 0 37 1 55 5L597 207c-52 52-52 137 0 189l29 29c52 52 137 52 189 0L930 312c2 16 4 33 4 50 0 152-123 276-276 276z",
                1.0);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionInstall):
                return (
                "M1091.291429 0H78.935771C35.34848 0.035109 0.029257 35.354331 0 78.935771v863.331475c0 43.534629 35.401143 78.994286 78.935771 78.994285H1091.291429c43.534629 0 78.994286-35.401143 78.994285-78.994285V78.871406C1170.156983 35.319223 1134.849463 0.064366 1091.291429 0z m-8.835658 87.771429v78.754377H87.771429v-78.760229h994.684342zM87.771429 933.425737V254.232869h994.684342v679.140205H87.771429v0.058515zM724.95104 340.00896l-206.19264 547.605943a43.903269 43.903269 0 0 1-82.154057-31.012572l206.139977-547.547428a43.944229 43.944229 0 0 1 82.20672 30.954057zM369.558674 545.909029l-85.489371 85.489371 85.489371 85.542034a43.885714 43.885714 0 0 1-62.025143 62.083657l-116.554605-116.560457a43.8272 43.8272 0 0 1 0-62.025143l116.560457-116.49024a43.885714 43.885714 0 0 1 62.019291 61.966629z m610.567315-37.566172a43.885714 43.885714 0 0 1 0 62.083657l-116.560458 116.560457a43.768686 43.768686 0 0 1-62.019291 0 43.885714 43.885714 0 0 1 0-62.083657l85.547886-85.547885-85.547886-85.542035a43.897417 43.897417 0 0 1 62.083657-62.083657l116.496092 116.618972z",
                0.87);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionExport):
                return (
                "M511 995a128 128 0 0 1-57-13L70 791a126 126 0 0 1-70-113V311a126 126 0 0 1 15-60V248c1-2 3-5 5-8a127 127 0 0 1 49-42L454 13a128 128 0 0 1 112 0l383 190a126 126 0 0 1 72 113v360a126 126 0 0 1-70 115L568 984c-17 7-37 11-57 11z m42-470v370l360-178c14-7 23-21 23-38v-335L554 524zM85 330v347a42 42 0 0 0 23 38l360 178V523L85 330zM135 260l375 189 137-65L286 188 135 260z m245-118l363 197 150-71-365-180a42 42 0 0 0-37 0l-111 53z",
                1.0);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionWorld):
                return (
                "M819.392 0L1024 202.752v652.16a168.96 168.96 0 0 1-168.832 168.768h-104.192a47.296 47.296 0 0 1-10.752 0H283.776a47.232 47.232 0 0 1-10.752 0H168.832A168.96 168.96 0 0 1 0 854.912V168.768A168.96 168.96 0 0 1 168.832 0h650.56z m110.208 854.912V242.112l-149.12-147.776H168.896c-41.088 0-74.432 33.408-74.432 74.432v686.144c0 41.024 33.344 74.432 74.432 74.432h62.4v-190.528c0-33.408 27.136-60.544 60.544-60.544h440.448c33.408 0 60.544 27.136 60.544 60.544v190.528h62.4c41.088 0 74.432-33.408 74.432-74.432z m-604.032 74.432h372.864v-156.736H325.568v156.736z m403.52-596.48a47.168 47.168 0 1 1 0 94.336H287.872a47.168 47.168 0 1 1 0-94.336h441.216z m0-153.728a47.168 47.168 0 1 1 0 94.4H287.872a47.168 47.168 0 1 1 0-94.4h441.216z",
                0.95);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionScreenshot):
                return (
                "M853.333333 42.666667H170.666667C99.978667 42.666667 42.666667 99.978667 42.666667 170.666667v682.666666c0 70.688 57.312 128 128 128h682.666666c70.688 0 128-57.312 128-128V170.666667c0-70.688-57.312-128-128-128z m42.666667 810.666666c0 23.573333-19.093333 42.666667-42.666667 42.666667H316.341333L682.666667 529.674667l213.333333 213.322666V853.333333z m0-230.997333l-213.333333-213.333333L195.658667 896H170.666667c-23.573333 0-42.666667-19.093333-42.666667-42.666667V170.666667c0-23.573333 19.093333-42.666667 42.666667-42.666667h682.666666c23.573333 0 42.666667 19.093333 42.666667 42.666667v451.669333zM341.333333 213.333333c-70.688 0-128 57.312-128 128s57.312 128 128 128 128-57.312 128-128-57.312-128-128-128z m0 170.666667a42.666667 42.666667 0 1 1 0-85.333333 42.666667 42.666667 0 0 1 0 85.333333z",
                0.95);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod):
                return (
                "M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28a40.448 40.448 0 0 0-40.448 39.936v77.312a34.816 34.816 0 0 1-34.816 35.328H204.8a41.984 41.984 0 0 1-40.448-42.496v-200.192a35.328 35.328 0 0 1 35.328-35.328h72.704a39.936 39.936 0 0 0 39.936-39.936v-37.888a39.936 39.936 0 0 0-39.936-40.448H199.68a35.328 35.328 0 0 1-35.328-35.328V287.744A41.984 41.984 0 0 1 204.8 245.76h176.64v-32.768a102.4 102.4 0 0 1 102.4-102.4h33.792a102.4 102.4 0 0 1 102.4 102.4v32.768h170.496a41.984 41.984 0 0 1 41.984 41.984V460.8h28.672a102.4 102.4 0 0 1 102.4 102.4v33.792a102.4 102.4 0 0 1-102.4 102.4h-28.672V870.4a41.984 41.984 0 0 1-43.008 42.496z m-159.744-70.144h131.584V665.6a34.304 34.304 0 0 1 34.816-34.816h64a31.744 31.744 0 0 0 31.744-31.744V563.2a31.744 31.744 0 0 0-31.744-31.744h-64a34.816 34.816 0 0 1-34.816-35.328V316.416h-177.152a35.328 35.328 0 0 1-35.328-35.328V212.992a31.744 31.744 0 0 0-31.744-31.744h-33.792a31.744 31.744 0 0 0-31.744 31.744v68.096a35.328 35.328 0 0 1-34.816 35.328H234.496v130.048h37.888a110.592 110.592 0 0 1 110.08 110.592v37.888a110.592 110.592 0 0 1-110.08 110.592h-37.888v137.216h136.192v-42.496a110.592 110.592 0 0 1 110.592-110.08h37.888a110.592 110.592 0 0 1 110.592 110.08z",
                1.0);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionModDisabled):
                return (
                "M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28a40.448 40.448 0 0 0-40.448 39.936v77.312a34.816 34.816 0 0 1-34.816 35.328H204.8a41.984 41.984 0 0 1-40.448-42.496v-200.192a35.328 35.328 0 0 1 35.328-35.328h72.704a39.936 39.936 0 0 0 39.936-39.936v-37.888a39.936 39.936 0 0 0-39.936-40.448H199.68a35.328 35.328 0 0 1-35.328-35.328V287.744A41.984 41.984 0 0 1 204.8 245.76h176.64v-32.768a102.4 102.4 0 0 1 102.4-102.4h33.792a102.4 102.4 0 0 1 102.4 102.4v32.768h170.496a41.984 41.984 0 0 1 41.984 41.984V460.8h28.672a102.4 102.4 0 0 1 102.4 102.4v33.792a102.4 102.4 0 0 1-102.4 102.4h-28.672V870.4a41.984 41.984 0 0 1-43.008 42.496z m-159.744-70.144h131.584V665.6a34.304 34.304 0 0 1 34.816-34.816h64a31.744 31.744 0 0 0 31.744-31.744V563.2a31.744 31.744 0 0 0-31.744-31.744h-64a34.816 34.816 0 0 1-34.816-35.328V316.416h-177.152a35.328 35.328 0 0 1-35.328-35.328V212.992a31.744 31.744 0 0 0-31.744-31.744h-33.792a31.744 31.744 0 0 0-31.744 31.744v68.096a35.328 35.328 0 0 1-34.816 35.328H234.496v130.048h37.888a110.592 110.592 0 0 1 110.08 110.592v37.888a110.592 110.592 0 0 1-110.08 110.592h-37.888v137.216h136.192v-42.496a110.592 110.592 0 0 1 110.592-110.08h37.888a110.592 110.592 0 0 1 110.592 110.08z",
                1.0);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionResourcePack):
                return (
                "M884.4 130.6H140.6c-41.9 0-76 34.1-76 76v613.3c0 41.9 34.1 76 76 76h743.8c41.9 0 76-34.1 76-76V206.6c0-41.9-34.1-76-76-76z m-743.8 72h743.8c2.2 0 4 1.8 4 4v371.3L796 461.7c-21.7-27.3-54.1-43.7-88.8-45-34.7-1.3-68.1 12.7-91.7 38.3L454 630c-2.8 3-7.2 3.5-10.5 1.3l-103-70.3c-31.5-21.5-72.8-22.5-105.3-2.7l-98.6 60.1V206.6c0-2.2 1.8-4 4-4z m743.8 621.3H140.6c-2.2 0-4-1.8-4-4V702.7l136.1-83c8.4-5.1 19.1-4.8 27.2 0.7l103 70.3c15.9 10.8 35.1 15.6 54.2 13.4 19-2.2 36.7-11.2 49.8-25.3l161.6-175c9.5-10.3 22.3-15.7 36.2-15.1 13.9 0.5 26.3 6.8 35.1 17.8l148.7 187v126.3c-0.1 2.3-1.9 4.1-4.1 4.1z M231.8 400.6a69.6 69.6 0 1 0 98.4-98.4 69.6 69.6 0 1 0-98.4 98.4Z",
                0.85);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionShader):
                return (
                "M512 0c25 0 42 17 42 42v85c0 25-17 42-42 42s-42-17-42-42V42c0-25 17-42 42-42zM512 853c25 0 42 17 42 42v85c0 25-17 42-42 42s-42-17-42-42v-85c0-25 17-42 42-42zM209 149c-17-17-42-17-59 0-17 17-17 42 0 59l59 59c17 17 42 17 59 0 17-17 17-42 0-59L209 149zM755 755c17-17 42-17 59 0l59 59c17 17 17 42 0 59s-42 17-59 0l-59-59c-17-17-17-46 0-59zM42 469c-25 0-42 17-42 42s17 42 42 42h85c25 0 42-17 42-42s-17-42-42-42H42zM853 512c0-25 17-42 42-42h85c25 0 42 17 42 42s-17 42-42 42h-85c-25 0-42-17-42-42zM149 814c-17 17-17 42 0 59 17 17 42 17 59 0l59-59c17-17 17-42 0-59-17-17-42-17-59 0l-59 59zM755 268c-17-17-17-42 0-59l59-59c17-17 42-17 59 0s17 42 0 59l-59 59c-17 17-46 17-59 0z M512 213c-166 0-298 132-298 298s132 298 298 298 298-132 298-298-132-298-298-298z m0 512c-119 0-213-93-213-213s93-213 213-213 213 93 213 213-93 213-213 213z",
                1.15);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionSchematic):
                return (
                "M857.6 160a70.4 70.4 0 0 1 70.016 62.72L928 230.4v563.2a70.4 70.4 0 0 1-62.72 70.016L857.6 864H166.4A70.4 70.4 0 0 1 96 793.6V230.4A70.4 70.4 0 0 1 166.4 160h691.2z m0 64H428.8v576h428.8a6.4 6.4 0 0 0 5.888-3.904L864 793.6V230.4a6.4 6.4 0 0 0-3.904-5.888L857.6 224zM364.8 224H166.4a6.4 6.4 0 0 0-5.888 3.904L160 230.4v563.2a6.4 6.4 0 0 0 6.4 6.4h198.4V224z m413.056 55.68a32 32 0 0 1 13.76 38.592l-2.432 5.184-230.4 390.4a32 32 0 0 1-57.6-27.328l2.432-5.184 230.4-390.4a32 32 0 0 1 43.84-11.328zM262.4 294.4a32 32 0 0 1 31.488 26.24l0.512 5.76v76.8a32 32 0 0 1-63.488 5.76L230.4 403.2V326.4a32 32 0 0 1 32-32z M224 544m38.4 0l0 0q38.4 0 38.4 38.4l0 0q0 38.4-38.4 38.4l0 0q-38.4 0-38.4-38.4l0 0q0-38.4 38.4-38.4Z M224 659.2m38.4 0l0 0q38.4 0 38.4 38.4l0 0q0 38.4-38.4 38.4l0 0q-38.4 0-38.4-38.4l0 0q0-38.4 38.4-38.4Z",
                0.85);
            case (LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionServer):
                return (
                "M12.653 8.008A1.5 1.5 0 0 1 14 9.5v2l-.008.153a1.5 1.5 0 0 1-1.339 1.34L12.5 13h-10l-.153-.008a1.5 1.5 0 0 1-1.34-1.339L1 11.5v-2a1.5 1.5 0 0 1 1.347-1.492L2.5 8h10zM2.5 9a.5.5 0 0 0-.5.5v2a.5.5 0 0 0 .5.5h10a.5.5 0 0 0 .5-.5v-2a.5.5 0 0 0-.5-.5zm1 1a.5.5 0 1 1 0 1a.5.5 0 0 1 0-1m9.153-7.992A1.5 1.5 0 0 1 14 3.5v2l-.008.153a1.5 1.5 0 0 1-1.339 1.34L12.5 7h-10l-.153-.008a1.5 1.5 0 0 1-1.34-1.339L1 5.5v-2a1.5 1.5 0 0 1 1.347-1.492L2.5 2h10zM2.5 3a.5.5 0 0 0-.5.5v2a.5.5 0 0 0 .5.5h10a.5.5 0 0 0 .5-.5v-2a.5.5 0 0 0-.5-.5zm1 1a.5.5 0 1 1 0 1a.5.5 0 0 1 0-1",
                0.85);
            default:
                var icon = FrontendIconCatalog.GetSidebarIcon(title);
                return (icon.Data, icon.Scale);
        }
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

    private static Bitmap? LoadCachedBitmapFromPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var normalizedPath = Path.GetFullPath(filePath);
        lock (LauncherBitmapCacheLock)
        {
            if (LauncherBitmapCache.TryGetValue(normalizedPath, out var bitmap))
            {
                return bitmap;
            }

            bitmap = File.Exists(normalizedPath) ? new Bitmap(normalizedPath) : null;
            LauncherBitmapCache[normalizedPath] = bitmap;
            return bitmap;
        }
    }

    private static Bitmap? LoadLauncherBitmap(params string[] segments)
    {
        return LoadCachedBitmapFromPath(GetLauncherAssetPath(segments));
    }
}
