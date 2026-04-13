using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
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

    private static MinecraftLaunchPrecheckResult BuildLaunchPrecheckResult(string scenario)
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
            IsNonAsciiPathWarningDisabled: false,
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
        return title switch
        {
            "启动" => ("M52.1,164.5c-1.4,0-3.1-0.5-4.2-1.3c-2.6-1.7-4-4.2-4-7V43.8c0-2.9,1.6-5.8,4.1-7c1.2-0.8,2.7-1.2,4.1-1.2c1.5,0,2.9,0.4,4.2,1.2L153.1,93c0,0,0.1,0,0.1,0.1c2.6,1.7,4,4.2,4,7c0,3-1.7,5.8-4.2,7.1l-96.8,56.2C55.1,164,53.5,164.5,52.1,164.5z M60.4,142.1l72.1-42.1L60.4,58.2V142.1z", 0.9),
            "下载" => ("M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59c-15 0-29 13-29 29V905c0 43 35 78 78 78h787c43 0 78-35 78-78V640c0-15-13-29-29-29zM492 740c11 11 29 11 41 0l265-265c11-11 11-29 0-41l-41-41c-11-11-29-11-41 0l-110 110c-11 11-33 3-33-13V68C571 53 555 39 541 39h-59c-15 0-29 13-29 29v417c0 17-21 25-33 13l-110-110c-11-11-29-11-41 0L226 433c-11 11-11 29 0 41L492 740z", 0.9),
            "设置" => ("M940.4 463.7L773.3 174.2c-17.3-30-49.2-48.4-83.8-48.4H340.2c-34.6 0-66.5 18.5-83.8 48.4L89.2 463.7c-17.3 30-17.3 66.9 0 96.8L256.4 850c17.3 30 49.2 48.4 83.8 48.4h349.2c34.6 0 66.5-18.5 83.8-48.4l167.2-289.5c17.3-29.9 17.3-66.8 0-96.8z m-94.6 96.8L725.9 768.1c-17.3 30-49.2 48.4-83.8 48.4H387.5c-34.6 0-66.5-18.5-83.8-48.4L183.9 560.5c-17.3-30-17.3-66.9 0-96.8l119.8-207.5c17.3-30 49.2-48.4 83.8-48.4h254.6c34.6 0 66.5 18.5 83.8 48.4l119.8 207.5c17.3 30 17.3 66.9 0.1 96.8z M522.3 321.2c-2.5-0.1-5-0.2-7.5-0.2-119.9 0-214 110.3-186.3 235 15.8 70.9 71.5 126.6 142.4 142.4 17.5 3.9 34.7 5.4 51.4 4.7 102.1-3.9 183.6-87.9 183.6-191 0.1-103-81.5-187-183.6-190.9z m68.6 269.1c-18.5 18-43 28.9-68.6 30.7l-6 0.3c-30.2 0.4-58.6-11.4-79.7-33-19.5-20.1-30.7-47-30.9-75-0.3-29.6 11.1-57.4 32-78.3 20.6-20.6 48-32 77.2-32 2.5 0 5 0.1 7.5 0.3 26.7 1.8 51.5 13.2 70.5 32.5 19.6 20 30.8 46.9 31.2 74.9 0.2 30.2-11.5 58.6-33.2 79.6z", 1.1),
            "工具" => ("M623.0016 208.5376c-103.6288-103.6288-269.4144-103.6288-352.256-20.736L415.744 332.8512 332.8 415.7952 187.8016 270.6944c-82.944 82.944-82.944 248.6784 20.736 352.3072 66.56 66.6112 158.9248 88.32 276.8896 64.9728l13.2608-2.7648 198.656 198.656a41.472 41.472 0 0 0 54.7328 3.4304l3.8912-3.4304 127.8976-127.8976a41.472 41.472 0 0 0 3.4304-54.7328l-3.4304-3.8912-198.656-198.656c27.648-124.3648 6.912-221.0816-62.208-290.1504z m-253.2352-9.6256l1.1776-0.4096c64.9728-20.736 150.6304-3.4816 208.0768 54.016 50.6368 50.5344 67.4816 121.7024 48.128 220.16l-2.56 12.4928-7.4752 33.28 208.1792 208.1792-98.6624 98.6112-208.128-208.128-33.28 7.3728c-105.0624 23.3472-180.0192 7.2704-232.704-45.4656-55.04-54.9376-73.216-135.68-56.5248-199.5264l2.9696-9.728L332.8 503.6544 503.7056 332.8 369.7664 198.912z", 1.0),
            _ => ("", 1.0)
        };
    }

    private static string GetUtilityIcon(string id)
    {
        return id switch
        {
            "back" => "M858.496 188.9024 173.1072 188.9024c-30.2848 0-54.8352-24.5504-54.8352-54.8352L118.272 106.6496c0-30.2848 24.5504-54.8352 54.8352-54.8352l685.3888 0c30.2848 0 54.8352 24.5504 54.8352 54.8352l0 27.4176C913.3312 164.352 888.7808 188.9024 858.496 188.9024L858.496 188.9024zM150.6048 550.8608c0 0 300.0064-240.3584 303.0272-243.328 13.9776-13.5936 31.1808-21.8624 48.8192-24.7552 1.7152-0.3072 3.4304-0.5888 5.1456-0.768 2.7392-0.3072 5.4528-0.3584 8.192-0.3328 2.7392-0.0256 5.4272 0.0256 8.1664 0.3328 1.7408 0.1792 3.4304 0.4864 5.1456 0.768 17.664 2.8928 34.8672 11.1616 48.8192 24.7552 3.0464 2.944 303.0016 243.328 303.0016 243.328 32.384 31.5136 29.6192 63.9744-2.7392 95.5136-32.3328 31.5392-75.648 2.9696-108.0064-28.544l-185.8816-147.1232 0 485.8368c0 30.3104-24.5248 54.8608-54.8352 54.8608l-27.392 0c-30.2848 0-54.8352-24.5504-54.8352-54.8608L447.232 470.7072l-185.8304 147.0976c-32.3584 31.5392-75.6992 60.1344-108.032 28.5696C121.0368 614.8352 118.272 582.3744 150.6048 550.8608L150.6048 550.8608zM150.6048 550.8608",
            "task-manager" => "M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59c-15 0-29 13-29 29V905c0 43 35 78 78 78h787c43 0 78-35 78-78V640c0-15-13-29-29-29zM492 740c11 11 29 11 41 0l265-265c11-11 11-29 0-41l-41-41c-11-11-29-11-41 0l-110 110c-11 11-33 3-33-13V68C571 53 555 39 541 39h-59c-15 0-29 13-29 29v417c0 17-21 25-33 13l-110-110c-11-11-29-11-41 0L226 433c-11 11-11 29 0 41L492 740z",
            "game-log" => "M1091.291429 0H78.935771C35.34848 0.035109 0.029257 35.354331 0 78.935771v863.331475c0 43.534629 35.401143 78.994286 78.935771 78.994285H1091.291429c43.534629 0 78.994286-35.401143 78.994285-78.994285V78.871406C1170.156983 35.319223 1134.849463 0.064366 1091.291429 0z m-8.835658 87.771429v78.754377H87.771429v-78.760229h994.684342zM87.771429 933.425737V254.232869h994.684342v679.140205H87.771429v0.058515zM724.95104 340.00896l-206.19264 547.605943a43.903269 43.903269 0 0 1-82.154057-31.012572l206.139977-547.547428a43.944229 43.944229 0 0 1 82.20672 30.954057zM369.558674 545.909029l-85.489371 85.489371 85.489371 85.542034a43.885714 43.885714 0 0 1-62.025143 62.083657l-116.554605-116.560457a43.8272 43.8272 0 0 1 0-62.025143l116.560457-116.49024a43.885714 43.885714 0 0 1 62.019291 61.966629z m610.567315-37.566172a43.885714 43.885714 0 0 1 0 62.083657l-116.560458 116.560457a43.768686 43.768686 0 0 1-62.019291 0 43.885714 43.885714 0 0 1 0-62.083657l85.547886-85.547885-85.547886-85.542035a43.897417 43.897417 0 0 1 62.083657-62.083657l116.496092 116.618972z",
            _ => string.Empty
        };
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
        return page switch
        {
            LauncherFrontendPageKey.Download => title switch
            {
                "自动安装" => ("M17.5 2.00586c-0.56635 0-1.13224 0.212382-1.55859 0.638672l-5.29688 5.29687c-0.85258 0.852707-0.85258 2.26448 0 3.11719l2.29688 2.29688c0.852708 0.85258 2.26448 0.85258 3.11719 0l5.29688-5.29688c0.85258-0.852707 0.85258-2.26448 0-3.11719L19.0586 2.64453C18.6322 2.21824 18.0663 2.00586 17.5 2.00586Z", 0.95),
                "Mod" => ("M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28a40.448 40.448 0 0 0-40.448 39.936v77.312a34.816 34.816 0 0 1-34.816 35.328H204.8a41.984 41.984 0 0 1-40.448-42.496v-200.192a35.328 35.328 0 0 1 35.328-35.328h72.704a39.936 39.936 0 0 0 39.936-39.936v-37.888a39.936 39.936 0 0 0-39.936-40.448H199.68a35.328 35.328 0 0 1-35.328-35.328V287.744A41.984 41.984 0 0 1 204.8 245.76h176.64v-32.768a102.4 102.4 0 0 1 102.4-102.4h33.792a102.4 102.4 0 0 1 102.4 102.4v32.768h170.496a41.984 41.984 0 0 1 41.984 41.984V460.8h28.672a102.4 102.4 0 0 1 102.4 102.4v33.792a102.4 102.4 0 0 1-102.4 102.4h-28.672V870.4a41.984 41.984 0 0 1-43.008 42.496z", 0.97),
                "整合包" => ("M511 995a128 128 0 0 1-57-13L70 791a126 126 0 0 1-70-113V311a126 126 0 0 1 15-60V248c1-2 3-5 5-8a127 127 0 0 1 49-42L454 13a128 128 0 0 1 112 0l383 190a126 126 0 0 1 72 113v360a126 126 0 0 1-70 115L568 984c-17 7-37 11-57 11z", 0.98),
                "数据包" => ("M445 545c18 0 33 14 33 33v149a182 182 0 1 1-182-182z m282 0a182 182 0 1 1-182 182v-149c0-18 14-33 33-33zM412 611H296a116 116 0 1 0 116 116V611z", 0.96),
                "资源包" => ("M884.4 130.6H140.6c-41.9 0-76 34.1-76 76v613.3c0 41.9 34.1 76 76 76h743.8c41.9 0 76-34.1 76-76V206.6c0-41.9-34.1-76-76-76z", 0.81),
                "光影包" => ("M512 0c25 0 42 17 42 42v85c0 25-17 42-42 42s-42-17-42-42V42c0-25 17-42 42-42zM512 213c-166 0-298 132-298 298s132 298 298 298 298-132 298-298-132-298-298-298z", 1.04),
                "存档" => ("M17.9 17.39C17.64 16.59 16.89 16 16 16H15V13A1 1 0 0 0 14 12H8V10H10A1 1 0 0 0 11 9V7H13A2 2 0 0 0 15 5V4.59C17.93 5.77 20 8.64 20 12C20 14.08 19.2 15.97 17.9 17.39", 0.9),
                "收藏夹" => ("M10.3633 4.94727C9.49901 4.9267 8.70665 5.26821 8.10156 5.79883C7.49648 6.32944 7.05536 7.07013 6.96289 7.92969C6.87042 8.78924 7.18013 9.74328 7.89844 10.4922l2.59375 2.82226c0.784548 0.900757 2.22912 0.900759 3.01367 0l2.59766-2.82617", 0.95),
                "客户端" => ("M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59c-15 0-29 13-29 29V905c0 43 35 78 78 78h787c43 0 78-35 78-78V640c0-15-13-29-29-29z", 0.9),
                "OptiFine" => ("M439.667 538.133l-323.311-174.763c-6.554-3.641-13.836-5.098-20.389-5.098-23.301 0-45.147 20.389-45.147 48.788v341.516c0 35.681 18.205 67.721 48.06 83.741l323.311 174.763", 1.0),
                "Forge" => ("M402.807089 481.189274c-12.040221-6.991228-27.412326-2.797719-34.397415 9.242502l-8.387018 14.512529c-6.987135 12.035104-2.793626 27.40721 9.246595 34.392298", 1.0),
                "NeoForge" => ("M544.6 921.2l-23.7 32.6c14.1 10.2 33.3 10.2 47.5 0l-23.7-32.6zM114.6 608.5l-23.9 32.5 0.2 0.1 23.6-32.6z", 0.95),
                "Cleanroom" => ("M718.1 653.2c-8.5-10.5-25.6-31.1-37.9-45.9-12.3-14.7-22.2-27-22-27.1.2-.2 10-.6 21.8-1 9.1-.6 17.1-.4 25.6-2.3", 0.95),
                "Fabric" => ("M826.453333 170.666667c-100.266667-93.866667-248.96-76.373333-256-75.52a31.786667 31.786667 0 0 0-26.666666 22.613333c-1.066667 3.2-101.546667 330.666667-426.666667 445.013333", 1.02),
                "Quilt" => ("M115.6 140.4H57.2c-8.9 0-16.1 7.2-16.1 16.1V215c0 8.9 7.2 16.1 16.1 16.1h58.5c8.9 0 16.1-7.2 16.1-16.1v-58.5C131.7 147.6 124.5 140.4 115.6 140.4Z", 1.02),
                "LiteLoader" => ("M517.41 186.99l22.45 83.78h-55.04l-18.76-70.02c-2.85-10.63-13.78-16.95-24.42-14.1-10.63 2.85-16.95 13.78-14.1 24.42", 1.06),
                "LabyMod" => ("M710 350L805 350 945 250 920 470 990 565 758 820 525 565 590 470 560 250 710 350", 1.02),
                "Legacy Fabric" => ("M826.453333 170.666667c-100.266667-93.866667-248.96-76.373333-256-75.52a31.786667 31.786667 0 0 0-26.666666 22.613333c-1.066667 3.2-101.546667 330.666667-426.666667 445.013333", 1.02),
                _ => ("", 1.0)
            },
            LauncherFrontendPageKey.Setup => title switch
            {
                "启动" => ("M924.009688 527.92367C875.975633 601.905321 808.729835 663.317138 733.564477 712.920073 726.302532 832.878385 665.670459 944.569541 567.084624 1015.075229", 1.0),
                "Java" => ("M6 1A1 1 0 0 0 5 2V4A1 1 0 0 0 6 5A1 1 0 0 0 7 4V2A1 1 0 0 0 6 1ZM4 7C2.90728 7 2 7.90728 2 9v8c0 2.74958 2.25042 5 5 5h6", 1.0),
                "游戏管理" => ("M224 423.84V231.744l192-0.096 0.096 192.096L224 423.84z m192.096-256.096H223.904A64 64 0 0 0 160 231.68v192.192a64 64 0 0 0 63.904 63.904h192.192", 0.95),
                "联机" => ("M7.5 1C5.57885 1 4 2.57885 4 4.5C4 6.42115 5.57885 8 7.5 8C9.42115 8 11 6.42115 11 4.5C11 2.57885 9.42115 1 7.5 1Z", 1.0),
                "界面" => ("M106 755a59 59 0 0 1-12 9c-12 7-25 8-38 5-27-10-56 11-53 40 24 224 378 233 468 10a39 39 0 0 0-8-42l-178-176", 1.0),
                "启动器杂项" => ("M4 13c-1.0907 0-2 0.909297-2 2v5c0 1.0907 0.909297 2 2 2h5c1.0907 0 2-0.909297 2-2v-5c0-1.0907-0.909297-2-2-2z", 0.95),
                "关于" => ("M149.883623 873.911618c47.094581 47.094581 101.765247 83.95121 162.783444 109.75085 63.065787 26.618676 130.226755 40.337532 199.230553 40.337532", 0.95),
                "更新" => ("M12 1a1 1 0 0 0-1 1 1 1 0 0 0 1 1c3.9525-0.0007205 6.89118 2.31366 8.23828 5.37109 1.3471 3.05744 1.07224 6.7869-1.5957 9.70313", 1.0),
                "反馈" => ("M613.717333 64.426667l3.413334 3.242666 331.861333 331.861334a85.333333 85.333333 0 0 1 3.2 117.269333l-3.2 3.413333L573.162667 896H960", 0.9),
                "日志" => ("M4 2C3.27778 2 2.54212 2.23535 1.96094 2.75195C1.37976 3.26856 1 4.08333 1 5v2c0 1.09272 0.907275 2 2 2h2v10c0 0.916666 0.379756 1.73144 0.960938 2.24805", 0.9),
                "游戏联机" => ("M554.496 170.496c141.312 0 256 114.688 256 256s-114.688 256-256 256H402.432c-22.016-1.536-39.424-19.968-39.424-42.496", 0.85),
                _ => ("", 1.0)
            },
            LauncherFrontendPageKey.Tools => title switch
            {
                "联机大厅" => ("M554.496 170.496c141.312 0 256 114.688 256 256s-114.688 256-256 256H402.432c-22.016-1.536-39.424-19.968-39.424-42.496", 0.85),
                "测试" => ("M511 995a128 128 0 0 1-57-13L70 791a126 126 0 0 1-70-113V311a126 126 0 0 1 15-60V248c1-2 3-5 5-8a127 127 0 0 1 49-42L454 13", 0.9),
                "帮助" => ("M520.6 620.3c-11.3-0.6-20.1-4-26.9-10.5-6.6-6.3-8.1-11-10.1-20.8-1.9-9.5-1.5-16.7-1-24.9l0.3-5.3c0.5-9.9 3.5-19.6 8.8-28.1", 0.97),
                _ => ("", 1.0)
            },
            _ => title switch
            {
                "概览" => ("M149.883623 873.911618c47.094581 47.094581 101.765247 83.95121 162.783444 109.75085 63.065787 26.618676 130.226755 40.337532 199.230553 40.337532", 0.95),
                "设置" => ("M940.4 463.7L773.3 174.2c-17.3-30-49.2-48.4-83.8-48.4H340.2c-34.6 0-66.5 18.5-83.8 48.4L89.2 463.7", 1.0),
                "导出" => ("M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59", 0.9),
                "世界" => ("M17.9 17.39C17.64 16.59 16.89 16 16 16H15V13A1 1 0 0 0 14 12H8V10H10A1 1 0 0 0 11 9V7H13A2 2 0 0 0 15 5V4.59", 0.9),
                "截图" => ("M884.4 130.6H140.6c-41.9 0-76 34.1-76 76v613.3c0 41.9 34.1 76 76 76h743.8c41.9 0 76-34.1 76-76V206.6", 0.81),
                "资源包" => ("M884.4 130.6H140.6c-41.9 0-76 34.1-76 76v613.3c0 41.9 34.1 76 76 76h743.8c41.9 0 76-34.1 76-76V206.6", 0.81),
                "光影包" => ("M512 0c25 0 42 17 42 42v85c0 25-17 42-42 42s-42-17-42-42V42c0-25 17-42 42-42zM512 213c-166 0-298 132-298 298s132 298 298 298", 1.04),
                "Mod" => ("M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28", 0.97),
                "已禁用 Mod" => ("M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28", 0.97),
                "安装" => ("M17.5 2.00586c-0.56635 0-1.13224 0.212382-1.55859 0.638672l-5.29688 5.29687c-0.85258 0.852707-0.85258 2.26448 0 3.11719", 0.95),
                "服务器" => ("M7.5 1C5.57885 1 4 2.57885 4 4.5C4 6.42115 5.57885 8 7.5 8C9.42115 8 11 6.42115 11 4.5C11 2.57885 9.42115 1 7.5 1Z", 1.0),
                _ => ("", 1.0)
            }
        };
    }

    private static (string ToolTip, string IconPath, string ActionLabel, string? Command) GetSidebarAccessory(LauncherFrontendPageKey page, LauncherFrontendSubpageKey subpage, string title)
    {
        const string refreshIcon = "M875.52 148.48C783.36 56.32 655.36 0 512 0 291.84 0 107.52 138.24 30.72 332.8l122.88 46.08C204.8 230.4 348.16 128 512 128c107.52 0 199.68 40.96 271.36 112.64L640 384h384V0L875.52 148.48zM512 896c-107.52 0-199.68-40.96-271.36-112.64L384 640H0v384l148.48-148.48C240.64 967.68 368.64 1024 512 1024c220.16 0 404.48-138.24 481.28-332.8L870.4 645.12C819.2 793.6 675.84 896 512 896z";
        const string resetIcon = "M530 0c287 0 521 229 521 511s-233 511-521 511c-233 0-436-151-500-368a63 63 0 0 1 44-79 65 65 0 0 1 80 43c48 162 200 276 375 276 215 0 390-171 390-383s-174-383-390-383c-103 0-199 39-270 106l21-5a63 63 0 0 1 33 123l-157 42a65 65 0 0 1-90-42l-49-183a65 65 0 1 1 126-33l6 26A524 524 0 0 1 530 0z";

        return page switch
        {
            LauncherFrontendPageKey.Download => ("刷新", refreshIcon, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Tools => ("刷新", refreshIcon, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupJava
                or LauncherFrontendSubpageKey.SetupFeedback
                or LauncherFrontendSubpageKey.SetupUpdate => ("刷新", refreshIcon, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Setup when title is "关于" or "日志" => (string.Empty, string.Empty, string.Empty, null),
            LauncherFrontendPageKey.Setup => ("初始化设置", resetIcon, "重置", $"初始化 {title} 页面设置"),
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
        return File.Exists(filePath) ? new Bitmap(filePath) : null;
    }
}
