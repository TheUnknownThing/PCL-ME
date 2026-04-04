using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _instanceOverviewName = "Modern Fabric Demo";
    private string _instanceOverviewSubtitle = "Fabric 0.16.9 / Minecraft 1.21.1 / 独立实例";
    private Bitmap? _instanceOverviewSelectedIcon;
    private int _selectedInstanceOverviewIconIndex;
    private int _selectedInstanceOverviewCategoryIndex;
    private bool _isInstanceOverviewStarred = true;
    private readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> _instanceOverviewIconOptions =
    [
        new DownloadResourceFilterOptionViewModel("自动", "Fabric.png"),
        new DownloadResourceFilterOptionViewModel("圆石", "CobbleStone.png"),
        new DownloadResourceFilterOptionViewModel("命令方块", "CommandBlock.png"),
        new DownloadResourceFilterOptionViewModel("金块", "GoldBlock.png"),
        new DownloadResourceFilterOptionViewModel("草方块", "Grass.png"),
        new DownloadResourceFilterOptionViewModel("土径", "GrassPath.png"),
        new DownloadResourceFilterOptionViewModel("铁砧", "Anvil.png"),
        new DownloadResourceFilterOptionViewModel("红石块", "RedstoneBlock.png"),
        new DownloadResourceFilterOptionViewModel("红石灯（开）", "RedstoneLampOn.png"),
        new DownloadResourceFilterOptionViewModel("红石灯（关）", "RedstoneLampOff.png"),
        new DownloadResourceFilterOptionViewModel("鸡蛋", "Egg.png"),
        new DownloadResourceFilterOptionViewModel("布料（Fabric）", "Fabric.png"),
        new DownloadResourceFilterOptionViewModel("方格（Quilt）", "Quilt.png"),
        new DownloadResourceFilterOptionViewModel("狐狸（NeoForge）", "NeoForge.png"),
        new DownloadResourceFilterOptionViewModel("Cleanroom", "Cleanroom.png")
    ];
    private readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> _instanceOverviewCategoryOptions =
    [
        new DownloadResourceFilterOptionViewModel("自动", "自动"),
        new DownloadResourceFilterOptionViewModel("从实例列表中隐藏", "从实例列表中隐藏"),
        new DownloadResourceFilterOptionViewModel("可安装 Mod 的实例", "可安装 Mod 的实例"),
        new DownloadResourceFilterOptionViewModel("常规实例", "常规实例"),
        new DownloadResourceFilterOptionViewModel("不常用实例", "不常用实例"),
        new DownloadResourceFilterOptionViewModel("愚人节版本", "愚人节版本")
    ];

    public ObservableCollection<string> InstanceOverviewDisplayTags { get; } = [];

    public ObservableCollection<KeyValueEntryViewModel> InstanceOverviewInfoEntries { get; } = [];

    public string InstanceOverviewName
    {
        get => _instanceOverviewName;
        private set => SetProperty(ref _instanceOverviewName, value);
    }

    public string InstanceOverviewSubtitle
    {
        get => _instanceOverviewSubtitle;
        private set => SetProperty(ref _instanceOverviewSubtitle, value);
    }

    public Bitmap? InstanceOverviewSelectedIcon
    {
        get => _instanceOverviewSelectedIcon;
        private set => SetProperty(ref _instanceOverviewSelectedIcon, value);
    }

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> InstanceOverviewIconOptions => _instanceOverviewIconOptions;

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> InstanceOverviewCategoryOptions => _instanceOverviewCategoryOptions;

    public int SelectedInstanceOverviewIconIndex
    {
        get => _selectedInstanceOverviewIconIndex;
        set
        {
            var nextValue = Math.Clamp(value, 0, InstanceOverviewIconOptions.Count - 1);
            if (SetProperty(ref _selectedInstanceOverviewIconIndex, nextValue))
            {
                RefreshInstanceOverviewSelectionState();
            }
        }
    }

    public int SelectedInstanceOverviewCategoryIndex
    {
        get => _selectedInstanceOverviewCategoryIndex;
        set
        {
            var nextValue = Math.Clamp(value, 0, InstanceOverviewCategoryOptions.Count - 1);
            if (SetProperty(ref _selectedInstanceOverviewCategoryIndex, nextValue))
            {
                RaisePropertyChanged(nameof(InstanceOverviewCategoryLabel));
            }
        }
    }

    public string InstanceOverviewCategoryLabel => InstanceOverviewCategoryOptions[SelectedInstanceOverviewCategoryIndex].Label;

    public string InstanceOverviewFavoriteButtonText => _isInstanceOverviewStarred ? "移出收藏夹" : "加入收藏夹";

    public bool HasInstanceOverviewInfoEntries => InstanceOverviewInfoEntries.Count > 0;

    public ActionCommand RenameInstanceCommand => new(() => _ = RenameInstanceAsync());

    public ActionCommand EditInstanceDescriptionCommand => new(() => _ = EditInstanceDescriptionAsync());

    public ActionCommand ToggleInstanceFavoriteCommand => new(ToggleInstanceFavorite);

    public ActionCommand OpenInstanceFolderCommand => new(() =>
        OpenInstanceTarget("实例文件夹", _instanceComposition.Selection.InstanceDirectory, "当前未选择实例。"));

    public ActionCommand OpenInstanceSavesFolderCommand => new(() =>
        OpenInstanceTarget(
            "存档文件夹",
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves") : string.Empty,
            "当前实例没有存档目录。"));

    public ActionCommand OpenInstanceModsFolderCommand => new(() =>
        OpenInstanceTarget(
            "Mod 文件夹",
            ResolveCurrentInstanceResourceDirectory("mods"),
            "当前实例没有 Mod 目录。"));

    public ActionCommand ExportInstanceScriptCommand => new(ExportInstanceScript);

    public ActionCommand TestInstanceCommand => new(TestInstance);

    public ActionCommand CheckInstanceFilesCommand => new(CheckInstanceFiles);

    public ActionCommand RestoreInstanceCommand => new(() => _ = ResetInstanceAsync());

    public ActionCommand DeleteInstanceCommand => new(() => _ = DeleteInstanceAsync());

    public ActionCommand PatchInstanceCoreCommand => new(() => _ = PatchInstanceCoreAsync());

    private void InitializeInstanceOverviewSurface()
    {
        var overview = _instanceComposition.Overview;
        InstanceOverviewName = overview.Name;
        InstanceOverviewSubtitle = overview.Subtitle;
        _selectedInstanceOverviewIconIndex = Math.Clamp(overview.IconIndex, 0, InstanceOverviewIconOptions.Count - 1);
        _selectedInstanceOverviewCategoryIndex = Math.Clamp(overview.CategoryIndex, 0, InstanceOverviewCategoryOptions.Count - 1);
        _isInstanceOverviewStarred = overview.IsStarred;

        ReplaceItems(InstanceOverviewDisplayTags, overview.DisplayTags);
        ReplaceItems(
            InstanceOverviewInfoEntries,
            overview.InfoEntries.Select(entry => new KeyValueEntryViewModel(entry.Label, entry.Value)));

        InstanceOverviewSelectedIcon = LoadInstanceBitmap(
            overview.IconPath,
            "Images",
            "Blocks",
            InstanceOverviewIconOptions[_selectedInstanceOverviewIconIndex].FilterValue);
    }

    private void RefreshInstanceOverviewSurface()
    {
        if (!IsInstanceOverviewSurface)
        {
            return;
        }

        RaisePropertyChanged(nameof(InstanceOverviewName));
        RaisePropertyChanged(nameof(InstanceOverviewSubtitle));
        RaisePropertyChanged(nameof(InstanceOverviewSelectedIcon));
        RaisePropertyChanged(nameof(InstanceOverviewIconOptions));
        RaisePropertyChanged(nameof(InstanceOverviewCategoryOptions));
        RaisePropertyChanged(nameof(SelectedInstanceOverviewIconIndex));
        RaisePropertyChanged(nameof(SelectedInstanceOverviewCategoryIndex));
        RaisePropertyChanged(nameof(InstanceOverviewCategoryLabel));
        RaisePropertyChanged(nameof(InstanceOverviewFavoriteButtonText));
        RaisePropertyChanged(nameof(HasInstanceOverviewInfoEntries));
    }

    private void RefreshInstanceOverviewSelectionState()
    {
        var overview = _instanceComposition.Overview;
        if (SelectedInstanceOverviewIconIndex == overview.IconIndex)
        {
            InstanceOverviewSelectedIcon = LoadInstanceBitmap(
                overview.IconPath,
                "Images",
                "Blocks",
                InstanceOverviewIconOptions[SelectedInstanceOverviewIconIndex].FilterValue);
        }
        else if (SelectedInstanceOverviewIconIndex == 0)
        {
            InstanceOverviewSelectedIcon = LoadInstanceBitmap(
                overview.IconPath,
                "Images",
                "Blocks",
                "Grass.png");
        }
        else
        {
            var iconName = InstanceOverviewIconOptions[SelectedInstanceOverviewIconIndex].FilterValue;
            InstanceOverviewSelectedIcon = LoadLauncherBitmap("Images", "Blocks", iconName);
        }

        RaisePropertyChanged(nameof(InstanceOverviewCategoryLabel));
    }

    private void ToggleInstanceFavorite()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("收藏实例", "当前未选择实例。");
            return;
        }

        _isInstanceOverviewStarred = !_isInstanceOverviewStarred;
        _shellActionService.PersistInstanceValue(_instanceComposition.Selection.InstanceDirectory, "IsStar", _isInstanceOverviewStarred);
        ReloadInstanceComposition();
        RaisePropertyChanged(nameof(InstanceOverviewFavoriteButtonText));
        AddActivity(_isInstanceOverviewStarred ? "加入收藏夹" : "移出收藏夹", _instanceComposition.Selection.InstanceName);
    }

    private async Task RenameInstanceAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("修改实例名", "当前未选择实例。");
            return;
        }

        var oldName = _instanceComposition.Selection.InstanceName;
        var newName = await _shellActionService.PromptForTextAsync(
            "重命名实例",
            "请输入新的实例名称。名称会同时用于实例文件夹、核心文件和当前选中的实例配置。",
            oldName,
            "重命名",
            "实例名称");
        if (newName is null)
        {
            AddActivity("修改实例名", "已取消重命名。");
            return;
        }

        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            AddActivity("修改实例名", "实例名称不能为空。");
            return;
        }

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            AddActivity("修改实例名", "实例名称未发生变化。");
            return;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || newName.Contains(Path.DirectorySeparatorChar)
            || newName.Contains(Path.AltDirectorySeparatorChar))
        {
            AddActivity("修改实例名", "实例名称包含无效字符。");
            return;
        }

        var launcherDirectory = _instanceComposition.Selection.LauncherDirectory;
        var oldDirectory = _instanceComposition.Selection.InstanceDirectory;
        var newDirectory = Path.Combine(launcherDirectory, "versions", newName);
        if (Directory.Exists(newDirectory) && !string.Equals(oldDirectory, newDirectory, StringComparison.OrdinalIgnoreCase))
        {
            AddActivity("修改实例名", $"已存在同名实例：{newName}");
            return;
        }

        try
        {
            MoveDirectoryPreservingCase(oldDirectory, newDirectory);
            RenamePathPreservingCase(
                Path.Combine(newDirectory, $"{oldName}.json"),
                Path.Combine(newDirectory, $"{newName}.json"));
            RenamePathPreservingCase(
                Path.Combine(newDirectory, $"{oldName}.jar"),
                Path.Combine(newDirectory, $"{newName}.jar"));
            MoveDirectoryPreservingCase(
                Path.Combine(newDirectory, $"{oldName}-natives"),
                Path.Combine(newDirectory, $"{newName}-natives"),
                treatMissingSourceAsSuccess: true);
            ReplacePathText(Path.Combine(newDirectory, "PCL", "config.v1.yml"), oldDirectory, newDirectory);
            RewriteInstanceManifestId(Path.Combine(newDirectory, $"{newName}.json"), newName);
            _shellActionService.PersistLocalValue("LaunchInstanceSelect", newName);
            ReloadInstanceComposition();
            AddActivity("修改实例名", $"{oldName} -> {newName}");
        }
        catch (Exception ex)
        {
            AddActivity("修改实例名失败", ex.Message);
        }
    }

    private async Task EditInstanceDescriptionAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("修改实例描述", "当前未选择实例。");
            return;
        }

        var currentValue = _instanceComposition.Setup.CustomInfo;
        var nextValue = await _shellActionService.PromptForTextAsync(
            "更改描述",
            "修改实例的描述文本，留空则恢复为默认描述。",
            currentValue,
            "保存",
            "实例描述");
        if (nextValue is null)
        {
            AddActivity("修改实例描述", "已取消修改。");
            return;
        }

        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;
        var trimmedValue = nextValue.Trim();
        try
        {
            if (string.IsNullOrWhiteSpace(trimmedValue))
            {
                _shellActionService.RemoveInstanceValues(instanceDirectory, ["VersionArgumentInfo", "CustomInfo"]);
            }
            else
            {
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentInfo", trimmedValue);
                _shellActionService.PersistInstanceValue(instanceDirectory, "CustomInfo", trimmedValue);
            }

            ReloadInstanceComposition();
            AddActivity("修改实例描述", string.IsNullOrWhiteSpace(trimmedValue) ? "已恢复为默认描述。" : trimmedValue);
        }
        catch (Exception ex)
        {
            AddActivity("修改实例描述失败", ex.Message);
        }
    }

    private void ExportInstanceScript()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("导出启动脚本", "当前未选择实例。");
            return;
        }

        var extension = OperatingSystem.IsWindows() ? ".cmd" : OperatingSystem.IsMacOS() ? ".command" : ".sh";
        var scriptPath = Path.Combine(
            GetInstanceOverviewArtifactDirectory("launch-scripts"),
            $"启动 {_instanceComposition.Selection.InstanceName}{extension}");
        var classpathEntries = _launchComposition.ClasspathPlan.Entries.Count;
        var lines = OperatingSystem.IsWindows()
            ? BuildWindowsLaunchExportLines(classpathEntries)
            : BuildUnixLaunchExportLines(classpathEntries);
        File.WriteAllText(scriptPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        TryMarkFileExecutable(scriptPath);
        OpenInstanceTarget("导出启动脚本", scriptPath, "启动脚本不存在。");
    }

    private void TestInstance()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("测试游戏", "当前未选择实例。");
            return;
        }

        var reportPath = WriteInstanceOverviewArtifact(
            "test-launch",
            $"{_instanceComposition.Selection.InstanceName}-test-launch.txt",
            [
                $"实例: {_instanceComposition.Selection.InstanceName}",
                $"档案: {_launchComposition.SelectedProfile.IdentityLabel}",
                $"Java: {_launchComposition.SelectedJavaRuntime?.DisplayName ?? "未找到可用 Java"}",
                $"Java 可执行文件: {_launchComposition.SelectedJavaRuntime?.ExecutablePath ?? "未设置"}",
                $"游戏目录: {_launchComposition.InstancePath}",
                $"Natives 目录: {_launchComposition.NativesDirectory}",
                $"分辨率: {_launchComposition.ResolutionPlan.Width} x {_launchComposition.ResolutionPlan.Height}",
                $"类路径条目数: {_launchComposition.ClasspathPlan.Entries.Count}",
                string.Empty,
                "当前 replacement shell 仍在推进真实启动切换。",
                "这个测试输出记录的是当前运行时的启动上下文，便于在不改动页面布局的情况下核对实例、Java 与路径绑定。"
            ]);
        OpenInstanceTarget("测试游戏", reportPath, "测试输出不存在。");
    }

    private void CheckInstanceFiles()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("补全文件", "当前未选择实例。");
            return;
        }

        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;
        var manifestPath = Path.Combine(instanceDirectory, $"{_instanceComposition.Selection.InstanceName}.json");
        var jarPath = Path.Combine(instanceDirectory, $"{_instanceComposition.Selection.InstanceName}.jar");
        var reportPath = WriteInstanceOverviewArtifact(
            "file-checks",
            $"{_instanceComposition.Selection.InstanceName}-file-check.txt",
            [
                $"实例: {_instanceComposition.Selection.InstanceName}",
                $"实例目录: {instanceDirectory}",
                $"核心 Json: {(File.Exists(manifestPath) ? "已检测到" : "缺失")} • {manifestPath}",
                $"核心 Jar: {(File.Exists(jarPath) ? "已检测到" : "缺失")} • {jarPath}",
                $"存档数量: {_instanceComposition.World.Entries.Count}",
                $"截图数量: {_instanceComposition.Screenshot.Entries.Count}",
                $"服务器条目: {_instanceComposition.Server.Entries.Count}",
                $"启用 Mod: {_instanceComposition.Mods.Entries.Count}",
                $"禁用 Mod: {_instanceComposition.DisabledMods.Entries.Count}",
                $"资源包: {_instanceComposition.ResourcePacks.Entries.Count}",
                $"光影包: {_instanceComposition.Shaders.Entries.Count}",
                $"投影结构: {_instanceComposition.Schematics.Entries.Count}",
                $"关闭文件校验: {(DisableInstanceFileValidation ? "是" : "否")}",
                string.Empty,
                "此报告用于核对 replacement shell 当前能读到的实例文件状态。",
                "真实的下载补全执行仍属于后续启动/安装切换工作。"
            ]);
        OpenInstanceTarget("补全文件", reportPath, "检查报告不存在。");
    }

    private async Task ResetInstanceAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("重置实例", "当前未选择实例。");
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            "实例重置确认",
            $"确定要为 {_instanceComposition.Selection.InstanceName} 生成重置备份与执行计划吗？当前实现会先保存核心快照，方便后续继续推进真实安装切换。",
            "生成计划");
        if (!confirmed)
        {
            AddActivity("重置实例", "已取消重置计划生成。");
            return;
        }

        try
        {
            var backupDirectory = BackupInstanceCoreFiles("reset-backups");
            var reportPath = WriteInstanceOverviewArtifact(
                "reset-plans",
                $"{_instanceComposition.Selection.InstanceName}-reset-plan.txt",
                [
                    $"实例: {_instanceComposition.Selection.InstanceName}",
                    $"备份目录: {backupDirectory}",
                    $"Minecraft: {_instanceComposition.Selection.VanillaVersion}",
                    .. InstanceOverviewInfoEntries.Select(entry => $"{entry.Label}: {entry.Value}"),
                    string.Empty,
                    "已完成当前核心文件备份。",
                    "真实的联网重置安装流仍在后续 launch/install cutover 范围内。"
                ]);
            OpenInstanceTarget("重置实例", reportPath, "重置计划不存在。");
        }
        catch (Exception ex)
        {
            AddActivity("重置实例失败", ex.Message);
        }
    }

    private async Task DeleteInstanceAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("删除实例", "当前未选择实例。");
            return;
        }

        var instanceName = _instanceComposition.Selection.InstanceName;
        var confirmed = await _shellActionService.ConfirmAsync(
            "实例删除确认",
            $"确定要将实例 {instanceName} 移入 replacement shell 的回收区吗？该操作会保留实例目录，便于后续人工恢复。",
            "移入回收区",
            isDanger: true);
        if (!confirmed)
        {
            AddActivity("删除实例", "已取消删除。");
            return;
        }

        try
        {
            var sourceDirectory = _instanceComposition.Selection.InstanceDirectory;
            var trashDirectory = Path.Combine(_instanceComposition.Selection.LauncherDirectory, ".pcl-trash", "versions");
            Directory.CreateDirectory(trashDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var targetDirectory = GetUniquePath(Path.Combine(trashDirectory, $"{instanceName}-{timestamp}"));
            Directory.Move(sourceDirectory, targetDirectory);

            var metadataPath = Path.Combine(targetDirectory, ".pcl-trash.json");
            File.WriteAllText(
                metadataPath,
                JsonSerializer.Serialize(new
                {
                    instanceName,
                    originalPath = sourceDirectory,
                    deletedAt = DateTimeOffset.Now
                }, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));

            _shellActionService.PersistLocalValue("LaunchInstanceSelect", string.Empty);
            ReloadInstanceComposition();
            OpenInstanceTarget("删除实例", targetDirectory, "回收区目录不存在。");
        }
        catch (Exception ex)
        {
            AddActivity("删除实例失败", ex.Message);
        }
    }

    private async Task PatchInstanceCoreAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("修补核心", "当前未选择实例。");
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            "修补提示",
            $"确定要对 {_instanceComposition.Selection.InstanceName} 的核心文件进行修补吗？修补完成后会自动关闭文件校验，并先保存一份核心备份。",
            "选择修补文件");
        if (!confirmed)
        {
            AddActivity("修补核心", "已取消修补。");
            return;
        }

        string? patchPath;
        try
        {
            patchPath = await _shellActionService.PickOpenFileAsync("选择用于修补核心的文件", "压缩文件", "*.jar", "*.zip");
        }
        catch (Exception ex)
        {
            AddActivity("修补核心失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(patchPath))
        {
            AddActivity("修补核心", "已取消选择修补文件。");
            return;
        }

        try
        {
            BackupInstanceCoreFiles("patched-core-backups");
            var corePath = Path.Combine(
                _instanceComposition.Selection.InstanceDirectory,
                $"{_instanceComposition.Selection.InstanceName}.jar");
            PatchCoreArchive(corePath, patchPath);
            _shellActionService.PersistInstanceValue(_instanceComposition.Selection.InstanceDirectory, "VersionAdvanceAssetsV2", true);
            ReloadInstanceComposition();
            OpenInstanceTarget("修补核心", corePath, "修补后的核心文件不存在。");
        }
        catch (Exception ex)
        {
            AddActivity("修补核心失败", ex.Message);
        }
    }

    private string BackupInstanceCoreFiles(string folderName)
    {
        var backupDirectory = Path.Combine(
            GetInstanceOverviewArtifactDirectory(folderName),
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupDirectory);

        var manifestPath = Path.Combine(
            _instanceComposition.Selection.InstanceDirectory,
            $"{_instanceComposition.Selection.InstanceName}.json");
        var jarPath = Path.Combine(
            _instanceComposition.Selection.InstanceDirectory,
            $"{_instanceComposition.Selection.InstanceName}.jar");

        CopyFileIfExists(manifestPath, Path.Combine(backupDirectory, Path.GetFileName(manifestPath)));
        CopyFileIfExists(jarPath, Path.Combine(backupDirectory, Path.GetFileName(jarPath)));
        return backupDirectory;
    }

    private string WriteInstanceOverviewArtifact(string folderName, string fileName, IReadOnlyList<string> lines)
    {
        var directory = GetInstanceOverviewArtifactDirectory(folderName);
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, fileName);
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        return outputPath;
    }

    private string GetInstanceOverviewArtifactDirectory(string folderName)
    {
        return Path.Combine(
            _shellActionService.RuntimePaths.FrontendArtifactDirectory,
            "instance-overview",
            folderName,
            SanitizePathSegment(_instanceComposition.Selection.InstanceName));
    }

    private IReadOnlyList<string> BuildWindowsLaunchExportLines(int classpathEntries)
    {
        return
        [
            "@echo off",
            "echo PCL CE Frontend Script Export",
            $"echo Instance: {_instanceComposition.Selection.InstanceName}",
            $"echo Profile: {_launchComposition.SelectedProfile.IdentityLabel}",
            $"echo Java: {_launchComposition.SelectedJavaRuntime?.ExecutablePath ?? "Not configured"}",
            $"echo Game directory: {_launchComposition.InstancePath}",
            $"echo Natives directory: {_launchComposition.NativesDirectory}",
            $"echo Resolution: {_launchComposition.ResolutionPlan.Width}x{_launchComposition.ResolutionPlan.Height}",
            $"echo Classpath entries: {classpathEntries}",
            "echo.",
            "echo The replacement shell now exports the runtime launch context here.",
            "echo Full end-to-end launch cutover is still being migrated.",
            "pause"
        ];
    }

    private IReadOnlyList<string> BuildUnixLaunchExportLines(int classpathEntries)
    {
        return
        [
            "#!/bin/sh",
            "echo \"PCL CE Frontend Script Export\"",
            $"echo \"Instance: {_instanceComposition.Selection.InstanceName}\"",
            $"echo \"Profile: {_launchComposition.SelectedProfile.IdentityLabel}\"",
            $"echo \"Java: {_launchComposition.SelectedJavaRuntime?.ExecutablePath ?? "Not configured"}\"",
            $"echo \"Game directory: {_launchComposition.InstancePath}\"",
            $"echo \"Natives directory: {_launchComposition.NativesDirectory}\"",
            $"echo \"Resolution: {_launchComposition.ResolutionPlan.Width}x{_launchComposition.ResolutionPlan.Height}\"",
            $"echo \"Classpath entries: {classpathEntries}\"",
            "echo",
            "echo \"The replacement shell now exports the runtime launch context here.\"",
            "echo \"Full end-to-end launch cutover is still being migrated.\""
        ];
    }

    private static void ReplacePathText(string path, string oldValue, string newValue)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        if (!content.Contains(oldValue, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.WriteAllText(path, content.Replace(oldValue, newValue, StringComparison.OrdinalIgnoreCase), new UTF8Encoding(false));
    }

    private static void RewriteInstanceManifestId(string manifestPath, string instanceName)
    {
        if (!File.Exists(manifestPath))
        {
            return;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(manifestPath));
        }
        catch
        {
            return;
        }

        if (root is not JsonObject obj)
        {
            return;
        }

        obj["id"] = instanceName;
        File.WriteAllText(
            manifestPath,
            obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
    }

    private static void CopyFileIfExists(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    private static void RenamePathPreservingCase(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            var tempPath = $"{sourcePath}.rename-{Guid.NewGuid():N}.tmp";
            File.Move(sourcePath, tempPath, overwrite: true);
            File.Move(tempPath, targetPath, overwrite: true);
            return;
        }

        File.Move(sourcePath, targetPath, overwrite: true);
    }

    private static void MoveDirectoryPreservingCase(string sourcePath, string targetPath, bool treatMissingSourceAsSuccess = false)
    {
        if (!Directory.Exists(sourcePath))
        {
            if (treatMissingSourceAsSuccess)
            {
                return;
            }

            throw new DirectoryNotFoundException($"未找到目录：{sourcePath}");
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            var tempPath = $"{sourcePath}.rename-{Guid.NewGuid():N}";
            Directory.Move(sourcePath, tempPath);
            Directory.Move(tempPath, targetPath);
            return;
        }

        Directory.Move(sourcePath, targetPath);
    }

    private static string GetUniquePath(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return path;
        }

        var suffix = 1;
        while (true)
        {
            var candidate = $"{path}-{suffix}";
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (invalidCharacters.Contains(character))
            {
                continue;
            }

            builder.Append(character switch
            {
                ' ' => '-',
                '/' => '-',
                '\\' => '-',
                _ => character
            });
        }

        return builder.Length == 0 ? "instance" : builder.ToString();
    }

    private static void TryMarkFileExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Best effort on Unix-like systems.
        }
    }

    private static void PatchCoreArchive(string corePath, string patchArchivePath)
    {
        if (!File.Exists(corePath))
        {
            throw new FileNotFoundException($"未找到指定文件：{corePath}");
        }

        if (!File.Exists(patchArchivePath))
        {
            throw new FileNotFoundException($"未找到指定文件：{patchArchivePath}");
        }

        using var coreStream = new FileStream(corePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 16384, useAsync: true);
        using var patchStream = new FileStream(patchArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, useAsync: true);
        using var coreArchive = new ZipArchive(coreStream, ZipArchiveMode.Update);
        using var patchArchive = new ZipArchive(patchStream, ZipArchiveMode.Read);
        var filter = patchArchivePath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ? string.Empty : "MINECRAFT-JAR";

        foreach (var entry in patchArchive.Entries)
        {
            if (!entry.FullName.Contains(filter, StringComparison.Ordinal))
            {
                continue;
            }

            coreArchive.GetEntry(entry.FullName)?.Delete();
            var targetEntry = coreArchive.CreateEntry(entry.FullName);
            using var targetStream = targetEntry.Open();
            using var sourceStream = entry.Open();
            sourceStream.CopyTo(targetStream);
        }

        foreach (var signatureEntry in coreArchive.Entries
                     .Where(entry => entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            signatureEntry.Delete();
        }
    }
}
