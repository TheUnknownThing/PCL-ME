using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string InstanceExportConfigSeparator = "==============================================================";
    private const string InstanceExportConfigCacheKey = "CacheExportConfig";
    private string _instanceExportName = "Modern Fabric Demo";
    private string _instanceExportVersion = "1.0.0";
    private bool _instanceExportIncludeResources;
    private bool _instanceExportModrinthMode;

    public ObservableCollection<ExportOptionGroupViewModel> InstanceExportOptionGroups { get; } = [];

    public string InstanceExportName
    {
        get => _instanceExportName;
        set => SetProperty(ref _instanceExportName, value);
    }

    public string InstanceExportVersion
    {
        get => _instanceExportVersion;
        set => SetProperty(ref _instanceExportVersion, value);
    }

    public bool InstanceExportIncludeResources
    {
        get => _instanceExportIncludeResources;
        set
        {
            if (SetProperty(ref _instanceExportIncludeResources, value))
            {
                RaisePropertyChanged(nameof(ShowInstanceExportIncludeWarning));
            }
        }
    }

    public bool InstanceExportModrinthMode
    {
        get => _instanceExportModrinthMode;
        set
        {
            if (SetProperty(ref _instanceExportModrinthMode, value))
            {
                RaisePropertyChanged(nameof(ShowInstanceExportOptiFineWarning));
            }
        }
    }

    public bool ShowInstanceExportIncludeWarning => InstanceExportIncludeResources;

    public bool ShowInstanceExportOptiFineWarning => InstanceExportModrinthMode;

    public bool HasInstanceExportOptionGroups => InstanceExportOptionGroups.Count > 0;

    private void InitializeInstanceExportSurface()
    {
        var exportState = _instanceComposition.Export;
        _instanceExportName = exportState.Name;
        _instanceExportVersion = exportState.Version;
        _instanceExportIncludeResources = exportState.IncludeResources;
        _instanceExportModrinthMode = exportState.ModrinthMode;

        ReplaceItems(
            InstanceExportOptionGroups,
            exportState.OptionGroups.Select(group => CreateExportOptionGroup(
                group.Title,
                group.Description,
                group.IsChecked,
                group.Children.Select(child => CreateExportOption(child.Title, child.Description, child.IsChecked)).ToArray())));
    }

    private void RefreshInstanceExportSurface()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceExport))
        {
            return;
        }

        RaisePropertyChanged(nameof(InstanceExportName));
        RaisePropertyChanged(nameof(InstanceExportVersion));
        RaisePropertyChanged(nameof(InstanceExportIncludeResources));
        RaisePropertyChanged(nameof(InstanceExportModrinthMode));
        RaisePropertyChanged(nameof(ShowInstanceExportIncludeWarning));
        RaisePropertyChanged(nameof(ShowInstanceExportOptiFineWarning));
        RaisePropertyChanged(nameof(HasInstanceExportOptionGroups));
    }

    private void ResetInstanceExportOptions()
    {
        ReloadInstanceComposition();
        RefreshInstanceExportSurface();
        AddActivity("重置导出选项", "实例导出页已恢复到当前实例扫描结果。");
    }

    private async Task ImportInstanceExportConfigAsync()
    {
        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync("选择整合包导出配置", "整合包导出配置", "*.txt");
        }
        catch (Exception ex)
        {
            AddFailureActivity("读取配置失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("读取配置", "已取消选择配置文件。");
            return;
        }

        string content;
        try
        {
            content = File.ReadAllText(sourcePath);
        }
        catch (Exception ex)
        {
            AddFailureActivity("读取配置失败", ex.Message);
            return;
        }

        ApplyInstanceExportConfig(content);
        AddActivity("读取配置", sourcePath);
    }

    private void SaveInstanceExportConfig() => _ = SaveInstanceExportConfigAsync();

    private async Task SaveInstanceExportConfigAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("保存配置", "当前未选择实例。");
            return;
        }

        var lines = new List<string>
        {
            $"Name:{InstanceExportName}",
            $"Version:{InstanceExportVersion}",
            $"IncludeResources:{InstanceExportIncludeResources}",
            $"ModrinthMode:{InstanceExportModrinthMode}",
            InstanceExportConfigSeparator
        };

        foreach (var group in InstanceExportOptionGroups)
        {
            lines.Add($"Group:{group.Header.Title}|{group.Header.IsChecked}");
            foreach (var child in group.Children)
            {
                lines.Add($"Item:{group.Header.Title}|{child.Title}|{child.IsChecked}");
            }
        }

        string? suggestedStartFolder = null;
        try
        {
            var provider = new JsonFileProvider(_shellActionService.RuntimePaths.SharedConfigPath);
            if (provider.Exists(InstanceExportConfigCacheKey))
            {
                var cachedPath = provider.Get<string>(InstanceExportConfigCacheKey);
                suggestedStartFolder = Path.GetDirectoryName(cachedPath);
            }
        }
        catch
        {
            // Ignore invalid cached save locations and fall back to the system default picker directory.
        }

        string? outputPath;
        try
        {
            outputPath = await _shellActionService.PickSaveFileAsync(
                "选择文件位置",
                "export_config.txt",
                "整合包导出配置",
                suggestedStartFolder,
                "*.txt");
        }
        catch (Exception ex)
        {
            AddFailureActivity("保存配置失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
            _shellActionService.PersistSharedValue(InstanceExportConfigCacheKey, outputPath);
            if (_shellActionService.TryRevealExternalTarget(outputPath, out var error))
            {
                AddActivity("保存配置", outputPath);
                return;
            }

            AddFailureActivity("保存配置失败", error ?? outputPath);
        }
        catch (Exception ex)
        {
            AddFailureActivity("保存配置失败", ex.Message);
        }
    }

    private void OpenInstanceExportGuide() => _ = OpenInstanceExportGuideAsync();

    private async Task OpenInstanceExportGuideAsync()
    {
        if (TryResolveHelpEntry("指南/整合包制作.json", out var guideEntry))
        {
            ShowHelpDetail(guideEntry, addActivity: true);
            return;
        }

        var lines = new[]
        {
            "整合包制作指南",
            string.Empty,
            "1. 先在实例导出页确认名称、版本号和勾选内容。",
            "2. 如果需要复用同一套导出规则，先点击“保存配置”，后续可直接“读取配置”。",
            "3. “打包资源文件”会直接把 Mod、资源包或光影包放进导出包，适合离线分发。",
            "4. “Modrinth 上传模式”会输出 .mrpack 文件，便于继续整理发布。",
            "5. 导出完成后，请检查生成的压缩包结构和内容是否符合当前实例需求。"
        };
        var result = await ShowToolboxConfirmationAsync(
            "整合包制作指南",
            string.Join(Environment.NewLine, lines.Skip(2)));
        if (result is null)
        {
            return;
        }

        AddActivity("整合包制作指南", "已显示制作指南。");
    }

    private void StartInstanceExport()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("开始导出", "当前未选择实例。");
            return;
        }

        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-exports");
        Directory.CreateDirectory(exportDirectory);
        var archiveName = $"{GetEffectiveInstanceExportName()} {GetEffectiveInstanceExportVersion()}{(InstanceExportModrinthMode ? ".mrpack" : ".zip")}";
        var archivePath = Path.Combine(exportDirectory, archiveName);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        var sources = CollectInstanceExportSources()
            .GroupBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var source in sources)
            {
                AddInstanceExportSourceToArchive(archive, source.SourcePath, source.ArchivePath);
            }
        }

        OpenInstanceTarget("开始导出", archivePath, "导出压缩包不存在。");
    }

    private static ExportOptionEntryViewModel CreateExportOption(string title, string description, bool isChecked)
    {
        return new ExportOptionEntryViewModel(title, description, isChecked);
    }

    private static ExportOptionGroupViewModel CreateExportOptionGroup(
        string title,
        string description,
        bool isChecked,
        IReadOnlyList<ExportOptionEntryViewModel> children)
    {
        return new ExportOptionGroupViewModel(
            new ExportOptionEntryViewModel(title, description, isChecked),
            children);
    }

    private void ApplyInstanceExportConfig(string content)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)
                || string.Equals(line, InstanceExportConfigSeparator, StringComparison.Ordinal)
                || line.StartsWith('#'))
            {
                continue;
            }

            if (TryParseConfigAssignment(line, "Name", out var name))
            {
                InstanceExportName = name;
                continue;
            }

            if (TryParseConfigAssignment(line, "Version", out var version))
            {
                InstanceExportVersion = version;
                continue;
            }

            if (TryParseConfigAssignment(line, "IncludeResources", out var includeResources)
                && bool.TryParse(includeResources, out var includeResourcesValue))
            {
                InstanceExportIncludeResources = includeResourcesValue;
                continue;
            }

            if (TryParseConfigAssignment(line, "ModrinthMode", out var modrinthMode)
                && bool.TryParse(modrinthMode, out var modrinthModeValue))
            {
                InstanceExportModrinthMode = modrinthModeValue;
                continue;
            }

            if (line.StartsWith("Group:", StringComparison.Ordinal))
            {
                var groupParts = line["Group:".Length..].Split('|', 2, StringSplitOptions.TrimEntries);
                if (groupParts.Length == 2
                    && bool.TryParse(groupParts[1], out var isChecked)
                    && TryFindInstanceExportGroup(groupParts[0], out var group))
                {
                    group.Header.IsChecked = isChecked;
                }

                continue;
            }

            if (!line.StartsWith("Item:", StringComparison.Ordinal))
            {
                continue;
            }

            var itemParts = line["Item:".Length..].Split('|', 3, StringSplitOptions.TrimEntries);
            if (itemParts.Length == 3
                && bool.TryParse(itemParts[2], out var isCheckedValue)
                && TryFindInstanceExportGroup(itemParts[0], out var selectedGroup))
            {
                var child = selectedGroup.Children.FirstOrDefault(entry => string.Equals(entry.Title, itemParts[1], StringComparison.OrdinalIgnoreCase));
                if (child is not null)
                {
                    child.IsChecked = isCheckedValue;
                }
            }
        }
    }

    private IEnumerable<(string SourcePath, string ArchivePath)> CollectInstanceExportSources()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            yield break;
        }

        var indieDirectory = _instanceComposition.Selection.IndieDirectory;
        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;

        foreach (var group in InstanceExportOptionGroups.Where(entry => entry.Header.IsChecked))
        {
            switch (group.Header.Title)
            {
                case "游戏本体":
                    foreach (var child in group.Children.Where(entry => entry.IsChecked))
                    {
                        var fileName = child.Title switch
                        {
                            "游戏本体设置" => "options.txt",
                            "游戏本体个人信息" => "optionsof.txt",
                            "OptiFine 设置" => "optionsof.txt",
                            _ => null
                        };
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            continue;
                        }

                        var sourcePath = Path.Combine(indieDirectory, fileName);
                        if (File.Exists(sourcePath))
                        {
                            yield return (sourcePath, Path.Combine("overrides", fileName));
                        }
                    }
                    break;
                case "Mod":
                    foreach (var child in group.Children.Where(entry => entry.IsChecked))
                    {
                        switch (child.Title)
                        {
                            case "已禁用的 Mod":
                                foreach (var entry in _instanceComposition.DisabledMods.Entries)
                                {
                                    yield return (entry.Path, BuildOverrideArchivePath(indieDirectory, entry.Path));
                                }
                                break;
                            case "整合包重要数据":
                            case "Mod 设置":
                                var configDirectory = Path.Combine(indieDirectory, "config");
                                if (Directory.Exists(configDirectory))
                                {
                                    yield return (configDirectory, BuildOverrideArchivePath(indieDirectory, configDirectory));
                                }
                                break;
                        }
                    }
                    break;
                case "资源包":
                    foreach (var source in ResolveCheckedExportEntries(group, _instanceComposition.ResourcePacks.Entries))
                    {
                        yield return source;
                    }
                    break;
                case "光影包":
                    foreach (var source in ResolveCheckedExportEntries(group, _instanceComposition.Shaders.Entries))
                    {
                        yield return source;
                    }
                    break;
                case "截图":
                    foreach (var entry in _instanceComposition.Screenshot.Entries)
                    {
                        yield return (entry.Path, BuildOverrideArchivePath(indieDirectory, entry.Path));
                    }
                    break;
                case "导出的结构":
                    foreach (var source in ResolveCheckedExportEntries(group, _instanceComposition.Schematics.Entries))
                    {
                        yield return source;
                    }
                    break;
                case "录像回放":
                    foreach (var child in group.Children.Where(entry => entry.IsChecked))
                    {
                        var sourcePath = Path.Combine(indieDirectory, "replay_recordings", child.Title);
                        if (File.Exists(sourcePath))
                        {
                            yield return (sourcePath, BuildOverrideArchivePath(indieDirectory, sourcePath));
                        }
                    }
                    break;
                case "单人游戏存档":
                    foreach (var child in group.Children.Where(entry => entry.IsChecked))
                    {
                        var world = _instanceComposition.World.Entries.FirstOrDefault(entry => string.Equals(entry.Title, child.Title, StringComparison.OrdinalIgnoreCase));
                        if (world is not null)
                        {
                            yield return (world.Path, BuildOverrideArchivePath(indieDirectory, world.Path));
                        }
                    }
                    break;
                case "多人游戏服务器列表":
                    var serversPath = Path.Combine(indieDirectory, "servers.dat");
                    if (File.Exists(serversPath))
                    {
                        yield return (serversPath, BuildOverrideArchivePath(indieDirectory, serversPath));
                    }
                    break;
                case "PCL 启动器程序":
                    if (group.Children.Any(child => child.IsChecked))
                    {
                        var pclDirectory = Path.Combine(instanceDirectory, "PCL");
                        if (Directory.Exists(pclDirectory))
                        {
                            yield return (pclDirectory, Path.Combine("overrides", "PCL"));
                        }
                    }
                    break;
            }
        }

        if (InstanceExportIncludeResources)
        {
            foreach (var entry in _instanceComposition.Mods.Entries.Concat(_instanceComposition.ResourcePacks.Entries).Concat(_instanceComposition.Shaders.Entries))
            {
                yield return (entry.Path, BuildOverrideArchivePath(indieDirectory, entry.Path));
            }
        }
    }

    private IEnumerable<(string SourcePath, string ArchivePath)> ResolveCheckedExportEntries(
        ExportOptionGroupViewModel group,
        IEnumerable<FrontendInstanceResourceEntry> entries)
    {
        foreach (var child in group.Children.Where(entry => entry.IsChecked))
        {
            var source = entries.FirstOrDefault(entry => string.Equals(entry.Title, child.Title, StringComparison.OrdinalIgnoreCase));
            if (source is not null)
            {
                yield return (source.Path, BuildOverrideArchivePath(_instanceComposition.Selection.IndieDirectory, source.Path));
            }
        }
    }

    private static string BuildOverrideArchivePath(string indieDirectory, string sourcePath)
    {
        return Path.Combine("overrides", Path.GetRelativePath(indieDirectory, sourcePath));
    }

    private static void AddInstanceExportSourceToArchive(ZipArchive archive, string sourcePath, string archivePath)
    {
        if (File.Exists(sourcePath))
        {
            archive.CreateEntryFromFile(sourcePath, NormalizeArchivePath(archivePath));
            return;
        }

        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            archive.CreateEntryFromFile(file, NormalizeArchivePath(Path.Combine(archivePath, relativePath)));
        }
    }

    private static string NormalizeArchivePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private bool TryFindInstanceExportGroup(string title, out ExportOptionGroupViewModel group)
    {
        group = InstanceExportOptionGroups.FirstOrDefault(entry => string.Equals(entry.Header.Title, title, StringComparison.OrdinalIgnoreCase))!;
        return group is not null;
    }

    private static bool TryParseConfigAssignment(string line, string key, out string value)
    {
        if (line.StartsWith($"{key}:", StringComparison.Ordinal))
        {
            value = line[(key.Length + 1)..].Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private string GetEffectiveInstanceExportName()
    {
        return string.IsNullOrWhiteSpace(InstanceExportName)
            ? _instanceComposition.Selection.InstanceName
            : InstanceExportName;
    }

    private string GetEffectiveInstanceExportVersion()
    {
        return string.IsNullOrWhiteSpace(InstanceExportVersion)
            ? "1.0.0"
            : InstanceExportVersion;
    }
}
