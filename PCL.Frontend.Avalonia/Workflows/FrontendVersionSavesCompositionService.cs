using fNbt;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendVersionSavesCompositionService
{
    private static class SaveDetailLabelIds
    {
        public const string Instance = "instance";
        public const string SaveName = "save_name";
        public const string SavePath = "save_path";
        public const string LastModified = "last_modified";
        public const string FileSize = "file_size";
        public const string FileCount = "file_count";
        public const string DatapackCount = "datapack_count";
        public const string BackupCount = "backup_count";
        public const string Icon = "icon";
        public const string LevelName = "level_name";
        public const string GameMode = "game_mode";
        public const string Difficulty = "difficulty";
        public const string AllowCommands = "allow_commands";
        public const string Hardcore = "hardcore";
        public const string Raining = "raining";
        public const string Thundering = "thundering";
        public const string DayCount = "day_count";
        public const string SaveVersion = "save_version";
        public const string SaveFormat = "save_format";
        public const string PlayerDimension = "player_dimension";
    }

    private static class SaveDetailValueIds
    {
        public const string NotFound = "not_found";
        public const string Provided = "provided";
        public const string NotProvided = "not_provided";
        public const string Survival = "survival";
        public const string Creative = "creative";
        public const string Adventure = "adventure";
        public const string Spectator = "spectator";
        public const string Peaceful = "peaceful";
        public const string Easy = "easy";
        public const string Normal = "normal";
        public const string Hard = "hard";
        public const string No = "no";
        public const string Yes = "yes";
        public const string Unknown = "unknown";
        public const string Folder = "folder";
        public const string Archive = "archive";
        public const string Datapack = "datapack";
    }

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
            new(SaveDetailLabelIds.Instance, selection.InstanceName),
            new(SaveDetailLabelIds.SaveName, selection.SaveName),
            new(SaveDetailLabelIds.SavePath, selection.SavePath),
            new(SaveDetailLabelIds.LastModified, Directory.GetLastWriteTime(selection.SavePath).ToString("yyyy/MM/dd HH:mm")),
            new(SaveDetailLabelIds.FileSize, FormatDirectorySize(selection.SavePath)),
            new(SaveDetailLabelIds.FileCount, CountFiles(selection.SavePath).ToString()),
            new(SaveDetailLabelIds.DatapackCount, BuildDatapacks(selection).Count.ToString()),
            new(SaveDetailLabelIds.BackupCount, BuildBackups(selection).Count.ToString())
        };

        var levelDatPath = Path.Combine(selection.SavePath, "level.dat");
        info.Add(new FrontendVersionSaveInfoEntry("level.dat", File.Exists(levelDatPath) ? levelDatPath : SaveDetailValueIds.NotFound));
        info.Add(new FrontendVersionSaveInfoEntry(
            SaveDetailLabelIds.Icon,
            File.Exists(Path.Combine(selection.SavePath, "icon.png")) ? SaveDetailValueIds.Provided : SaveDetailValueIds.NotProvided));
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
            AddIfNotEmpty(settings, SaveDetailLabelIds.LevelName, data.Get<NbtString>("LevelName")?.Value);
            AddIfNotEmpty(settings, SaveDetailLabelIds.GameMode, MapGameType(data.Get<NbtInt>("GameType")?.Value));
            AddIfNotEmpty(settings, SaveDetailLabelIds.Difficulty, MapDifficulty(data.Get<NbtByte>("Difficulty")?.Value));
            AddIfNotEmpty(settings, SaveDetailLabelIds.AllowCommands, MapBool(data.Get<NbtByte>("allowCommands")?.Value));
            AddIfNotEmpty(settings, SaveDetailLabelIds.Hardcore, MapBool(data.Get<NbtByte>("hardcore")?.Value));
            AddIfNotEmpty(settings, SaveDetailLabelIds.Raining, MapBool(data.Get<NbtByte>("raining")?.Value));
            AddIfNotEmpty(settings, SaveDetailLabelIds.Thundering, MapBool(data.Get<NbtByte>("thundering")?.Value));
            AddIfNotEmpty(settings, SaveDetailLabelIds.DayCount, data.Get<NbtLong>("DayTime")?.Value is { } dayTime ? (dayTime / 24000L).ToString() : null);

            var version = data.Get<NbtCompound>("Version");
            if (version is not null)
            {
                AddIfNotEmpty(settings, SaveDetailLabelIds.SaveVersion, version.Get<NbtString>("Name")?.Value);
                AddIfNotEmpty(settings, SaveDetailLabelIds.SaveFormat, version.Get<NbtInt>("Id")?.Value.ToString());
            }

            var player = data.Get<NbtCompound>("Player");
            if (player is not null)
            {
                AddIfNotEmpty(settings, SaveDetailLabelIds.PlayerDimension, player.Get<NbtString>("Dimension")?.Value);
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
                $"{SaveDetailValueIds.Datapack} • {SaveDetailValueIds.Archive}",
                file.FullName,
                "CommandBlock.png"));
        var folders = Directory.EnumerateDirectories(selection.DatapackDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .Where(folder => folder.EnumerateFileSystemInfos().Any())
            .Select(folder => new FrontendVersionSaveDatapackEntry(
                folder.Name,
                $"{folder.LastWriteTime:yyyy/MM/dd HH:mm} • {SaveDetailValueIds.Folder}",
                $"{SaveDetailValueIds.Datapack} • {SaveDetailValueIds.Folder}",
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
            0 => SaveDetailValueIds.Survival,
            1 => SaveDetailValueIds.Creative,
            2 => SaveDetailValueIds.Adventure,
            3 => SaveDetailValueIds.Spectator,
            _ => SaveDetailValueIds.Unknown
        };
    }

    private static string MapDifficulty(byte? value)
    {
        return value switch
        {
            0 => SaveDetailValueIds.Peaceful,
            1 => SaveDetailValueIds.Easy,
            2 => SaveDetailValueIds.Normal,
            3 => SaveDetailValueIds.Hard,
            _ => SaveDetailValueIds.Unknown
        };
    }

    private static string MapBool(byte? value)
    {
        return value switch
        {
            0 => SaveDetailValueIds.No,
            1 => SaveDetailValueIds.Yes,
            _ => SaveDetailValueIds.Unknown
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
            return SaveDetailValueIds.Unknown;
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
