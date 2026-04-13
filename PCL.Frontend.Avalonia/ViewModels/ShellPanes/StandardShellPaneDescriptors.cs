using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.ViewModels.ShellPanes;

internal enum StandardShellLeftPaneKind
{
    None = 0,
    Sidebar = 1,
    InstanceSelection = 4,
    TaskManager = 5
}

internal enum StandardShellRightPaneKind
{
    SetupLaunch = 0,
    SetupLink = 1,
    SetupAbout = 2,
    SetupFeedback = 3,
    SetupLog = 4,
    SetupUpdate = 5,
    SetupGameManage = 7,
    SetupLauncherMisc = 8,
    SetupJava = 9,
    SetupUi = 10,
    DownloadInstall = 11,
    DownloadCatalog = 12,
    DownloadResource = 13,
    DownloadFavorites = 14,
    ToolsHelp = 16,
    ToolsTest = 17,
    VersionSaveInfo = 18,
    VersionSaveBackup = 19,
    VersionSaveDatapack = 20,
    InstanceOverview = 21,
    InstanceSetup = 22,
    InstanceExport = 23,
    InstanceInstall = 24,
    InstanceWorld = 25,
    InstanceScreenshot = 26,
    InstanceServer = 27,
    InstanceResource = 28,
    InstanceSelection = 29,
    Generic = 30,
    TaskManager = 31
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
