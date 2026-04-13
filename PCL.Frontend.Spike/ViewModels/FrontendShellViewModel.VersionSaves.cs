using System.IO.Compression;
using System.Text;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private FrontendVersionSavesComposition _versionSavesComposition = new(
        new FrontendVersionSaveSelectionState(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
        [],
        [],
        [],
        []);
    private string _selectedVersionSavePath = string.Empty;
    private string _versionSaveDatapackSearchQuery = string.Empty;

    public string VersionSaveDatapackSearchQuery
    {
        get => _versionSaveDatapackSearchQuery;
        set
        {
            if (SetProperty(ref _versionSaveDatapackSearchQuery, value))
            {
                RefreshVersionSaveDatapackEntries();
            }
        }
    }

    public string VersionSaveTitle => _versionSavesComposition.Selection.HasSelection
        ? _versionSavesComposition.Selection.SaveName
        : "未选择存档";

    public bool HasVersionSaveInfoEntries => VersionSaveInfoEntries.Count > 0;

    public bool HasVersionSaveSettingEntries => VersionSaveSettingEntries.Count > 0;

    public bool HasVersionSaveBackupEntries => VersionSaveBackupEntries.Count > 0;

    public bool HasNoVersionSaveBackupEntries => !HasVersionSaveBackupEntries;

    public bool HasVersionSaveDatapackEntries => VersionSaveDatapackEntries.Count > 0;

    public bool HasNoVersionSaveDatapackEntries => !HasVersionSaveDatapackEntries;

    public ActionCommand OpenVersionSaveFolderCommand => new(() =>
        OpenInstanceTarget("打开存档文件夹", _versionSavesComposition.Selection.SavePath, "当前没有可打开的存档。"));

    public ActionCommand CreateVersionSaveBackupCommand => new(CreateVersionSaveBackup);

    public ActionCommand CleanVersionSaveBackupCommand => new(CleanVersionSaveBackups);

    public ActionCommand OpenVersionSaveDatapackFolderCommand => new(() =>
        OpenInstanceTarget("打开数据包文件夹", _versionSavesComposition.Selection.DatapackDirectory, "当前存档没有数据包目录。"));

    public ActionCommand InstallVersionSaveDatapackFromFileCommand => new(() => _ = InstallVersionSaveDatapackFromFileAsync());

    public ActionCommand DownloadVersionSaveDatapackCommand => new(() =>
    {
        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadDataPack), "Opened datapack download from save details.");
    });

    public ActionCommand ExportVersionSaveDatapackInfoCommand => new(ExportVersionSaveDatapackInfo);

    public void OpenVersionSaveDetails(string savePath)
    {
        _selectedVersionSavePath = savePath;
        ReloadVersionSavesComposition();
        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.VersionSaves, LauncherFrontendSubpageKey.VersionSavesInfo), $"Opened save details for {Path.GetFileName(savePath)}.");
    }

    private void ReloadVersionSavesComposition()
    {
        _versionSavesComposition = FrontendVersionSavesCompositionService.Compose(_instanceComposition, _selectedVersionSavePath);
        if (_versionSavesComposition.Selection.HasSelection)
        {
            _selectedVersionSavePath = _versionSavesComposition.Selection.SavePath;
        }

        RefreshVersionSaveSurfaces();
    }

    private void RefreshVersionSaveSurfaces()
    {
        ReplaceItems(
            VersionSaveInfoEntries,
            _versionSavesComposition.InfoEntries.Select(entry => new KeyValueEntryViewModel(entry.Label, entry.Value)));
        ReplaceItems(
            VersionSaveSettingEntries,
            _versionSavesComposition.SettingEntries.Select(entry => new KeyValueEntryViewModel(entry.Label, entry.Value)));
        ReplaceItems(
            VersionSaveBackupEntries,
            _versionSavesComposition.Backups.Select(entry => new SimpleListEntryViewModel(
                entry.Title,
                entry.Summary,
                new ActionCommand(() => OpenInstanceTarget("查看备份", entry.Path, "备份文件不存在。")))));
        RefreshVersionSaveDatapackEntries();

        RaisePropertyChanged(nameof(VersionSaveTitle));
        RaisePropertyChanged(nameof(HasVersionSaveInfoEntries));
        RaisePropertyChanged(nameof(HasVersionSaveSettingEntries));
        RaisePropertyChanged(nameof(HasVersionSaveBackupEntries));
        RaisePropertyChanged(nameof(HasNoVersionSaveBackupEntries));
        RaisePropertyChanged(nameof(HasVersionSaveDatapackEntries));
        RaisePropertyChanged(nameof(HasNoVersionSaveDatapackEntries));
    }

    private void RefreshVersionSaveDatapackEntries()
    {
        ReplaceItems(
            VersionSaveDatapackEntries,
            _versionSavesComposition.Datapacks
                .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Meta, VersionSaveDatapackSearchQuery))
                .Select(entry => new InstanceResourceEntryViewModel(
                    LoadLauncherBitmap("Images", "Blocks", entry.IconName),
                    entry.Title,
                    entry.Summary,
                    entry.Meta,
                    new ActionCommand(() => OpenInstanceTarget("查看数据包", entry.Path, "当前数据包不存在。")))));

        RaisePropertyChanged(nameof(HasVersionSaveDatapackEntries));
        RaisePropertyChanged(nameof(HasNoVersionSaveDatapackEntries));
    }

    private void CreateVersionSaveBackup()
    {
        if (!_versionSavesComposition.Selection.HasSelection)
        {
            AddActivity("创建存档备份", "当前没有可备份的存档。");
            return;
        }

        var backupDirectory = FrontendVersionSavesCompositionService.ResolveBackupDirectory(_versionSavesComposition.Selection);
        Directory.CreateDirectory(backupDirectory);

        var archivePath = Path.Combine(
            backupDirectory,
            $"{_versionSavesComposition.Selection.SaveName}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var file in Directory.EnumerateFiles(_versionSavesComposition.Selection.SavePath, "*", SearchOption.AllDirectories))
            {
                archive.CreateEntryFromFile(file, Path.GetRelativePath(_versionSavesComposition.Selection.SavePath, file));
            }
        }

        ReloadVersionSavesComposition();
        AddActivity("创建存档备份", archivePath);
    }

    private void CleanVersionSaveBackups()
    {
        if (!_versionSavesComposition.Selection.HasSelection)
        {
            AddActivity("清理备份记录", "当前没有可清理的存档。");
            return;
        }

        var removed = 0;
        foreach (var backup in _versionSavesComposition.Backups)
        {
            try
            {
                var file = new FileInfo(backup.Path);
                if (file.Exists && file.Length == 0)
                {
                    file.Delete();
                    removed++;
                }
            }
            catch
            {
                // Ignore single-file cleanup failures.
            }
        }

        ReloadVersionSavesComposition();
        AddActivity("清理备份记录", removed == 0 ? "没有检测到需要清理的空备份文件。" : $"已清理 {removed} 个空备份文件。");
    }

    private async Task InstallVersionSaveDatapackFromFileAsync()
    {
        if (!_versionSavesComposition.Selection.HasSelection)
        {
            AddActivity("安装数据包", "当前没有选中的存档。");
            return;
        }

        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync("选择数据包文件", "压缩包或数据包文件", "*.zip");
        }
        catch (Exception ex)
        {
            AddActivity("安装数据包失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("安装数据包", "已取消选择数据包文件。");
            return;
        }

        Directory.CreateDirectory(_versionSavesComposition.Selection.DatapackDirectory);
        var targetPath = Path.Combine(_versionSavesComposition.Selection.DatapackDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, targetPath, true);
        ReloadVersionSavesComposition();
        AddActivity("安装数据包", $"{sourcePath} -> {targetPath}");
    }

    private void ExportVersionSaveDatapackInfo()
    {
        if (!_versionSavesComposition.Selection.HasSelection)
        {
            AddActivity("导出数据包信息", "当前没有选中的存档。");
            return;
        }

        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "save-datapacks");
        Directory.CreateDirectory(exportDirectory);
        var outputPath = Path.Combine(exportDirectory, $"{_versionSavesComposition.Selection.SaveName}-datapacks.txt");
        var lines = _versionSavesComposition.Datapacks.Select(entry => $"{entry.Title} | {entry.Meta} | {entry.Summary}").ToArray();
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        OpenInstanceTarget("导出数据包信息", outputPath, "导出文件不存在。");
    }
}
