using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using fNbt;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.ViewModels.ShellPanes;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _instanceWorldSearchQuery = string.Empty;
    private string _instanceServerSearchQuery = string.Empty;
    private string _instanceResourceSearchQuery = string.Empty;
    private string _instanceResourceSurfaceTitle = "Mod";

    public string InstanceWorldSearchQuery
    {
        get => _instanceWorldSearchQuery;
        set
        {
            if (SetProperty(ref _instanceWorldSearchQuery, value))
            {
                RefreshInstanceWorldEntries();
            }
        }
    }

    public string InstanceServerSearchQuery
    {
        get => _instanceServerSearchQuery;
        set
        {
            if (SetProperty(ref _instanceServerSearchQuery, value))
            {
                RefreshInstanceServerEntries();
            }
        }
    }

    public string InstanceResourceSearchQuery
    {
        get => _instanceResourceSearchQuery;
        set
        {
            if (SetProperty(ref _instanceResourceSearchQuery, value))
            {
                RefreshInstanceResourceEntries();
            }
        }
    }

    public string InstanceResourceSurfaceTitle
    {
        get => _instanceResourceSurfaceTitle;
        private set => SetProperty(ref _instanceResourceSurfaceTitle, value);
    }

    public bool HasInstanceWorldEntries => InstanceWorldEntries.Count > 0;

    public bool HasNoInstanceWorldEntries => !HasInstanceWorldEntries;

    public bool HasInstanceScreenshotEntries => InstanceScreenshotEntries.Count > 0;

    public bool HasNoInstanceScreenshotEntries => !HasInstanceScreenshotEntries;

    public bool HasInstanceServerEntries => InstanceServerEntries.Count > 0;

    public bool HasNoInstanceServerEntries => !HasInstanceServerEntries;

    public bool HasInstanceResourceEntries => InstanceResourceEntries.Count > 0;

    public bool HasNoInstanceResourceEntries => !HasInstanceResourceEntries;

    public bool ShowInstanceResourceUnsupportedState => IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceResource)
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionSchematic
        && HasNoInstanceResourceEntries;

    public bool ShowInstanceResourceEmptyInstallActions => !ShowInstanceResourceUnsupportedState;

    public bool ShowInstanceResourceInstanceSelectAction => ShowInstanceResourceUnsupportedState;

    public string InstanceResourceSearchWatermark => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionResourcePack => "搜索资源 名称 / 描述 / 标签",
        LauncherFrontendSubpageKey.VersionShader => "搜索光影 名称 / 描述 / 标签",
        LauncherFrontendSubpageKey.VersionSchematic => "搜索投影 名称 / 描述 / 标签",
        _ => "搜索资源 名称 / 描述 / 标签"
    };

    public string InstanceResourceDownloadButtonText => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionResourcePack => "下载新资源",
        LauncherFrontendSubpageKey.VersionShader => "下载新资源",
        LauncherFrontendSubpageKey.VersionSchematic => "下载投影 Mod",
        _ => "下载新资源"
    };

    public string InstanceResourceEmptyTitle => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionModDisabled => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionResourcePack => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionShader => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionSchematic => "该实例不可用投影原理图",
        _ => "尚未安装资源"
    };

    public string InstanceResourceEmptyDescription => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionSchematic => "你可能需要先安装投影 Mod，如果已经安装过了投影 Mod 请先启动一次游戏。也可能是你选择错了实例，请点击实例选择按钮切换实例。",
        _ => "你可以从已经下载好的文件安装资源。如果你已经安装了资源，可能是版本隔离设置有误，请在设置中调整版本隔离选项。"
    };

    public bool ShowInstanceResourceCheckButton => _currentRoute.Subpage is LauncherFrontendSubpageKey.VersionMod
        or LauncherFrontendSubpageKey.VersionModDisabled;

    public ActionCommand OpenInstanceWorldFolderCommand => new(() =>
        OpenInstanceTarget(
            "打开存档文件夹",
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves") : string.Empty,
            "当前实例没有存档目录。"));

    public ActionCommand PasteInstanceWorldClipboardCommand => new(() => _ = PasteInstanceWorldClipboardAsync());

    public ActionCommand OpenInstanceScreenshotFolderCommand => new(() =>
        OpenInstanceTarget(
            "打开截图文件夹",
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "screenshots") : string.Empty,
            "当前实例没有截图目录。"));

    public ActionCommand RefreshInstanceServerCommand => new(() =>
    {
        ReloadInstanceComposition();
        AddActivity("刷新服务器信息", "已重新扫描当前实例中的服务器列表。");
    });

    public ActionCommand AddInstanceServerCommand => new(() => _ = AddInstanceServerFromClipboardAsync());

    public ActionCommand OpenInstanceResourceFolderCommand => new(() =>
        OpenInstanceTarget("打开资源文件夹", GetCurrentInstanceResourceDirectory(), "当前实例没有对应的资源目录。"));

    public ActionCommand InstallInstanceResourceFromFileCommand => new(() => _ = InstallInstanceResourceFromFileAsync());

    public ActionCommand DownloadInstanceResourceCommand => new(DownloadInstanceResource);

    public ActionCommand SelectAllInstanceResourcesCommand => new(() =>
        AddActivity("全选资源", $"{InstanceResourceSurfaceTitle} • Would toggle select-all for the current list."));

    public ActionCommand ExportInstanceResourceInfoCommand => new(ExportInstanceResourceInfo);

    public ActionCommand CheckInstanceModsCommand => new(CheckInstanceMods);

    private void InitializeInstanceContentSurfaces()
    {
        _instanceWorldSearchQuery = string.Empty;
        _instanceServerSearchQuery = string.Empty;
        _instanceResourceSearchQuery = string.Empty;
        _instanceResourceSurfaceTitle = ResolveInstanceResourceSurfaceTitle();

        RefreshInstanceWorldEntries();
        RefreshInstanceScreenshotEntries();
        RefreshInstanceServerEntries();
        RefreshInstanceResourceEntries();
    }

    private void RefreshInstanceContentSurfaces()
    {
        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceWorld))
        {
            RaisePropertyChanged(nameof(InstanceWorldSearchQuery));
            RaisePropertyChanged(nameof(HasInstanceWorldEntries));
            RaisePropertyChanged(nameof(HasNoInstanceWorldEntries));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceScreenshot))
        {
            RaisePropertyChanged(nameof(HasInstanceScreenshotEntries));
            RaisePropertyChanged(nameof(HasNoInstanceScreenshotEntries));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceServer))
        {
            RaisePropertyChanged(nameof(InstanceServerSearchQuery));
            RaisePropertyChanged(nameof(HasInstanceServerEntries));
            RaisePropertyChanged(nameof(HasNoInstanceServerEntries));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceResource))
        {
            RefreshInstanceResourceEntries();
            RaisePropertyChanged(nameof(InstanceResourceSearchQuery));
            RaisePropertyChanged(nameof(InstanceResourceSurfaceTitle));
            RaisePropertyChanged(nameof(InstanceResourceSearchWatermark));
            RaisePropertyChanged(nameof(InstanceResourceDownloadButtonText));
            RaisePropertyChanged(nameof(InstanceResourceEmptyTitle));
            RaisePropertyChanged(nameof(InstanceResourceEmptyDescription));
            RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
            RaisePropertyChanged(nameof(HasInstanceResourceEntries));
            RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
            RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
            RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
            RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
        }
    }

    private InstanceScreenshotEntryViewModel CreateInstanceScreenshotEntry(string title, string info, string path, Bitmap? image)
    {
        return new InstanceScreenshotEntryViewModel(
            image,
            title,
            info,
            new ActionCommand(() => OpenInstanceTarget("打开截图", path, "当前截图文件不存在。")));
    }

    private void RefreshInstanceResourceEntries()
    {
        InstanceResourceSurfaceTitle = ResolveInstanceResourceSurfaceTitle();
        ReplaceItems(
            InstanceResourceEntries,
            GetCurrentInstanceResourceState().Entries
                .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Meta, InstanceResourceSearchQuery))
                .Select(entry => CreateInstanceResourceEntry(entry.Title, entry.Summary, entry.Meta, entry.IconName, entry.Path)));
    }

    private InstanceResourceEntryViewModel CreateInstanceResourceEntry(string title, string info, string meta, string iconName, string path)
    {
        return new InstanceResourceEntryViewModel(
            LoadLauncherBitmap("Images", "Blocks", iconName),
            title,
            info,
            meta,
            new ActionCommand(() => OpenInstanceTarget("查看资源", path, $"{InstanceResourceSurfaceTitle} 项目不存在。")));
    }

    private void RefreshInstanceWorldEntries()
    {
        ReplaceItems(
            InstanceWorldEntries,
            _instanceComposition.World.Entries
                .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Path, InstanceWorldSearchQuery))
                .Select(entry => new SimpleListEntryViewModel(
                    entry.Title,
                    entry.Summary,
                    new ActionCommand(() => OpenVersionSaveDetails(entry.Path)))));
    }

    private void RefreshInstanceScreenshotEntries()
    {
        ReplaceItems(
            InstanceScreenshotEntries,
            _instanceComposition.Screenshot.Entries.Select(entry => CreateInstanceScreenshotEntry(
                entry.Title,
                entry.Summary,
                entry.Path,
                LoadInstanceBitmap(entry.Path, "Images", "Backgrounds", "server_bg.png"))));
    }

    private void RefreshInstanceServerEntries()
    {
        ReplaceItems(
            InstanceServerEntries,
            _instanceComposition.Server.Entries
                .Where(entry => MatchesSearch(entry.Title, entry.Address, entry.Status, InstanceServerSearchQuery))
                .Select(entry => new InstanceServerEntryViewModel(
                    entry.Title,
                    entry.Address,
                    entry.Status,
                    new ActionCommand(() => ViewInstanceServer(entry)))));
    }

    private async Task PasteInstanceWorldClipboardAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("粘贴剪贴板文件", "当前未选择实例。");
            return;
        }

        string? clipboardText;
        try
        {
            clipboardText = await _shellActionService.ReadClipboardTextAsync();
        }
        catch (Exception ex)
        {
            AddActivity("粘贴剪贴板文件失败", ex.Message);
            return;
        }

        var sourcePaths = ParseClipboardPaths(clipboardText)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourcePaths.Length == 0)
        {
            AddActivity("粘贴剪贴板文件", "剪贴板中没有可导入的文件或文件夹路径。");
            return;
        }

        var savesDirectory = Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves");
        Directory.CreateDirectory(savesDirectory);

        var importedTargets = new List<string>();
        foreach (var sourcePath in sourcePaths)
        {
            var targetPath = GetUniqueChildPath(savesDirectory, Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, targetPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(sourcePath, targetPath, overwrite: false);
            }

            importedTargets.Add(targetPath);
        }

        ReloadInstanceComposition();
        AddActivity("粘贴剪贴板文件", string.Join(Environment.NewLine, importedTargets));
    }

    private async Task AddInstanceServerFromClipboardAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("添加新服务器", "当前未选择实例。");
            return;
        }

        string? clipboardText;
        try
        {
            clipboardText = await _shellActionService.ReadClipboardTextAsync();
        }
        catch (Exception ex)
        {
            AddActivity("添加新服务器失败", ex.Message);
            return;
        }

        if (!TryParseClipboardServer(clipboardText, out var name, out var address))
        {
            AddActivity("添加新服务器", "请先复制服务器地址，或复制“名称|地址”后再试。");
            return;
        }

        var serversPath = Path.Combine(_instanceComposition.Selection.IndieDirectory, "servers.dat");
        var file = File.Exists(serversPath)
            ? new NbtFile(serversPath)
            : new NbtFile(new NbtCompound
            {
                new NbtList("servers", NbtTagType.Compound)
            });
        var serverList = file.RootTag.Get<NbtList>("servers")
            ?? new NbtList("servers", NbtTagType.Compound);
        if (serverList.ListType == NbtTagType.Unknown)
        {
            serverList.ListType = NbtTagType.Compound;
        }

        serverList.Add(new NbtCompound
        {
            new NbtString("name", name),
            new NbtString("ip", address)
        });

        if (file.RootTag.Get<NbtList>("servers") is null)
        {
            file.RootTag.Add(serverList);
        }

        try
        {
            file.SaveToFile(serversPath, NbtCompression.None);
        }
        catch
        {
            AddActivity("添加新服务器失败", "无法写入当前实例的服务器列表。");
            return;
        }

        ReloadInstanceComposition();
        AddActivity("添加新服务器", $"{name} • {address}");
    }

    private async Task InstallInstanceResourceFromFileAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("从文件安装资源", "当前未选择实例。");
            return;
        }

        var (typeName, patterns) = ResolveInstanceResourcePickerOptions();

        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync($"选择{InstanceResourceSurfaceTitle}文件", typeName, patterns);
        }
        catch (Exception ex)
        {
            AddActivity("从文件安装资源失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("从文件安装资源", "已取消选择资源文件。");
            return;
        }

        var targetDirectory = GetCurrentInstanceResourceDirectory();
        Directory.CreateDirectory(targetDirectory);
        var targetPath = GetUniqueChildPath(targetDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, targetPath, overwrite: false);

        ReloadInstanceComposition();
        AddActivity("从文件安装资源", $"{sourcePath} -> {targetPath}");
    }

    private void DownloadInstanceResource()
    {
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, ResolveInstanceDownloadSubpage()),
            $"{InstanceResourceSurfaceTitle} 页面已跳转到下载页。");
    }

    private void ExportInstanceResourceInfo()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("导出资源信息", "当前未选择实例。");
            return;
        }

        var entries = GetCurrentInstanceResourceState().Entries;
        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-resources");
        Directory.CreateDirectory(exportDirectory);
        var outputPath = Path.Combine(
            exportDirectory,
            $"{_instanceComposition.Selection.InstanceName}-{ResolveInstanceResourceExportSlug()}-info.txt");
        var lines = entries.Count == 0
            ? [$"{InstanceResourceSurfaceTitle} 列表为空。"]
            : entries.Select(entry => $"{entry.Title} | {entry.Meta} | {entry.Summary} | {entry.Path}").ToArray();
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        OpenInstanceTarget("导出资源信息", outputPath, "导出文件不存在。");
    }

    private void CheckInstanceMods()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("检查 Mod", "当前未选择实例。");
            return;
        }

        var enabledMods = _instanceComposition.Mods.Entries;
        var disabledMods = _instanceComposition.DisabledMods.Entries;
        var duplicateGroups = enabledMods
            .Concat(disabledMods)
            .GroupBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-mod-checks");
        Directory.CreateDirectory(exportDirectory);
        var outputPath = Path.Combine(exportDirectory, $"{_instanceComposition.Selection.InstanceName}-mod-check.txt");
        var lines = new List<string>
        {
            $"实例: {_instanceComposition.Selection.InstanceName}",
            $"启用 Mod: {enabledMods.Count}",
            $"已禁用 Mod: {disabledMods.Count}",
            $"重复名称: {duplicateGroups.Length}",
            string.Empty
        };

        if (duplicateGroups.Length == 0)
        {
            lines.Add("未检测到重复名称的 Mod 文件。");
        }
        else
        {
            lines.Add("重复名称的 Mod:");
            foreach (var group in duplicateGroups)
            {
                lines.Add($"- {group.Key}");
                foreach (var entry in group)
                {
                    lines.Add($"  {entry.Meta} | {entry.Summary} | {entry.Path}");
                }
            }
        }

        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        OpenInstanceTarget("检查 Mod", outputPath, "检查结果不存在。");
    }

    private void ViewInstanceServer(FrontendInstanceServerEntry entry)
    {
        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-servers");
        Directory.CreateDirectory(exportDirectory);
        var safeName = string.Concat(entry.Title.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var outputPath = Path.Combine(exportDirectory, $"{safeName}.txt");
        var lines = new[]
        {
            $"名称: {entry.Title}",
            $"地址: {entry.Address}",
            $"状态: {entry.Status}"
        };
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        OpenInstanceTarget("查看服务器", outputPath, "服务器详情文件不存在。");
    }

    private FrontendInstanceResourceState GetCurrentInstanceResourceState()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => _instanceComposition.Mods,
            LauncherFrontendSubpageKey.VersionModDisabled => _instanceComposition.DisabledMods,
            LauncherFrontendSubpageKey.VersionResourcePack => _instanceComposition.ResourcePacks,
            LauncherFrontendSubpageKey.VersionShader => _instanceComposition.Shaders,
            LauncherFrontendSubpageKey.VersionSchematic => _instanceComposition.Schematics,
            _ => _instanceComposition.Mods
        };
    }

    private string ResolveInstanceResourceSurfaceTitle()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "Mod",
            LauncherFrontendSubpageKey.VersionModDisabled => "已禁用 Mod",
            LauncherFrontendSubpageKey.VersionResourcePack => "资源包",
            LauncherFrontendSubpageKey.VersionShader => "光影包",
            LauncherFrontendSubpageKey.VersionSchematic => "投影原理图",
            _ => "资源"
        };
    }

    private string GetCurrentInstanceResourceDirectory()
    {
        var folderName = _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "mods"
        };

        return ResolveCurrentInstanceResourceDirectory(folderName);
    }

    private (string TypeName, string[] Patterns) ResolveInstanceResourcePickerOptions()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => ("资源包文件", ["*.zip", "*.rar"]),
            LauncherFrontendSubpageKey.VersionShader => ("光影文件", ["*.zip", "*.rar"]),
            LauncherFrontendSubpageKey.VersionSchematic => ("投影原理图文件", ["*.litematic", "*.schem", "*.schematic", "*.nbt"]),
            _ => ("Mod 文件", ["*.jar", "*.disabled", "*.old"])
        };
    }

    private LauncherFrontendSubpageKey ResolveInstanceDownloadSubpage()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => LauncherFrontendSubpageKey.DownloadResourcePack,
            LauncherFrontendSubpageKey.VersionShader => LauncherFrontendSubpageKey.DownloadShader,
            LauncherFrontendSubpageKey.VersionSchematic => LauncherFrontendSubpageKey.DownloadMod,
            _ => LauncherFrontendSubpageKey.DownloadMod
        };
    }

    private string ResolveInstanceResourceExportSlug()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "mods",
            LauncherFrontendSubpageKey.VersionModDisabled => "mods-disabled",
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "resources"
        };
    }

    private static IEnumerable<string> ParseClipboardPaths(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return [];
        }

        return clipboardText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Trim('"'))
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static bool TryParseClipboardServer(string? clipboardText, out string name, out string address)
    {
        name = "Minecraft服务器";
        address = string.Empty;

        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return false;
        }

        var lines = clipboardText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0)
        {
            return false;
        }

        if (lines.Length >= 2)
        {
            name = lines[0];
            address = lines[1];
            return true;
        }

        var singleLine = lines[0];
        var split = singleLine.Split('|', 2, StringSplitOptions.TrimEntries);
        if (split.Length == 2)
        {
            name = string.IsNullOrWhiteSpace(split[0]) ? "Minecraft服务器" : split[0];
            address = split[1];
            return !string.IsNullOrWhiteSpace(address);
        }

        address = singleLine;
        return true;
    }

    private static string GetUniqueChildPath(string directory, string fileOrFolderName)
    {
        var candidate = Path.Combine(directory, fileOrFolderName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileOrFolderName);
        var extension = Path.GetExtension(fileOrFolderName);
        var suffix = 1;
        while (true)
        {
            candidate = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Copy(file, targetPath, overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }

    private static bool MatchesSearch(params string[] values)
    {
        if (values.Length == 0)
        {
            return true;
        }

        var query = values[^1];
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return values
            .Take(values.Length - 1)
            .Any(value => !string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
