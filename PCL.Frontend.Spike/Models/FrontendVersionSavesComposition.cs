namespace PCL.Frontend.Spike.Models;

internal sealed record FrontendVersionSavesComposition(
    FrontendVersionSaveSelectionState Selection,
    IReadOnlyList<FrontendVersionSaveInfoEntry> InfoEntries,
    IReadOnlyList<FrontendVersionSaveInfoEntry> SettingEntries,
    IReadOnlyList<FrontendVersionSaveBackupEntry> Backups,
    IReadOnlyList<FrontendVersionSaveDatapackEntry> Datapacks);

internal sealed record FrontendVersionSaveSelectionState(
    bool HasSelection,
    string InstanceName,
    string SaveName,
    string SavePath,
    string DatapackDirectory,
    string BackupDirectory);

internal sealed record FrontendVersionSaveInfoEntry(
    string Label,
    string Value);

internal sealed record FrontendVersionSaveBackupEntry(
    string Title,
    string Summary,
    string Path);

internal sealed record FrontendVersionSaveDatapackEntry(
    string Title,
    string Summary,
    string Meta,
    string Path,
    string IconName);
