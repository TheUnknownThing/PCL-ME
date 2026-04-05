using PCL.Core.App.Essentials;

namespace PCL.Frontend.Spike.ViewModels.ShellPanes;

internal enum StandardShellLeftPaneKind
{
    Sidebar = 0,
    Summary = 1,
    Empty = 2
}

internal enum StandardShellRightPaneKind
{
    SetupLaunch = 0,
    SetupAbout = 1,
    SetupFeedback = 2,
    SetupLog = 3,
    SetupUpdate = 4,
    SetupGameLink = 5,
    SetupGameManage = 6,
    SetupLauncherMisc = 7,
    SetupJava = 8,
    SetupUi = 9,
    DownloadInstall = 10,
    DownloadCatalog = 11,
    DownloadResource = 12,
    DownloadFavorites = 13,
    ToolsGameLink = 14,
    ToolsHelp = 15,
    ToolsTest = 16,
    VersionSaveInfo = 17,
    VersionSaveBackup = 18,
    VersionSaveDatapack = 19,
    InstanceOverview = 20,
    InstanceSetup = 21,
    InstanceExport = 22,
    InstanceInstall = 23,
    InstanceWorld = 24,
    InstanceScreenshot = 25,
    InstanceServer = 26,
    InstanceResource = 27,
    Generic = 28
}

internal enum StandardShellRightPaneGroup
{
    SetupFamily = 0,
    DownloadInstall = 1,
    DownloadCatalog = 2,
    DownloadResource = 3,
    DownloadFavorites = 4,
    ToolsFamily = 5,
    VersionSavesFamily = 6,
    InstanceOverviewFamily = 7,
    InstanceSetupFamily = 8,
    InstanceContentFamily = 9,
    Generic = 10
}

internal sealed record StandardShellLeftPaneDescriptor(
    StandardShellLeftPaneKind Kind,
    string Key);

internal sealed record StandardShellRightPaneDescriptor(
    StandardShellRightPaneKind Kind,
    StandardShellRightPaneGroup Group,
    string Key,
    bool UsesCompatibilityView);

internal sealed record StandardShellPaneResolution(
    LauncherFrontendRoute Route,
    StandardShellLeftPaneDescriptor? LeftPane,
    StandardShellRightPaneDescriptor? RightPane);
