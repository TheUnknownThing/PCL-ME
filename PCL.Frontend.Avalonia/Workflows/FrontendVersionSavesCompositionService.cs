using fNbt;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendVersionSavesCompositionService
{
    public static FrontendVersionSavesComposition Compose(
        FrontendInstanceComposition instanceComposition,
        string? selectedSavePath)
    {
        var selection = ResolveSelection(instanceComposition, selectedSavePath);
        if (!selection.HasSelection)
        {
            return new FrontendVersionSavesComposition(
                selection,
                [],
                [],
                [],
                []);
        }

        return new FrontendVersionSavesComposition(
            selection,
            BuildInfoEntries(selection),
            BuildSettingEntries(selection),
            BuildBackups(selection),
            BuildDatapacks(selection));
    }

    private static FrontendVersionSaveSelectionState ResolveSelection(
        FrontendInstanceComposition instanceComposition,
        string? selectedSavePath)
    {
        var savePath = ResolveSavePath(instanceComposition, selectedSavePath);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            return new FrontendVersionSaveSelectionState(
                false,
                instanceComposition.Selection.InstanceName,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        var savesRoot = Path.GetDirectoryName(savePath) ?? string.Empty;
        var backupDirectory = Path.Combine(savesRoot, ".PCLBackups", Path.GetFileName(savePath));
        return new FrontendVersionSaveSelectionState(
            true,
            instanceComposition.Selection.InstanceName,
            Path.GetFileName(savePath),
            savePath,
            Path.Combine(savePath, "datapacks"),
            backupDirectory);
    }

    private static string ResolveSavePath(
        FrontendInstanceComposition instanceComposition,
        string? selectedSavePath)
    {
        if (!string.IsNullOrWhiteSpace(selectedSavePath))
        {
            var matchedSavePath = instanceComposition.World.Entries
                .Select(entry => entry.Path)
                .FirstOrDefault(path =>
                    string.Equals(path, selectedSavePath, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(path));
            if (!string.IsNullOrWhiteSpace(matchedSavePath))
            {
                return matchedSavePath;
            }
        }

        return instanceComposition.World.Entries
            .Select(entry => entry.Path)
            .FirstOrDefault(path => Directory.Exists(path)) ?? string.Empty;
    }

    private static IReadOnlyList<FrontendVersionSaveInfoEntry> BuildInfoEntries(FrontendVersionSaveSelectionState selection)
    {
        var info = new List<FrontendVersionSaveInfoEntry>
        {
            new("实例", selection.InstanceName),
            new("存档名称", selection.SaveName),
            new("存档路径", selection.SavePath),
            new("最后修改", Directory.GetLastWriteTime(selection.SavePath).ToString("yyyy/MM/dd HH:mm")),
            new("文件大小", FormatDirectorySize(selection.SavePath)),
            new("文件总数", CountFiles(selection.SavePath).ToString()),
            new("数据包数量", BuildDatapacks(selection).Count.ToString()),
            new("备份数量", BuildBackups(selection).Count.ToString())
        };

        var levelDatPath = Path.Combine(selection.SavePath, "level.dat");
        info.Add(new FrontendVersionSaveInfoEntry("level.dat", File.Exists(levelDatPath) ? levelDatPath : "未找到"));
        info.Add(new FrontendVersionSaveInfoEntry("图标", File.Exists(Path.Combine(selection.SavePath, "icon.png")) ? "已提供" : "未提供"));
        return info;
    }

    private static IReadOnlyList<FrontendVersionSaveInfoEntry> BuildSettingEntries(FrontendVersionSaveSelectionState selection)
    {
        var levelDatPath = Path.Combine(selection.SavePath, "level.dat");
        if (!File.Exists(levelDatPath))
        {
            return [];
        }

        try
        {
            var file = new NbtFile(levelDatPath);
            var data = file.RootTag.Get<NbtCompound>("Data");
            if (data is null)
            {
                return [];
            }

            var settings = new List<FrontendVersionSaveInfoEntry>();
            AddIfNotEmpty(settings, "关卡名称", data.Get<NbtString>("LevelName")?.Value);
            AddIfNotEmpty(settings, "游戏模式", MapGameType(data.Get<NbtInt>("GameType")?.Value));
            AddIfNotEmpty(settings, "难度", MapDifficulty(data.Get<NbtByte>("Difficulty")?.Value));
            AddIfNotEmpty(settings, "允许作弊", MapBool(data.Get<NbtByte>("allowCommands")?.Value));
            AddIfNotEmpty(settings, "极限模式", MapBool(data.Get<NbtByte>("hardcore")?.Value));
            AddIfNotEmpty(settings, "下雨中", MapBool(data.Get<NbtByte>("raining")?.Value));
            AddIfNotEmpty(settings, "雷暴中", MapBool(data.Get<NbtByte>("thundering")?.Value));
            AddIfNotEmpty(settings, "游戏天数", data.Get<NbtLong>("DayTime")?.Value is { } dayTime ? (dayTime / 24000L).ToString() : null);

            var version = data.Get<NbtCompound>("Version");
            if (version is not null)
            {
                AddIfNotEmpty(settings, "存档版本", version.Get<NbtString>("Name")?.Value);
                AddIfNotEmpty(settings, "存档格式", version.Get<NbtInt>("Id")?.Value.ToString());
            }

            var player = data.Get<NbtCompound>("Player");
            if (player is not null)
            {
                AddIfNotEmpty(settings, "玩家维度", player.Get<NbtString>("Dimension")?.Value);
            }

            return settings;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<FrontendVersionSaveBackupEntry> BuildBackups(FrontendVersionSaveSelectionState selection)
    {
        var candidates = GetBackupDirectories(selection);
        return candidates
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.zip", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new FrontendVersionSaveBackupEntry(
                Path.GetFileNameWithoutExtension(file.Name),
                $"{file.LastWriteTime:yyyy/MM/dd HH:mm} • {FormatFileSize(file.Length)}",
                file.FullName))
            .ToArray();
    }

    private static IReadOnlyList<FrontendVersionSaveDatapackEntry> BuildDatapacks(FrontendVersionSaveSelectionState selection)
    {
        if (!Directory.Exists(selection.DatapackDirectory))
        {
            return [];
        }

        var archives = Directory.EnumerateFiles(selection.DatapackDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Select(file => new FrontendVersionSaveDatapackEntry(
                Path.GetFileNameWithoutExtension(file.Name),
                $"{file.LastWriteTime:yyyy/MM/dd HH:mm} • {FormatFileSize(file.Length)}",
                "数据包 • 压缩包",
                file.FullName,
                "CommandBlock.png"));
        var folders = Directory.EnumerateDirectories(selection.DatapackDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .Where(folder => folder.EnumerateFileSystemInfos().Any())
            .Select(folder => new FrontendVersionSaveDatapackEntry(
                folder.Name,
                $"{folder.LastWriteTime:yyyy/MM/dd HH:mm} • 文件夹",
                "数据包 • 文件夹",
                folder.FullName,
                "Grass.png"));
        return archives.Concat(folders)
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ResolveBackupDirectory(FrontendVersionSaveSelectionState selection)
    {
        return GetBackupDirectories(selection).FirstOrDefault() ?? selection.BackupDirectory;
    }

    private static IReadOnlyList<string> GetBackupDirectories(FrontendVersionSaveSelectionState selection)
    {
        var saveRoot = Path.GetDirectoryName(selection.SavePath) ?? string.Empty;
        return
        [
            selection.BackupDirectory,
            Path.Combine(selection.SavePath, "PCL", "Backups"),
            Path.Combine(saveRoot, "PCL.Backups", selection.SaveName)
        ];
    }

    private static void AddIfNotEmpty(List<FrontendVersionSaveInfoEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new FrontendVersionSaveInfoEntry(label, value));
        }
    }

    private static string MapGameType(int? value)
    {
        return value switch
        {
            0 => "生存",
            1 => "创造",
            2 => "冒险",
            3 => "旁观",
            _ => "未知"
        };
    }

    private static string MapDifficulty(byte? value)
    {
        return value switch
        {
            0 => "和平",
            1 => "简单",
            2 => "普通",
            3 => "困难",
            _ => "未知"
        };
    }

    private static string MapBool(byte? value)
    {
        return value switch
        {
            0 => "否",
            1 => "是",
            _ => "未知"
        };
    }

    private static long CountFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).LongCount();
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatDirectorySize(string directory)
    {
        try
        {
            var size = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .Sum(file => file.Length);
            return FormatFileSize(size);
        }
        catch
        {
            return "未知";
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
