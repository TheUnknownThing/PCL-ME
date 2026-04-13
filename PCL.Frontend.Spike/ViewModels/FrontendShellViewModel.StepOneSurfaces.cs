using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private readonly HashSet<TaskModel> _observedTaskModels = [];
    private string _instanceSelectionSearchQuery = string.Empty;
    private string _instanceSelectionLauncherDirectory = string.Empty;
    private int _instanceSelectionTotalCount;
    private int _taskManagerWaitingCount;
    private int _taskManagerRunningCount;
    private int _taskManagerFinishedCount;
    private int _taskManagerFailedCount;
    private int _gameLogRecentFileCount;
    private string _gameLogLatestUpdateLabel = "尚未发现日志文件";

    public ObservableCollection<InstanceSelectEntryViewModel> InstanceSelectionEntries { get; } = [];

    public ObservableCollection<TaskManagerEntryViewModel> TaskManagerEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> GameLogFileEntries { get; } = [];

    public ActionCommand RefreshInstanceSelectionCommand => _refreshInstanceSelectionCommand;

    public ActionCommand ClearInstanceSelectionSearchCommand => _clearInstanceSelectionSearchCommand;

    public ActionCommand RefreshTaskManagerCommand => _refreshTaskManagerCommand;

    public ActionCommand ClearFinishedTasksCommand => _clearFinishedTasksCommand;

    public ActionCommand RefreshGameLogCommand => _refreshGameLogCommand;

    public ActionCommand ClearGameLogCommand => _clearGameLogCommand;

    public string InstanceSelectionSearchQuery
    {
        get => _instanceSelectionSearchQuery;
        set
        {
            if (SetProperty(ref _instanceSelectionSearchQuery, value))
            {
                RefreshInstanceSelectionSurface();
            }
        }
    }

    public bool HasDedicatedGenericRouteSurface =>
        ShowInstanceSelectSurface
        || ShowTaskManagerSurface
        || ShowGameLogSurface
        || ShowCompDetailSurface
        || ShowHomePageMarketSurface
        || ShowHelpDetailSurface;

    public bool ShowGenericCompatibilitySurface => !HasDedicatedGenericRouteSurface;

    public bool ShowInstanceSelectSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.InstanceSelect;

    public bool HasInstanceSelectionEntries => InstanceSelectionEntries.Count > 0;

    public bool HasNoInstanceSelectionEntries => !HasInstanceSelectionEntries;

    public string InstanceSelectionLauncherDirectory => _instanceSelectionLauncherDirectory;

    public string InstanceSelectionResultSummary => HasInstanceSelectionEntries
        ? $"已显示 {InstanceSelectionEntries.Count} / {_instanceSelectionTotalCount} 个实例"
        : _instanceSelectionTotalCount == 0
            ? "当前启动目录下还没有可用实例"
            : "当前筛选条件没有匹配到任何实例";

    public bool ShowTaskManagerSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.TaskManager;

    public bool HasTaskManagerEntries => TaskManagerEntries.Count > 0;

    public bool HasNoTaskManagerEntries => !HasTaskManagerEntries;

    public int TaskManagerWaitingCount => _taskManagerWaitingCount;

    public int TaskManagerRunningCount => _taskManagerRunningCount;

    public int TaskManagerFinishedCount => _taskManagerFinishedCount;

    public int TaskManagerFailedCount => _taskManagerFailedCount;

    public string TaskManagerSummary => HasTaskManagerEntries
        ? $"运行中 {TaskManagerRunningCount} 项，等待中 {TaskManagerWaitingCount} 项"
        : "当前没有需要壳层跟踪的后台任务";

    public bool ShowGameLogSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.GameLog;

    public bool HasGameLogFiles => GameLogFileEntries.Count > 0;

    public bool HasNoGameLogFiles => !HasGameLogFiles;

    public bool ShowGameLogLiveOutput => _launchLogBuilder.Length > 0;

    public bool ShowGameLogEmptyState => !ShowGameLogLiveOutput && HasNoGameLogFiles;

    public int GameLogLiveLineCount => CountLaunchLogLines();

    public int GameLogRecentFileCount => _gameLogRecentFileCount;

    public string GameLogLatestUpdateLabel => _gameLogLatestUpdateLabel;

    private void InitializeStepOneSurfaces()
    {
        TaskCenter.Tasks.CollectionChanged += OnTaskCenterCollectionChanged;
        SyncTaskSubscriptions();
        RefreshDedicatedGenericRouteSurface();
    }

    private void RefreshDedicatedGenericRouteSurface()
    {
        RefreshInstanceSelectionSurface();
        RefreshTaskManagerSurface();
        RefreshGameLogSurface();
        RefreshCompDetailSurface();
        RefreshHomePageMarketSurface();
        RefreshHelpDetailSurface();
    }

    private void RefreshCurrentDedicatedGenericRouteSurface()
    {
        switch (_currentRoute.Page)
        {
            case LauncherFrontendPageKey.InstanceSelect:
                RefreshInstanceSelectionSurface();
                break;
            case LauncherFrontendPageKey.TaskManager:
                RefreshTaskManagerSurface();
                break;
            case LauncherFrontendPageKey.GameLog:
                RefreshGameLogSurface();
                break;
            case LauncherFrontendPageKey.CompDetail:
                RefreshCompDetailSurface();
                break;
            case LauncherFrontendPageKey.HomePageMarket:
                RefreshHomePageMarketSurface();
                break;
            case LauncherFrontendPageKey.HelpDetail:
                RefreshHelpDetailSurface();
                break;
        }
    }

    private bool TryBuildDedicatedGenericRouteMetadata(out DedicatedGenericRouteMetadata metadata)
    {
        switch (_currentRoute.Page)
        {
            case LauncherFrontendPageKey.InstanceSelect:
                metadata = new DedicatedGenericRouteMetadata(
                    "实例选择",
                    "从当前启动目录读取真实实例列表，并直接切换启动目标，而不是退回通用迁移面板。",
                    [
                        new LauncherFrontendPageFact("启动目录", string.IsNullOrWhiteSpace(_instanceSelectionLauncherDirectory) ? "未解析" : _instanceSelectionLauncherDirectory),
                        new LauncherFrontendPageFact("已选实例", _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : "未选择"),
                        new LauncherFrontendPageFact("结果数量", $"{InstanceSelectionEntries.Count} / {_instanceSelectionTotalCount}")
                    ]);
                return true;
            case LauncherFrontendPageKey.TaskManager:
                metadata = new DedicatedGenericRouteMetadata(
                    "任务中心",
                    "这里直接观察 TaskCenter 的实时任务状态，包括等待、执行、完成和失败，不再展示迁移占位摘要。",
                    [
                        new LauncherFrontendPageFact("等待中", TaskManagerWaitingCount.ToString()),
                        new LauncherFrontendPageFact("运行中", TaskManagerRunningCount.ToString()),
                        new LauncherFrontendPageFact("已结束", TaskManagerFinishedCount.ToString()),
                        new LauncherFrontendPageFact("失败", TaskManagerFailedCount.ToString())
                    ]);
                return true;
            case LauncherFrontendPageKey.GameLog:
                metadata = new DedicatedGenericRouteMetadata(
                    "实时日志",
                    "实时日志直接显示当前会话输出，并补充最近生成的启动脚本、原始输出和启动器日志文件。",
                    [
                        new LauncherFrontendPageFact("实时行数", GameLogLiveLineCount.ToString()),
                        new LauncherFrontendPageFact("最近文件", GameLogRecentFileCount.ToString()),
                        new LauncherFrontendPageFact("最新更新", GameLogLatestUpdateLabel)
                    ]);
                return true;
            case LauncherFrontendPageKey.CompDetail:
                metadata = new DedicatedGenericRouteMetadata(
                    "工程详情",
                    "资源工程详情页现在会直接请求实时社区元数据与最近版本，而不是落回通用下载迁移面板。",
                    [
                        new LauncherFrontendPageFact("来源", CommunityProjectSource),
                        new LauncherFrontendPageFact("状态", CommunityProjectStatus),
                        new LauncherFrontendPageFact("最近更新", CommunityProjectUpdatedLabel),
                        new LauncherFrontendPageFact("下载量", CommunityProjectDownloadCountLabel)
                    ]);
                return true;
            case LauncherFrontendPageKey.HomePageMarket:
                metadata = new DedicatedGenericRouteMetadata(
                    "主页市场",
                    "主页市场会聚合热门社区资源分区，直接展示当前可访问来源的实时榜单，而不是继续复用普通下载概览。",
                    [
                        new LauncherFrontendPageFact("聚合分区", HomePageMarketSections.Count.ToString()),
                        new LauncherFrontendPageFact("首选来源", _selectedCommunityDownloadSourceIndex == 0 ? "镜像优先" : "官方优先"),
                        new LauncherFrontendPageFact("当前实例", _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : "未选择实例"),
                        new LauncherFrontendPageFact("兼容版本", string.IsNullOrWhiteSpace(_instanceComposition.Selection.VanillaVersion) ? "未指定" : _instanceComposition.Selection.VanillaVersion)
                    ]);
                return true;
            case LauncherFrontendPageKey.HelpDetail:
                metadata = new DedicatedGenericRouteMetadata(
                    HelpDetailTitle,
                    "帮助详情会直接展示条目正文、内嵌动作和可追溯的来源信息，而不是只记录原始路径。",
                    [
                        new LauncherFrontendPageFact("来源", HelpDetailSource),
                        new LauncherFrontendPageFact("段落数", HelpDetailSections.Sum(section => section.Lines.Count).ToString()),
                        new LauncherFrontendPageFact("动作数", HelpDetailSections.Sum(section => section.Actions.Count).ToString())
                    ]);
                return true;
            default:
                metadata = null!;
                return false;
        }
    }

    private void RefreshInstanceSelectionSurface()
    {
        var runtimePaths = _shellActionService.RuntimePaths;
        var localConfig = new YamlFileProvider(runtimePaths.LocalConfigPath);
        var launcherDirectory = ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", "$.minecraft\\"),
            runtimePaths);
        var selectedInstance = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        _instanceSelectionLauncherDirectory = launcherDirectory;

        var allEntries = Directory.Exists(versionsDirectory)
            ? Directory.EnumerateDirectories(versionsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(directory => BuildInstanceSelectionSnapshot(directory, selectedInstance))
                .Where(snapshot => snapshot is not null)
                .Select(snapshot => snapshot!)
                .OrderByDescending(snapshot => snapshot.IsSelected)
                .ThenByDescending(snapshot => snapshot.IsStarred)
                .ThenBy(snapshot => snapshot.IsBroken)
                .ThenBy(snapshot => snapshot.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray()
            : [];
        _instanceSelectionTotalCount = allEntries.Length;

        var query = InstanceSelectionSearchQuery.Trim();
        var filteredEntries = string.IsNullOrWhiteSpace(query)
            ? allEntries
            : allEntries.Where(entry => MatchesInstanceSelectionQuery(entry, query)).ToArray();

        ReplaceItems(
            InstanceSelectionEntries,
            filteredEntries.Select(entry => CreateInstanceSelectionEntry(entry)));

        RaisePropertyChanged(nameof(InstanceSelectionLauncherDirectory));
        RaisePropertyChanged(nameof(HasInstanceSelectionEntries));
        RaisePropertyChanged(nameof(HasNoInstanceSelectionEntries));
        RaisePropertyChanged(nameof(InstanceSelectionResultSummary));
    }

    private void RefreshTaskManagerSurface()
    {
        SyncTaskSubscriptions();

        var tasks = TaskCenter.Tasks.ToArray();
        _taskManagerWaitingCount = tasks.Count(task => task.State == TaskState.Waiting);
        _taskManagerRunningCount = tasks.Count(task => task.State == TaskState.Running);
        _taskManagerFinishedCount = tasks.Count(task => task.State == TaskState.Success || task.State == TaskState.Canceled);
        _taskManagerFailedCount = tasks.Count(task => task.State == TaskState.Failed);

        ReplaceItems(
            TaskManagerEntries,
            tasks
                .OrderByDescending(task => task.State == TaskState.Running)
                .ThenByDescending(task => task.State == TaskState.Waiting)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(CreateTaskManagerEntry));

        RaisePropertyChanged(nameof(HasTaskManagerEntries));
        RaisePropertyChanged(nameof(HasNoTaskManagerEntries));
        RaisePropertyChanged(nameof(TaskManagerWaitingCount));
        RaisePropertyChanged(nameof(TaskManagerRunningCount));
        RaisePropertyChanged(nameof(TaskManagerFinishedCount));
        RaisePropertyChanged(nameof(TaskManagerFailedCount));
        RaisePropertyChanged(nameof(TaskManagerSummary));
    }

    private void RefreshGameLogSurface()
    {
        var runtimePaths = _shellActionService.RuntimePaths;
        var candidateFiles = new[]
        {
            Path.Combine(runtimePaths.LauncherAppDataDirectory, "LatestLaunch.bat"),
            Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "RawOutput.log"),
            Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "latest.log"),
            Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "PCL.log"),
            Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "LatestLaunch.bat"),
            Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "RawOutput.log"),
            Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "latest.log"),
            Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "PCL.log")
        };
        var recentFiles = candidateFiles
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log")))
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(10)
            .ToArray();

        _gameLogRecentFileCount = recentFiles.Length;
        _gameLogLatestUpdateLabel = recentFiles.FirstOrDefault() is { } latest
            ? latest.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
            : "尚未发现日志文件";

        ReplaceItems(
            GameLogFileEntries,
            recentFiles.Select(file =>
                new SimpleListEntryViewModel(
                    file.Name,
                    $"{file.DirectoryName} • {file.LastWriteTime:yyyy-MM-dd HH:mm}",
                    CreateOpenTargetCommand($"打开日志文件: {file.Name}", file.FullName, file.FullName))));

        RaiseGameLogSurfaceProperties();
        RaisePropertyChanged(nameof(HasGameLogFiles));
        RaisePropertyChanged(nameof(HasNoGameLogFiles));
    }

    private void SelectInstanceForLaunch(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return;
        }

        _shellActionService.PersistLocalValue("LaunchInstanceSelect", instanceName);
        RefreshLaunchState();
        RefreshShell($"已将启动实例切换为 {instanceName}。");
    }

    private void OpenInstanceSelectionEntry(InstanceSelectionSnapshot entry)
    {
        _shellActionService.PersistLocalValue("LaunchInstanceSelect", entry.Name);
        RefreshLaunchState();
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall),
            $"已打开实例 {entry.Name} 的概览。");
    }

    private void ClearGameLogSurface()
    {
        _launchLogBuilder.Clear();
        RaiseGameLogSurfaceProperties();
        AddActivity("清空实时日志", "仅清除了前端当前持有的会话输出缓存。");
    }

    private void RaiseGameLogSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowGameLogLiveOutput));
        RaisePropertyChanged(nameof(ShowGameLogEmptyState));
        RaisePropertyChanged(nameof(GameLogLiveLineCount));
        RaisePropertyChanged(nameof(GameLogRecentFileCount));
        RaisePropertyChanged(nameof(GameLogLatestUpdateLabel));
        RaisePropertyChanged(nameof(LaunchLogText));
    }

    private int CountLaunchLogLines()
    {
        if (_launchLogBuilder.Length == 0)
        {
            return 0;
        }

        var lineCount = 1;
        for (var index = 0; index < _launchLogBuilder.Length; index++)
        {
            if (_launchLogBuilder[index] == '\n')
            {
                lineCount++;
            }
        }

        return lineCount;
    }

    private static IEnumerable<string> EnumerateLogDirectoryFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveLauncherFolder(string rawValue, FrontendRuntimePaths runtimePaths)
    {
        var normalized = string.IsNullOrWhiteSpace(rawValue)
            ? "$.minecraft\\"
            : rawValue.Trim();
        normalized = normalized.Replace("$", EnsureStepSurfaceTrailingSeparator(runtimePaths.ExecutableDirectory), StringComparison.Ordinal);
        return Path.GetFullPath(normalized);
    }

    private static string EnsureStepSurfaceTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static InstanceSelectionSnapshot? BuildInstanceSelectionSnapshot(string directory, string selectedInstance)
    {
        var name = Path.GetFileName(directory);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var instanceConfig = OpenInstanceConfigProvider(directory);
        var manifestPath = Path.Combine(directory, $"{name}.json");
        var manifest = ParseInstanceManifest(manifestPath);
        var tags = new List<string>();
        if (ReadValue(instanceConfig, "IsStar", false))
        {
            tags.Add("收藏");
        }

        var category = MapInstanceCategory(ReadValue(instanceConfig, "DisplayType", 0));
        if (!string.IsNullOrWhiteSpace(category))
        {
            tags.Add(category);
        }

        if (manifest.LoaderLabel is not null)
        {
            tags.Add(manifest.LoaderLabel);
        }

        if (manifest.IsBroken)
        {
            tags.Add("配置缺失");
        }

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(manifest.VersionLabel))
        {
            subtitleParts.Add($"Minecraft {manifest.VersionLabel}");
        }

        if (!string.IsNullOrWhiteSpace(manifest.LoaderLabel))
        {
            subtitleParts.Add(manifest.LoaderLabel);
        }

        var customInfo = ReadValue(
            instanceConfig,
            "VersionArgumentInfo",
            ReadValue(instanceConfig, "CustomInfo", string.Empty));
        if (!string.IsNullOrWhiteSpace(customInfo))
        {
            subtitleParts.Add(customInfo.Trim());
        }

        var subtitle = subtitleParts.Count == 0
            ? "尚未识别实例版本信息"
            : string.Join(" • ", subtitleParts);
        var detail = $"{directory} • 最近修改 {Directory.GetLastWriteTime(directory):yyyy-MM-dd HH:mm}";

        return new InstanceSelectionSnapshot(
            name,
            subtitle,
            detail,
            tags,
            string.Equals(name, selectedInstance, StringComparison.OrdinalIgnoreCase),
            ReadValue(instanceConfig, "IsStar", false),
            manifest.IsBroken,
            directory);
    }

    private static bool MatchesInstanceSelectionQuery(InstanceSelectionSnapshot entry, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || entry.Subtitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || entry.Detail.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || entry.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private InstanceSelectEntryViewModel CreateInstanceSelectionEntry(InstanceSelectionSnapshot entry)
    {
        return new InstanceSelectEntryViewModel(
            entry.Name,
            entry.Subtitle,
            entry.Detail,
            entry.Tags,
            entry.IsSelected,
            new ActionCommand(() => SelectInstanceForLaunch(entry.Name)),
            new ActionCommand(() => OpenInstanceSelectionEntry(entry)),
            new ActionCommand(() =>
            {
                if (_shellActionService.TryOpenExternalTarget(entry.Directory, out var error))
                {
                    AddActivity("打开实例目录", entry.Directory);
                }
                else
                {
                    AddActivity("打开实例目录失败", error ?? entry.Directory);
                }
            }));
    }

    private static TaskManagerEntryViewModel CreateTaskManagerEntry(TaskModel task)
    {
        return new TaskManagerEntryViewModel(
            task.Title,
            MapTaskStateLabel(task.State),
            string.IsNullOrWhiteSpace(task.StateMessage) ? "等待状态消息" : task.StateMessage,
            task.SupportProgress ? $"{Math.Round(task.Progress * 100, MidpointRounding.AwayFromZero)}%" : "无进度信息",
            task.Children.Count,
            task.Cancel.CanExecute(null),
            task.Pause.CanExecute(null),
            new ActionCommand(() => task.Cancel.Execute(null), () => task.Cancel.CanExecute(null)),
            new ActionCommand(() => task.Pause.Execute(null), () => task.Pause.CanExecute(null)));
    }

    private static string MapTaskStateLabel(TaskState state)
    {
        return state switch
        {
            TaskState.Waiting => "等待中",
            TaskState.Running => "运行中",
            TaskState.Success => "已完成",
            TaskState.Failed => "失败",
            TaskState.Canceled => "已取消",
            _ => state.ToString()
        };
    }

    private void OnTaskCenterCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncTaskSubscriptions();
        Dispatcher.UIThread.Post(RefreshTaskManagerSurface);
    }

    private void SyncTaskSubscriptions()
    {
        var activeTasks = TaskCenter.Tasks.ToHashSet();
        foreach (var staleTask in _observedTaskModels.Where(task => !activeTasks.Contains(task)).ToArray())
        {
            staleTask.PropertyChanged -= OnTaskModelPropertyChanged;
            staleTask.Children.CollectionChanged -= OnTaskChildrenChanged;
            _observedTaskModels.Remove(staleTask);
        }

        foreach (var activeTask in activeTasks)
        {
            if (_observedTaskModels.Add(activeTask))
            {
                activeTask.PropertyChanged += OnTaskModelPropertyChanged;
                activeTask.Children.CollectionChanged += OnTaskChildrenChanged;
            }
        }
    }

    private void OnTaskModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshTaskManagerSurface);
    }

    private void OnTaskChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshTaskManagerSurface);
    }

    private static YamlFileProvider OpenInstanceConfigProvider(string instanceDirectory)
    {
        var pclDirectory = Path.Combine(instanceDirectory, "PCL");
        var configPath = Path.Combine(pclDirectory, "config.v1.yml");
        if (!File.Exists(configPath))
        {
            var legacyPath = Path.Combine(pclDirectory, "Setup.ini");
            if (File.Exists(legacyPath))
            {
                Directory.CreateDirectory(pclDirectory);
                var provider = new YamlFileProvider(configPath);
                foreach (var line in File.ReadLines(legacyPath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var splitIndex = line.IndexOf(':');
                    if (splitIndex <= 0)
                    {
                        continue;
                    }

                    provider.Set(line[..splitIndex], line[(splitIndex + 1)..]);
                }

                provider.Sync();
            }
        }

        return new YamlFileProvider(configPath);
    }

    private static InstanceManifestSnapshot ParseInstanceManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return new InstanceManifestSnapshot("Unknown", null, true);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var versionLabel = GetString(root, "inheritsFrom")
                               ?? GetString(root, "id")
                               ?? "Unknown";
            var loaderLabel = ResolveLoaderLabel(root);
            return new InstanceManifestSnapshot(versionLabel, loaderLabel, false);
        }
        catch
        {
            return new InstanceManifestSnapshot("Unknown", null, true);
        }
    }

    private static string? ResolveLoaderLabel(JsonElement root)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var names = libraries.EnumerateArray()
            .Select(library => GetString(library, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();

        if (names.Any(name => name.Contains("neoforge", StringComparison.OrdinalIgnoreCase)))
        {
            return "NeoForge";
        }

        if (names.Any(name => name.Contains("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase)))
        {
            return "Forge";
        }

        if (names.Any(name => name.Contains("cleanroom", StringComparison.OrdinalIgnoreCase)))
        {
            return "Cleanroom";
        }

        if (names.Any(name => name.Contains("net.fabricmc:fabric-loader", StringComparison.OrdinalIgnoreCase)))
        {
            return "Fabric";
        }

        if (names.Any(name => name.Contains("org.quiltmc", StringComparison.OrdinalIgnoreCase)))
        {
            return "Quilt";
        }

        if (names.Any(name => name.Contains("liteloader", StringComparison.OrdinalIgnoreCase)))
        {
            return "LiteLoader";
        }

        if (names.Any(name => name.Contains("labymod", StringComparison.OrdinalIgnoreCase)))
        {
            return "LabyMod";
        }

        return null;
    }

    private static string MapInstanceCategory(int displayType)
    {
        return displayType switch
        {
            1 => "收藏夹",
            2 => "API",
            3 => "隐藏",
            4 => "较少使用",
            _ => string.Empty
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static T ReadValue<T>(YamlFileProvider provider, string key, T fallback)
    {
        try
        {
            return provider.Exists(key)
                ? provider.Get<T>(key)
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}

internal sealed class InstanceSelectEntryViewModel(
    string title,
    string subtitle,
    string detail,
    IReadOnlyList<string> tags,
    bool isSelected,
    ActionCommand selectCommand,
    ActionCommand openCommand,
    ActionCommand openFolderCommand)
{
    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public string Detail { get; } = detail;

    public IReadOnlyList<string> Tags { get; } = tags;

    public bool HasTags => Tags.Count > 0;

    public bool IsSelected { get; } = isSelected;

    public string SelectText => IsSelected ? "当前实例" : "设为启动实例";

    public ActionCommand SelectCommand { get; } = selectCommand;

    public ActionCommand OpenCommand { get; } = openCommand;

    public ActionCommand OpenFolderCommand { get; } = openFolderCommand;
}

internal sealed class TaskManagerEntryViewModel(
    string title,
    string state,
    string summary,
    string progressText,
    int childCount,
    bool canCancel,
    bool canPause,
    ActionCommand cancelCommand,
    ActionCommand pauseCommand)
{
    public string Title { get; } = title;

    public string State { get; } = state;

    public string Summary { get; } = summary;

    public string ProgressText { get; } = progressText;

    public int ChildCount { get; } = childCount;

    public bool HasChildren => ChildCount > 0;

    public string ChildrenText => $"子任务 {ChildCount} 项";

    public bool CanCancel { get; } = canCancel;

    public bool CanPause { get; } = canPause;

    public ActionCommand CancelCommand { get; } = cancelCommand;

    public ActionCommand PauseCommand { get; } = pauseCommand;
}

internal sealed record DedicatedGenericRouteMetadata(
    string Eyebrow,
    string Description,
    IReadOnlyList<LauncherFrontendPageFact> Facts);

internal sealed record InstanceSelectionSnapshot(
    string Name,
    string Subtitle,
    string Detail,
    IReadOnlyList<string> Tags,
    bool IsSelected,
    bool IsStarred,
    bool IsBroken,
    string Directory);

internal sealed record InstanceManifestSnapshot(
    string VersionLabel,
    string? LoaderLabel,
    bool IsBroken);
