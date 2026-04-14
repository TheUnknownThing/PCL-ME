using System.Collections.Immutable;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendUiVisibilityService
{
    private const string HidePageDownloadKey = "UiHiddenPageDownload";
    private const string HidePageSetupKey = "UiHiddenPageSetup";
    private const string HidePageToolsKey = "UiHiddenPageTools";
    private const string HideSetupLaunchKey = "UiHiddenSetupLaunch";
    private const string HideSetupJavaKey = "UiHiddenSetupJava";
    private const string HideSetupGameManageKey = "UiHiddenSetupGameManage";
    private const string HideSetupUiKey = "UiHiddenSetupUi";
    private const string HideSetupLauncherMiscKey = "UiHiddenSetupLauncherMisc";
    private const string HideSetupUpdateKey = "UiHiddenSetupUpdate";
    private const string HideSetupAboutKey = "UiHiddenSetupAbout";
    private const string HideSetupFeedbackKey = "UiHiddenSetupFeedback";
    private const string HideSetupLogKey = "UiHiddenSetupLog";
    private const string HideToolsTestKey = "UiHiddenToolsTest";
    private const string HideToolsHelpKey = "UiHiddenToolsHelp";
    private const string HideVersionEditKey = "UiHiddenVersionEdit";
    private const string HideVersionExportKey = "UiHiddenVersionExport";
    private const string HideVersionSaveKey = "UiHiddenVersionSave";
    private const string HideVersionScreenshotKey = "UiHiddenVersionScreenshot";
    private const string HideVersionModKey = "UiHiddenVersionMod";
    private const string HideVersionResourcePackKey = "UiHiddenVersionResourcePack";
    private const string HideVersionShaderKey = "UiHiddenVersionShader";
    private const string HideVersionSchematicKey = "UiHiddenVersionSchematic";
    private const string HideVersionServerKey = "UiHiddenVersionServer";
    private const string HideFunctionSelectKey = "UiHiddenFunctionSelect";
    private const string HideFunctionModUpdateKey = "UiHiddenFunctionModUpdate";
    private const string HideFunctionHiddenKey = "UiHiddenFunctionHidden";

    private static readonly ImmutableHashSet<string> EmptyHiddenKeySet = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);

    public static FrontendUiVisibilityPreferences BuildPreferences(
        FrontendSetupUiState uiState,
        bool forceShowHiddenItems)
    {
        ArgumentNullException.ThrowIfNull(uiState);

        var hiddenKeys = uiState.ToggleGroups
            .SelectMany(group => group.Items)
            .Where(item => item.IsChecked)
            .Select(item => item.ConfigKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToImmutableHashSet(StringComparer.Ordinal);

        return new FrontendUiVisibilityPreferences(forceShowHiddenItems, hiddenKeys);
    }

    public static bool ShouldShowFunctionHiddenCard(FrontendUiVisibilityPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.ForceShowHiddenItems || !preferences.IsHidden(HideFunctionHiddenKey);
    }

    public static bool ShouldShowLaunchInstanceManagement(FrontendUiVisibilityPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.ForceShowHiddenItems || !preferences.IsHidden(HideFunctionSelectKey);
    }

    public static bool ShouldShowModUpdateAction(FrontendUiVisibilityPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.ForceShowHiddenItems || !preferences.IsHidden(HideFunctionModUpdateKey);
    }

    public static LauncherFrontendNavigationView FilterNavigationView(
        LauncherFrontendNavigationView navigation,
        FrontendUiVisibilityPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(preferences);

        if (preferences.ForceShowHiddenItems || preferences.HiddenKeys.Count == 0)
        {
            return navigation;
        }

        return navigation with
        {
            TopLevelEntries = navigation.TopLevelEntries
                .Where(entry => IsTopLevelPageVisible(entry.Route.Page, preferences))
                .ToArray(),
            SidebarEntries = navigation.SidebarEntries
                .Where(entry => IsRouteVisible(entry.Route, preferences))
                .ToArray()
        };
    }

    public static LauncherFrontendRoute NormalizeRoute(
        LauncherFrontendRoute route,
        FrontendUiVisibilityPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (IsRouteVisible(route, preferences))
        {
            return route;
        }

        return route.Page switch
        {
            LauncherFrontendPageKey.Download => ResolveFirstVisibleTopLevelRoute(preferences),
            LauncherFrontendPageKey.Setup => ResolveFirstVisibleRouteForPage(
                LauncherFrontendPageKey.Setup,
                preferences,
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupUI),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupGameManage),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupUpdate),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupJava),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLauncherMisc),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupAbout),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupFeedback),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLog)),
            LauncherFrontendPageKey.Tools => ResolveFirstVisibleRouteForPage(
                LauncherFrontendPageKey.Tools,
                preferences,
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsTest),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsLauncherHelp)),
            LauncherFrontendPageKey.InstanceSetup => ResolveFirstVisibleRouteForPage(
                LauncherFrontendPageKey.InstanceSetup,
                preferences,
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionSetup),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionInstall),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionExport),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionWorld),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionScreenshot),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionMod),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionResourcePack),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionShader),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionSchematic),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionServer)),
            LauncherFrontendPageKey.HelpDetail => ResolveFirstVisibleRouteForPage(
                LauncherFrontendPageKey.Tools,
                preferences,
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsLauncherHelp),
                new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsTest)),
            LauncherFrontendPageKey.VersionSaves => ResolveFirstVisibleRouteForPage(
                LauncherFrontendPageKey.InstanceSetup,
                preferences,
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionWorld),
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall)),
            _ => ResolveFirstVisibleTopLevelRoute(preferences)
        };
    }

    public static bool IsTopLevelPageVisible(
        LauncherFrontendPageKey page,
        FrontendUiVisibilityPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (preferences.ForceShowHiddenItems)
        {
            return true;
        }

        return page switch
        {
            LauncherFrontendPageKey.Download => !preferences.IsHidden(HidePageDownloadKey),
            LauncherFrontendPageKey.Setup => !preferences.IsHidden(HidePageSetupKey),
            LauncherFrontendPageKey.Tools => !preferences.IsHidden(HidePageToolsKey),
            _ => true
        };
    }

    public static bool IsRouteVisible(
        LauncherFrontendRoute route,
        FrontendUiVisibilityPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (preferences.ForceShowHiddenItems)
        {
            return true;
        }

        return route.Page switch
        {
            LauncherFrontendPageKey.Download => IsTopLevelPageVisible(route.Page, preferences),
            LauncherFrontendPageKey.Setup => IsTopLevelPageVisible(route.Page, preferences) && !preferences.IsHidden(route.Subpage switch
            {
                LauncherFrontendSubpageKey.SetupLaunch => HideSetupLaunchKey,
                LauncherFrontendSubpageKey.SetupJava => HideSetupJavaKey,
                LauncherFrontendSubpageKey.SetupGameManage => HideSetupGameManageKey,
                LauncherFrontendSubpageKey.SetupUI => HideSetupUiKey,
                LauncherFrontendSubpageKey.SetupLauncherMisc => HideSetupLauncherMiscKey,
                LauncherFrontendSubpageKey.SetupUpdate => HideSetupUpdateKey,
                LauncherFrontendSubpageKey.SetupAbout => HideSetupAboutKey,
                LauncherFrontendSubpageKey.SetupFeedback => HideSetupFeedbackKey,
                LauncherFrontendSubpageKey.SetupLog => HideSetupLogKey,
                _ => string.Empty
            }),
            LauncherFrontendPageKey.Tools => IsTopLevelPageVisible(route.Page, preferences) && !preferences.IsHidden(route.Subpage switch
            {
                LauncherFrontendSubpageKey.ToolsTest => HideToolsTestKey,
                LauncherFrontendSubpageKey.ToolsLauncherHelp => HideToolsHelpKey,
                _ => string.Empty
            }),
            LauncherFrontendPageKey.InstanceSetup => !preferences.IsHidden(route.Subpage switch
            {
                LauncherFrontendSubpageKey.VersionInstall => HideVersionEditKey,
                LauncherFrontendSubpageKey.VersionExport => HideVersionExportKey,
                LauncherFrontendSubpageKey.VersionWorld => HideVersionSaveKey,
                LauncherFrontendSubpageKey.VersionScreenshot => HideVersionScreenshotKey,
                LauncherFrontendSubpageKey.VersionMod => HideVersionModKey,
                LauncherFrontendSubpageKey.VersionModDisabled => HideVersionModKey,
                LauncherFrontendSubpageKey.VersionResourcePack => HideVersionResourcePackKey,
                LauncherFrontendSubpageKey.VersionShader => HideVersionShaderKey,
                LauncherFrontendSubpageKey.VersionSchematic => HideVersionSchematicKey,
                LauncherFrontendSubpageKey.VersionServer => HideVersionServerKey,
                _ => string.Empty
            }),
            LauncherFrontendPageKey.HelpDetail => IsTopLevelPageVisible(LauncherFrontendPageKey.Tools, preferences)
                                                && !preferences.IsHidden(HideToolsHelpKey),
            LauncherFrontendPageKey.CompDetail => IsTopLevelPageVisible(LauncherFrontendPageKey.Download, preferences),
            LauncherFrontendPageKey.VersionSaves => !preferences.IsHidden(HideVersionSaveKey),
            _ => true
        };
    }

    private static LauncherFrontendRoute ResolveFirstVisibleTopLevelRoute(FrontendUiVisibilityPreferences preferences)
    {
        return ResolveFirstVisibleRouteForPage(
            LauncherFrontendPageKey.Launch,
            preferences,
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadInstall),
            new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch),
            new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsTest));
    }

    private static LauncherFrontendRoute ResolveFirstVisibleRouteForPage(
        LauncherFrontendPageKey fallbackPage,
        FrontendUiVisibilityPreferences preferences,
        params LauncherFrontendRoute[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (IsRouteVisible(candidate, preferences))
            {
                return candidate;
            }
        }

        return fallbackPage == LauncherFrontendPageKey.Launch
            ? new LauncherFrontendRoute(LauncherFrontendPageKey.Launch)
            : ResolveFirstVisibleTopLevelRoute(preferences);
    }
}

internal sealed record FrontendUiVisibilityPreferences(
    bool ForceShowHiddenItems,
    ImmutableHashSet<string> HiddenKeys)
{
    private static readonly ImmutableHashSet<string> FallbackHiddenKeySet = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);

    public bool IsHidden(string key)
    {
        if (ForceShowHiddenItems || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return (HiddenKeys ?? FallbackHiddenKeySet).Contains(key);
    }
}
