using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private readonly HashSet<TaskModel> _observedTaskModels = [];
    private string _instanceSelectionSearchQuery = string.Empty;
    private string _instanceSelectionLauncherDirectory = string.Empty;
    private int _instanceSelectionTotalCount;
    private string _taskManagerActiveTaskTitle = "当前没有活动任务";
    private int _taskManagerWaitingCount;
    private int _taskManagerRunningCount;
    private int _taskManagerFinishedCount;
    private int _taskManagerFailedCount;
    private double _taskManagerOverallProgress = 1d;
    private string _taskManagerDownloadSpeedText = "0 B/s";
    private string _taskManagerRemainingFilesText = "0";
    private DispatcherTimer? _taskManagerRefreshTimer;
    private int _gameLogRecentFileCount;
    private string _gameLogLatestUpdateLabel = "尚未发现日志文件";

    public ObservableCollection<InstanceSelectEntryViewModel> InstanceSelectionEntries { get; } = [];

    public ObservableCollection<InstanceSelectionGroupViewModel> InstanceSelectionGroups { get; } = [];

    public ObservableCollection<InstanceSelectionFolderEntryViewModel> InstanceSelectionFolderEntries { get; } = [];

    public ObservableCollection<InstanceSelectionShortcutEntryViewModel> InstanceSelectionShortcutEntries { get; } = [];

    public ObservableCollection<TaskManagerEntryViewModel> TaskManagerEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> GameLogFileEntries { get; } = [];

    public ActionCommand RefreshInstanceSelectionCommand => _refreshInstanceSelectionCommand;

    public ActionCommand ClearInstanceSelectionSearchCommand => _clearInstanceSelectionSearchCommand;

    public ActionCommand AddInstanceSelectionFolderCommand => _addInstanceSelectionFolderCommand;

    public ActionCommand ImportInstanceSelectionPackCommand => _importInstanceSelectionPackCommand;

    public ActionCommand OpenInstanceSelectionDownloadCommand => _openInstanceSelectionDownloadCommand;

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

    public bool HasInstanceSelectionFolders => InstanceSelectionFolderEntries.Count > 0;

    public bool HasInstanceSelectionSearchBox => _instanceSelectionTotalCount > 0 || !string.IsNullOrWhiteSpace(InstanceSelectionSearchQuery);

    public bool ShowInstanceSelectionEmptyDownloadAction => _instanceSelectionTotalCount == 0;

    public bool ShowInstanceSelectionEmptyClearAction => _instanceSelectionTotalCount > 0 && !HasInstanceSelectionEntries;

    public string InstanceSelectionEmptyTitle => _instanceSelectionTotalCount == 0
        ? "无可用实例"
        : "无匹配实例";

    public string InstanceSelectionEmptyDescription => _instanceSelectionTotalCount == 0
        ? "未找到任何游戏实例，请先下载一个游戏实例。\n若有已存在的实例，请在左边的列表中选择文件夹，或通过添加已有文件夹将其导入。"
        : $"没有找到与“{InstanceSelectionSearchQuery.Trim()}”匹配的实例，请尝试更换关键词或清空搜索。";

    public string InstanceSelectionLauncherDirectoryLabel => GetInstanceSelectionDirectoryLabel(_instanceSelectionLauncherDirectory);

    public string InstanceSelectionLauncherDirectoryPath => _instanceSelectionLauncherDirectory;

    public string InstanceSelectionLauncherDirectory => _instanceSelectionLauncherDirectory;

    public string InstanceSelectionResultSummary => HasInstanceSelectionEntries
        ? $"已显示 {InstanceSelectionEntries.Count} 个实例"
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
        : "当前没有后台任务";

    public string TaskManagerActiveTaskTitle => _taskManagerActiveTaskTitle;

    public double TaskManagerOverallProgress => _taskManagerOverallProgress;

    public double TaskManagerOverallProgressValue => _taskManagerOverallProgress * 100d;

    public string TaskManagerOverallProgressText => $"{Math.Round(TaskManagerOverallProgress * 100, 1, MidpointRounding.AwayFromZero)} %";

    public string TaskManagerDownloadSpeedText => _taskManagerDownloadSpeedText;

    public string TaskManagerRemainingFilesText => _taskManagerRemainingFilesText;

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
        EnsureTaskManagerRefreshTimer();
        SyncTaskSubscriptions();
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
                    "查看当前启动目录中的实例列表，并切换启动目标。",
                    [
                        new LauncherFrontendPageFact("启动目录", string.IsNullOrWhiteSpace(_instanceSelectionLauncherDirectory) ? "未解析" : _instanceSelectionLauncherDirectory),
                        new LauncherFrontendPageFact("已选实例", _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : "未选择"),
                        new LauncherFrontendPageFact("结果数量", $"{InstanceSelectionEntries.Count} / {_instanceSelectionTotalCount}")
                    ]);
                return true;
            case LauncherFrontendPageKey.TaskManager:
                metadata = new DedicatedGenericRouteMetadata(
                    "任务中心",
                    "查看后台任务的等待、执行、完成和失败状态。",
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
                    "查看资源工程的实时社区信息与最近版本。",
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
        var sharedConfig = new JsonFileProvider(runtimePaths.SharedConfigPath);
        var launcherDirectory = ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var selectedInstance = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        _instanceSelectionLauncherDirectory = launcherDirectory;

        ReplaceItems(
            InstanceSelectionFolderEntries,
            BuildInstanceSelectionFolderSnapshots(sharedConfig, localConfig, runtimePaths, launcherDirectory)
                .Select(CreateInstanceSelectionFolderEntry));

        ReplaceItems(
            InstanceSelectionShortcutEntries,
            [
                CreateInstanceSelectionShortcutEntry(
                    "添加已有文件夹",
                    "选择一个包含 versions 目录的 .minecraft 或启动目录",
                    "F1 m 12 7 a 1 1 0 0 0 -1 1 v 8 a 1 1 0 0 0 1 1 a 1 1 0 0 0 1 -1 V 8 A 1 1 0 0 0 12 7 Z m -4 4 a 1 1 0 0 0 -1 1 a 1 1 0 0 0 1 1 h 8 a 1 1 0 0 0 1 -1 a 1 1 0 0 0 -1 -1 z M 12 1 C 5.93671 1 1 5.93671 1 12 C 1 18.0633 5.93671 23 12 23 C 18.0633 23 23 18.0633 23 12 C 23 5.93671 18.0633 1 12 1 Z m 0 2 c 4.98241 0 9 4.01759 9 9 c 0 4.98241 -4.01759 9 -9 9 C 7.01759 21 3 16.9824 3 12 C 3 7.01759 7.01759 3 12 3 Z",
                    _addInstanceSelectionFolderCommand),
                CreateInstanceSelectionShortcutEntry(
                    "导入整合包",
                    "选择 CurseForge、Modrinth 或普通压缩整合包文件",
                    "F1 m 11.293 11.293 l -3 3 a 1 1 0 0 0 0 1.41406 a 1 1 0 0 0 1.41406 0 L 12 13.4141 l 2.29297 2.29297 a 1 1 0 0 0 1.41406 0 a 1 1 0 0 0 0 -1.41406 l -3 -3 a 1.0001 1.0001 0 0 0 -1.41406 0 z M 12 11 a 1 1 0 0 0 -1 1 v 6 a 1 1 0 0 0 1 1 a 1 1 0 0 0 1 -1 V 12 A 1 1 0 0 0 12 11 Z M 14 1 a 1 1 0 0 0 -1 1 v 5 c 0 1.09272 0.907275 2 2 2 h 5 A 1 1 0 0 0 21 8 A 1 1 0 0 0 20 7 H 15 V 2 A 1 1 0 0 0 14 1 Z M 6 1 C 4.35499 1 3 2.35499 3 4 v 16 c 0 1.64501 1.35499 3 3 3 h 12 c 1.64501 0 3 -1.35499 3 -3 V 8.00195 V 8 C 21.001 7.09394 20.6387 6.22279 19.9961 5.58398 L 16.4121 2 L 16.4101 1.99805 C 15.7718 1.35838 14.9038 0.999054 14 1 Z m 0 2 h 8 a 1.0001 1.0001 0 0 0 0.002 0 c 0.373356 -0.0006051 0.730614 0.147632 0.994141 0.412109 a 1.0001 1.0001 0 0 0 0 0.00195 l 3.58789 3.58789 a 1.0001 1.0001 0 0 0 0.0039 0.00195 C 18.8531 7.26753 19.0006 7.62412 19 7.99805 A 1.0001 1.0001 0 0 0 19 8 v 12 c 0 0.564129 -0.435871 1 -1 1 H 6 C 5.43587 21 5 20.5641 5 20 V 4 C 5 3.43587 5.43587 3 6 3 Z",
                    _importInstanceSelectionPackCommand),
                CreateInstanceSelectionShortcutEntry(
                    "实例回收区",
                    "打开当前启动目录的实例回收区，继续手动清理或恢复实例",
                    FrontendIconCatalog.FolderOutline.Data,
                    new ActionCommand(OpenInstanceSelectionTrashDirectory))
            ]);

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

        var groupExpansionStates = InstanceSelectionGroups.ToDictionary(
            group => group.Title,
            group => group.IsExpanded,
            StringComparer.CurrentCultureIgnoreCase);

        ReplaceItems(
            InstanceSelectionEntries,
            filteredEntries.Select(entry => CreateInstanceSelectionEntry(entry)));

        ReplaceItems(
            InstanceSelectionGroups,
            BuildInstanceSelectionGroups(filteredEntries, groupExpansionStates));

        RaisePropertyChanged(nameof(InstanceSelectionLauncherDirectory));
        RaisePropertyChanged(nameof(InstanceSelectionLauncherDirectoryLabel));
        RaisePropertyChanged(nameof(InstanceSelectionLauncherDirectoryPath));
        RaisePropertyChanged(nameof(HasInstanceSelectionFolders));
        RaisePropertyChanged(nameof(HasInstanceSelectionSearchBox));
        RaisePropertyChanged(nameof(HasInstanceSelectionEntries));
        RaisePropertyChanged(nameof(HasNoInstanceSelectionEntries));
        RaisePropertyChanged(nameof(InstanceSelectionResultSummary));
        RaisePropertyChanged(nameof(InstanceSelectionEmptyTitle));
        RaisePropertyChanged(nameof(InstanceSelectionEmptyDescription));
        RaisePropertyChanged(nameof(ShowInstanceSelectionEmptyDownloadAction));
        RaisePropertyChanged(nameof(ShowInstanceSelectionEmptyClearAction));
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

        var primaryTask = tasks
            .OrderByDescending(task => task.State == TaskState.Running)
            .ThenByDescending(task => task.State == TaskState.Waiting)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
        _taskManagerActiveTaskTitle = primaryTask?.Title ?? "当前没有活动任务";
        _taskManagerOverallProgress = primaryTask?.SupportProgress == true
            ? Math.Clamp(primaryTask.Progress, 0d, 1d)
            : tasks.Length == 0
                ? 1d
                : 0d;
        _taskManagerDownloadSpeedText = string.IsNullOrWhiteSpace(primaryTask?.SpeedText)
            ? "0 B/s"
            : primaryTask.SpeedText;
        _taskManagerRemainingFilesText = primaryTask?.RemainingFileCount?.ToString() ?? "0";

        RaisePropertyChanged(nameof(HasTaskManagerEntries));
        RaisePropertyChanged(nameof(HasNoTaskManagerEntries));
        RaisePropertyChanged(nameof(TaskManagerActiveTaskTitle));
        RaisePropertyChanged(nameof(TaskManagerWaitingCount));
        RaisePropertyChanged(nameof(TaskManagerRunningCount));
        RaisePropertyChanged(nameof(TaskManagerFinishedCount));
        RaisePropertyChanged(nameof(TaskManagerFailedCount));
        RaisePropertyChanged(nameof(TaskManagerOverallProgress));
        RaisePropertyChanged(nameof(TaskManagerOverallProgressValue));
        RaisePropertyChanged(nameof(TaskManagerOverallProgressText));
        RaisePropertyChanged(nameof(TaskManagerDownloadSpeedText));
        RaisePropertyChanged(nameof(TaskManagerRemainingFilesText));
        RaisePropertyChanged(nameof(TaskManagerSummary));
        RaisePropertyChanged(nameof(HasRunningTaskManagerTasks));
        RaisePropertyChanged(nameof(ShowTaskManagerShortcutButton));
        RaisePropertyChanged(nameof(ShowBottomRightExtraButtons));
        RefreshDynamicUtilityEntries();
    }

    private void RefreshGameLogSurface()
    {
        var runtimePaths = _shellActionService.RuntimePaths;
        var candidateFiles = FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                runtimePaths.LauncherAppDataDirectory,
                _shellActionService.PlatformAdapter)
            .Concat(
                FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                    Path.Combine(runtimePaths.ExecutableDirectory, "PCL"),
                    _shellActionService.PlatformAdapter))
            .Concat(
            [
                Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "RawOutput.log"),
                Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "latest.log"),
                Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "PCL.log"),
                Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "RawOutput.log"),
                Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "latest.log"),
                Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "PCL.log")
            ]);
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
        RefreshDynamicUtilityEntries();
    }

    private void SelectInstanceForLaunch(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return;
        }

        if (_instanceComposition.Selection.HasSelection &&
            string.Equals(_instanceComposition.Selection.InstanceName, instanceName, System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyOptimisticInstanceSelection(instanceName);
            return;
        }

        RefreshSelectedInstanceSmoothly(instanceName);
    }

    private void SelectInstanceAndCloseSelection(InstanceSelectionSnapshot entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return;
        }

        if (!_instanceComposition.Selection.HasSelection ||
            !string.Equals(_instanceComposition.Selection.InstanceName, entry.Name, System.StringComparison.OrdinalIgnoreCase))
        {
            RefreshSelectedInstanceSmoothly(entry.Name);
        }

        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            $"已切换启动实例到 {entry.Name} 并返回启动页。");
    }

    private async Task AddInstanceSelectionFolderAsync()
    {
        try
        {
            var pickedFolderPath = await _shellActionService.PickFolderAsync("选择已有 Minecraft 文件夹");
            if (string.IsNullOrWhiteSpace(pickedFolderPath))
            {
                AddActivity("添加已有文件夹", "未选择任何文件夹。");
                return;
            }

            var runtimePaths = _shellActionService.RuntimePaths;
            var resolvedFolderPath = ResolvePickedLauncherFolderPath(pickedFolderPath);
            var localConfig = new YamlFileProvider(runtimePaths.LocalConfigPath);
            var sharedConfig = new JsonFileProvider(runtimePaths.SharedConfigPath);
            var currentFolderPath = ResolveLauncherFolder(
                ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
                runtimePaths);
            var configuredFolders = BuildInstanceSelectionFolderSnapshots(sharedConfig, localConfig, runtimePaths, currentFolderPath)
                .ToList();
            var existingFolder = configuredFolders.FirstOrDefault(folder =>
                string.Equals(folder.Directory, resolvedFolderPath, GetPathComparison()));
            var addedToList = existingFolder is null;

            if (existingFolder is null)
            {
                configuredFolders.Add(new InstanceSelectionFolderSnapshot(
                    GetInstanceSelectionDirectoryLabel(resolvedFolderPath),
                    resolvedFolderPath,
                    StoreLauncherFolderPath(resolvedFolderPath, runtimePaths)));
                _shellActionService.PersistSharedValue("LaunchFolders", SerializeInstanceSelectionFolders(configuredFolders, runtimePaths));
            }

            RefreshSelectedLauncherFolderSmoothly(
                StoreLauncherFolderPath(resolvedFolderPath, runtimePaths),
                resolvedFolderPath,
                addedToList
                ? $"已将 {resolvedFolderPath} 添加到实例目录列表并切换过去。"
                : $"已切换启动目录到 {resolvedFolderPath}。");
        }
        catch (Exception ex)
        {
            AddActivity("添加已有文件夹失败", ex.Message);
        }
    }

    private async Task ImportInstanceSelectionPackAsync()
    {
        try
        {
            var sourcePath = await _shellActionService.PickOpenFileAsync(
                "选择整合包文件",
                "整合包文件",
                "*.zip",
                "*.mrpack",
                "*.rar",
                "*.7z");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                AddActivity("导入整合包", "未选择任何整合包文件。");
                return;
            }

            AddActivity("导入整合包", $"已选择 {Path.GetFileName(sourcePath)}，后续可在下载页面继续安装流程。");
            NavigateTo(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Download, LauncherFrontendSubpageKey.DownloadInstall),
                $"已准备导入整合包 {Path.GetFileName(sourcePath)}。");
        }
        catch (Exception ex)
        {
            AddActivity("导入整合包失败", ex.Message);
        }
    }

    private void OpenInstanceSelectionEntry(InstanceSelectionSnapshot entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return;
        }

        _shellActionService.PersistLocalValue("LaunchInstanceSelect", entry.Name);
        if (!_instanceComposition.Selection.HasSelection
            || !string.Equals(
                _instanceComposition.Selection.InstanceName,
                entry.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            ApplyOptimisticInstanceSelection(entry.Name);
            var refreshVersion = System.Threading.Interlocked.Increment(ref _instanceSelectionRefreshVersion);
            _ = RefreshSelectedInstanceStateAsync(refreshVersion);
        }

        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall),
            $"已打开实例 {entry.Name} 的概览页面。");
    }

    private void ToggleInstanceSelectionFavorite(InstanceSelectionSnapshot entry)
    {
        try
        {
            var nextIsFavorite = !entry.IsStarred;
            _shellActionService.PersistInstanceValue(entry.Directory, "IsStar", nextIsFavorite);
            RefreshInstanceSelectionSurface();
            AddActivity(nextIsFavorite ? "加入收藏夹" : "移出收藏夹", entry.Name);
        }
        catch (Exception ex)
        {
            AddActivity("切换实例收藏状态失败", ex.Message);
        }
    }

    private async Task DeleteInstanceSelectionEntryAsync(InstanceSelectionSnapshot entry)
    {
        var confirmed = await _shellActionService.ConfirmAsync(
            "实例删除确认",
            $"确定要将实例 {entry.Name} 移入回收区吗？该操作会保留实例目录，便于后续恢复。",
            "移入回收区",
            isDanger: true);
        if (!confirmed)
        {
            AddActivity("删除实例", "已取消删除。");
            return;
        }

        try
        {
            var showIndieWarning = _instanceComposition.Selection.HasSelection
                && string.Equals(_instanceComposition.Selection.InstanceDirectory, entry.Directory, GetPathComparison())
                && _instanceComposition.Selection.IsIndie;
            var outcome = await DeleteInstanceDirectoryAsync(
                entry.Name,
                entry.Directory,
                _instanceSelectionLauncherDirectory,
                showIndieWarning);
            if (outcome is null)
            {
                return;
            }

            if (_instanceComposition.Selection.HasSelection
                && (string.Equals(_instanceComposition.Selection.InstanceDirectory, entry.Directory, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(_instanceComposition.Selection.InstanceName, entry.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _shellActionService.PersistLocalValue("LaunchInstanceSelect", string.Empty);
                RefreshLaunchState();
            }

            RefreshInstanceSelectionSurface();
            if (outcome.IsPermanentDelete)
            {
                AddActivity("删除实例", $"实例 {outcome.InstanceName} 已永久删除。");
                return;
            }

            OpenInstanceTarget("实例回收区", outcome.TrashDirectory, "回收区目录不存在。");
        }
        catch (Exception ex)
        {
            AddActivity("删除实例失败", ex.Message);
        }
    }

    private void ClearGameLogSurface()
    {
        _launchLogBuilder.Clear();
        RaiseGameLogSurfaceProperties();
        AddActivity("清空实时日志", "已清空当前会话输出缓存。");
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
                           || path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".command", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveLauncherFolder(string rawValue, FrontendRuntimePaths runtimePaths)
    {
        return FrontendLauncherPathService.ResolveLauncherFolder(rawValue, runtimePaths);
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
            directory,
            ReadValue(instanceConfig, "DisplayType", 0),
            manifest.VersionLabel,
            manifest.LoaderLabel,
            string.IsNullOrWhiteSpace(customInfo) ? null : customInfo.Trim(),
            ReadValue(instanceConfig, "LogoCustom", false),
            ReadValue(instanceConfig, "Logo", string.Empty));
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
        var icon = ResolveInstanceSelectionBitmap(entry);
        var displayTags = BuildInstanceSelectionDisplayTags(entry);
        var subtitle = BuildInstanceSelectionSubtitle(entry, displayTags);

        return new InstanceSelectEntryViewModel(
            entry.Name,
            subtitle,
            entry.Detail,
            displayTags,
            entry.IsSelected,
            entry.IsStarred,
            icon,
            new ActionCommand(() => SelectInstanceAndCloseSelection(entry)),
            new ActionCommand(() => OpenInstanceSelectionEntry(entry)),
            new ActionCommand(() => ToggleInstanceSelectionFavorite(entry)),
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
            }),
            new ActionCommand(() => _ = DeleteInstanceSelectionEntryAsync(entry)));
    }

    private IReadOnlyList<InstanceSelectionGroupViewModel> BuildInstanceSelectionGroups(
        IReadOnlyList<InstanceSelectionSnapshot> entries,
        IReadOnlyDictionary<string, bool> groupExpansionStates)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var groups = new List<InstanceSelectionGroupViewModel>();
        var favorites = entries.Where(entry => entry.IsStarred).ToArray();
        if (favorites.Length > 0)
        {
            groups.Add(CreateInstanceSelectionGroup("收藏夹", favorites, groupExpansionStates, isExpandedByDefault: true));
        }

        foreach (var bucket in entries.GroupBy(GetInstanceSelectionBaseGroupKey))
        {
            var orderedEntries = bucket
                .OrderByDescending(entry => entry.IsSelected)
                .ThenByDescending(entry => entry.IsStarred)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            groups.Add(CreateInstanceSelectionGroup(
                ResolveInstanceSelectionGroupTitle(bucket.Key, orderedEntries),
                orderedEntries,
                groupExpansionStates,
                isExpandedByDefault: bucket.Key is not "error" and not "rarely-used" and not "hidden"));
        }

        return groups
            .OrderBy(group => GetInstanceSelectionGroupPriority(group.Title))
            .ThenBy(group => group.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private InstanceSelectionGroupViewModel CreateInstanceSelectionGroup(
        string title,
        IReadOnlyList<InstanceSelectionSnapshot> entries,
        IReadOnlyDictionary<string, bool> groupExpansionStates,
        bool isExpandedByDefault)
    {
        return new InstanceSelectionGroupViewModel(
            title,
            entries.Select(CreateInstanceSelectionEntry).ToArray(),
            groupExpansionStates.TryGetValue(title, out var isExpanded)
                ? isExpanded
                : isExpandedByDefault);
    }

    private static IReadOnlyList<string> BuildInstanceSelectionDisplayTags(InstanceSelectionSnapshot entry)
    {
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.VersionLabel))
        {
            tags.Add(entry.VersionLabel);
        }

        if (!string.IsNullOrWhiteSpace(entry.LoaderLabel))
        {
            tags.Add(entry.LoaderLabel);
        }

        if (entry.IsBroken)
        {
            tags.Add("配置缺失");
        }
        else if (entry.DisplayType == 4)
        {
            tags.Add("较少使用");
        }

        return tags;
    }

    private static string BuildInstanceSelectionSubtitle(InstanceSelectionSnapshot entry, IReadOnlyList<string> displayTags)
    {
        if (!string.IsNullOrWhiteSpace(entry.CustomInfo))
        {
            return entry.CustomInfo!;
        }

        if (!string.IsNullOrWhiteSpace(entry.Subtitle))
        {
            var subtitle = entry.Subtitle
                .Replace("Minecraft ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("•", " ", StringComparison.Ordinal)
                .Trim();
            foreach (var tag in displayTags)
            {
                subtitle = subtitle.Replace(tag, string.Empty, StringComparison.CurrentCultureIgnoreCase).Trim();
            }

            return string.Join(" ", subtitle.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        return string.Empty;
    }

    private static Bitmap? ResolveInstanceSelectionBitmap(InstanceSelectionSnapshot entry)
    {
        if (entry.IsCustomLogo)
        {
            var customPath = Path.Combine(entry.Directory, "PCL", "Logo.png");
            if (File.Exists(customPath))
            {
                return new Bitmap(customPath);
            }
        }

        var mappedLogo = MapStoredLogoPath(entry.RawLogoPath);
        if (!string.IsNullOrWhiteSpace(mappedLogo) && File.Exists(mappedLogo))
        {
            return new Bitmap(mappedLogo);
        }

        return LoadLauncherBitmap("Images", "Blocks", DetermineInstanceSelectionIconName(entry));
    }

    private static string? MapStoredLogoPath(string rawLogoPath)
    {
        if (string.IsNullOrWhiteSpace(rawLogoPath))
        {
            return null;
        }

        var fileName = Path.GetFileName(rawLogoPath.Replace("pack://application:,,,/images/Blocks/", string.Empty, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return Path.Combine(LauncherRootDirectory, "Images", "Blocks", fileName);
    }

    private static string DetermineInstanceSelectionIconName(InstanceSelectionSnapshot entry)
    {
        if (entry.IsBroken)
        {
            return "RedstoneBlock.png";
        }

        return entry.LoaderLabel switch
        {
            "Forge" => "Anvil.png",
            "NeoForge" => "NeoForge.png",
            "Cleanroom" => "Cleanroom.png",
            "Fabric" or "Legacy Fabric" => "Fabric.png",
            "Quilt" => "Quilt.png",
            "LiteLoader" => "GrassPath.png",
            "LabyMod" => "LabyMod.png",
            _ => "Grass.png"
        };
    }

    private static string GetInstanceSelectionBaseGroupKey(InstanceSelectionSnapshot entry)
    {
        if (entry.IsBroken)
        {
            return "error";
        }

        return entry.DisplayType switch
        {
            3 => "hidden",
            4 => "rarely-used",
            _ when !string.IsNullOrWhiteSpace(entry.LoaderLabel) => $"loader:{NormalizeInstanceSelectionLoader(entry.LoaderLabel!)}",
            2 => "api",
            _ => "normal"
        };
    }

    private static string ResolveInstanceSelectionGroupTitle(string key, IReadOnlyList<InstanceSelectionSnapshot> entries)
    {
        if (key.StartsWith("loader:", StringComparison.Ordinal))
        {
            return ResolveSingleLoaderInstanceGroupTitle(key["loader:".Length..]);
        }

        return key switch
        {
            "api" => ResolveApiInstanceGroupTitle(entries),
            "error" => "错误的实例",
            "hidden" => "隐藏的实例",
            "rarely-used" => "不常用实例",
            _ => "常规实例"
        };
    }

    private static string ResolveSingleLoaderInstanceGroupTitle(string loaderKey)
    {
        return loaderKey switch
        {
            "forge" => "Forge 实例",
            "neoforge" => "NeoForge 实例",
            "cleanroom" => "Cleanroom 实例",
            "labymod" => "LabyMod 实例",
            "liteloader" => "LiteLoader 实例",
            "quilt" => "Quilt 实例",
            "legacy-fabric" => "Legacy Fabric 实例",
            _ => "Fabric 实例"
        };
    }

    private static string NormalizeInstanceSelectionLoader(string loaderLabel)
    {
        return loaderLabel.Trim() switch
        {
            "Forge" => "forge",
            "NeoForge" => "neoforge",
            "Cleanroom" => "cleanroom",
            "LabyMod" => "labymod",
            "LiteLoader" => "liteloader",
            "Quilt" => "quilt",
            "Legacy Fabric" => "legacy-fabric",
            _ => "fabric"
        };
    }

    private static string ResolveApiInstanceGroupTitle(IReadOnlyList<InstanceSelectionSnapshot> entries)
    {
        var loaderLabels = entries
            .Select(entry => entry.LoaderLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (loaderLabels.Length > 1)
        {
            return "可安装 Mod";
        }

        if (loaderLabels.Length == 1)
        {
            return loaderLabels[0] switch
            {
                "Forge" => "Forge 实例",
                "NeoForge" => "NeoForge 实例",
                "Cleanroom" => "Cleanroom 实例",
                "LabyMod" => "LabyMod 实例",
                "LiteLoader" => "LiteLoader 实例",
                "Quilt" => "Quilt 实例",
                _ => "Fabric 实例"
            };
        }

        return "可安装 Mod";
    }

    private static int GetInstanceSelectionGroupPriority(string title)
    {
        return title switch
        {
            "收藏夹" => 0,
            "常规实例" => 1,
            "Fabric 实例" => 2,
            "Forge 实例" => 3,
            "NeoForge 实例" => 4,
            "Quilt 实例" => 5,
            "LiteLoader 实例" => 6,
            "Cleanroom 实例" => 7,
            "LabyMod 实例" => 8,
            "可安装 Mod" => 9,
            "不常用实例" => 10,
            "错误的实例" => 11,
            "隐藏的实例" => 12,
            _ => 99
        };
    }

    private InstanceSelectionFolderEntryViewModel CreateInstanceSelectionFolderEntry(InstanceSelectionFolderSnapshot folder)
    {
        return new InstanceSelectionFolderEntryViewModel(
            folder.Label,
            folder.Directory,
            string.Equals(folder.Directory, _instanceSelectionLauncherDirectory, GetPathComparison()),
            FrontendIconCatalog.Folder.Data,
            new ActionCommand(() =>
            {
                if (string.Equals(folder.Directory, _instanceSelectionLauncherDirectory, GetPathComparison()))
                {
                    AddActivity("当前文件夹", folder.Directory);
                    return;
                }

                RefreshSelectedLauncherFolderSmoothly(
                    folder.StoredPath,
                    folder.Directory,
                    $"已切换实例目录到 {folder.Directory}。");
            }),
            new ActionCommand(() =>
            {
                if (_shellActionService.TryOpenExternalTarget(folder.Directory, out var error))
                {
                    AddActivity("打开实例根目录", folder.Directory);
                }
                else
                {
                    AddActivity("打开实例根目录失败", error ?? folder.Directory);
                }
            }));
    }

    private static InstanceSelectionShortcutEntryViewModel CreateInstanceSelectionShortcutEntry(
        string title,
        string description,
        string iconPath,
        ActionCommand command)
    {
        return new InstanceSelectionShortcutEntryViewModel(title, description, iconPath, command);
    }

    private static TaskManagerEntryViewModel CreateTaskManagerEntry(TaskModel task)
    {
        return new TaskManagerEntryViewModel(
            task.Title,
            task.State,
            MapTaskStateLabel(task.State),
            string.IsNullOrWhiteSpace(task.StateMessage) ? "等待状态消息" : task.StateMessage,
            task.SupportProgress
                ? (string.IsNullOrWhiteSpace(task.ProgressText)
                    ? $"{Math.Round(task.Progress * 100, 1, MidpointRounding.AwayFromZero)}%"
                    : task.ProgressText)
                : "无进度信息",
            task.Progress,
            task.SupportProgress,
            string.IsNullOrWhiteSpace(task.SpeedText) ? "0 B/s" : task.SpeedText,
            task.RemainingFileCount?.ToString() ?? "0",
            task.Children.Count,
            task.Children.Select(CreateTaskManagerStageEntry).ToArray(),
            task.Cancel.CanExecute(null),
            task.Pause.CanExecute(null),
            new ActionCommand(() => task.Cancel.Execute(null), () => task.Cancel.CanExecute(null)),
            new ActionCommand(() => task.Pause.Execute(null), () => task.Pause.CanExecute(null)));
    }

    private static TaskManagerStageEntryViewModel CreateTaskManagerStageEntry(TaskModel task)
    {
        var indicator = task.State switch
        {
            TaskState.Success => "✓",
            TaskState.Failed => "×",
            TaskState.Canceled => "×",
            TaskState.Running when task.SupportProgress => $"{Math.Round(task.Progress * 100, MidpointRounding.AwayFromZero)}%",
            TaskState.Running => "···",
            TaskState.Waiting => "···",
            _ => "·"
        };
        var message = string.IsNullOrWhiteSpace(task.StateMessage) ? task.Title : task.StateMessage;
        return new TaskManagerStageEntryViewModel(indicator, task.Title, message);
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
        QueueTaskManagerSurfaceRefresh(immediate: true);
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
        QueueTaskManagerSurfaceRefresh();
    }

    private void OnTaskChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueTaskManagerSurfaceRefresh();
    }

    private void QueueTaskManagerSurfaceRefresh(bool immediate = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            EnsureTaskManagerRefreshTimer();
            var refreshTimer = _taskManagerRefreshTimer!;
            if (immediate)
            {
                refreshTimer.Stop();
                RefreshTaskManagerSurface();
                return;
            }

            refreshTimer.Stop();
            refreshTimer.Start();
        });
    }

    private void EnsureTaskManagerRefreshTimer()
    {
        if (_taskManagerRefreshTimer is not null)
        {
            return;
        }

        _taskManagerRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _taskManagerRefreshTimer.Tick += (_, _) =>
        {
            _taskManagerRefreshTimer?.Stop();
            RefreshTaskManagerSurface();
        };
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

    private static IReadOnlyList<InstanceSelectionFolderSnapshot> BuildInstanceSelectionFolderSnapshots(
        IKeyValueFileProvider sharedConfig,
        IKeyValueFileProvider localConfig,
        FrontendRuntimePaths runtimePaths,
        string selectedDirectory)
    {
        var rawFolders = ReadValue(sharedConfig, "LaunchFolders", string.Empty);
        if (string.IsNullOrWhiteSpace(rawFolders))
        {
            rawFolders = ReadValue(localConfig, "LaunchFolders", string.Empty);
        }

        var folders = new List<InstanceSelectionFolderSnapshot>();
        var comparer = GetPathComparer();
        var seenDirectories = new HashSet<string>(comparer);
        foreach (var rawEntry in rawFolders.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var folder = ParseInstanceSelectionFolderSnapshot(rawEntry, runtimePaths);
            if (folder is null || !seenDirectories.Add(folder.Directory))
            {
                continue;
            }

            folders.Add(folder);
        }

        if (seenDirectories.Add(selectedDirectory))
        {
            folders.Insert(0, new InstanceSelectionFolderSnapshot(
                GetInstanceSelectionDirectoryLabel(selectedDirectory),
                selectedDirectory,
                StoreLauncherFolderPath(selectedDirectory, runtimePaths)));
        }

        return folders;
    }

    private static InstanceSelectionFolderSnapshot? ParseInstanceSelectionFolderSnapshot(string rawEntry, FrontendRuntimePaths runtimePaths)
    {
        if (string.IsNullOrWhiteSpace(rawEntry))
        {
            return null;
        }

        var separatorIndex = rawEntry.IndexOf('>');
        var label = separatorIndex > 0
            ? rawEntry[..separatorIndex].Trim()
            : string.Empty;
        var rawPath = separatorIndex >= 0
            ? rawEntry[(separatorIndex + 1)..].Trim()
            : rawEntry.Trim();
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var directory = ResolveLauncherFolder(rawPath, runtimePaths);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        return new InstanceSelectionFolderSnapshot(
            string.IsNullOrWhiteSpace(label) ? GetInstanceSelectionDirectoryLabel(directory) : label,
            directory,
            rawPath);
    }

    private static string SerializeInstanceSelectionFolders(
        IReadOnlyList<InstanceSelectionFolderSnapshot> folders,
        FrontendRuntimePaths runtimePaths)
    {
        return string.Join(
            "|",
            folders.Select(folder =>
            {
                var label = string.IsNullOrWhiteSpace(folder.Label)
                    ? GetInstanceSelectionDirectoryLabel(folder.Directory)
                    : folder.Label.Trim();
                var storedPath = StoreLauncherFolderPath(folder.Directory, runtimePaths);
                return $"{label}>{storedPath}";
            }));
    }

    private static string ResolvePickedLauncherFolderPath(string pickedFolderPath)
    {
        var fullPath = Path.GetFullPath(pickedFolderPath);
        if (string.Equals(Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "versions", GetPathComparison())
            && Directory.GetParent(fullPath) is { } parent)
        {
            return parent.FullName;
        }

        if (Directory.Exists(Path.Combine(fullPath, "versions")))
        {
            return fullPath;
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(fullPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (Directory.Exists(Path.Combine(childDirectory, "versions")))
            {
                return Path.GetFullPath(childDirectory);
            }
        }

        return fullPath;
    }

    private static string StoreLauncherFolderPath(string directory, FrontendRuntimePaths runtimePaths)
    {
        var fullPath = Path.GetFullPath(directory);
        var executableDirectory = EnsureTrailingSeparator(Path.GetFullPath(runtimePaths.ExecutableDirectory));
        var comparison = GetPathComparison();
        if (fullPath.StartsWith(executableDirectory, comparison))
        {
            var relativePath = fullPath[executableDirectory.Length..]
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(relativePath)
                ? "$"
                : $"${Path.DirectorySeparatorChar}{relativePath}";
        }

        return fullPath;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
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

    private static string GetInstanceSelectionDirectoryLabel(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "未选择文件夹";
        }

        var trimmed = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? trimmed : name;
    }

    private void RefreshInstanceSelectionRouteMetadata()
    {
        if (_currentRoute.Page != LauncherFrontendPageKey.InstanceSelect)
        {
            return;
        }

        if (TryBuildDedicatedGenericRouteMetadata(out var metadata))
        {
            Eyebrow = metadata.Eyebrow;
            Description = metadata.Description;
            ReplaceSurfaceFactsIfChanged(metadata.Facts);
            ReplaceSurfaceSectionsIfChanged([]);
        }
    }
}

internal sealed class InstanceSelectEntryViewModel(
    string title,
    string subtitle,
    string detail,
    IReadOnlyList<string> tags,
    bool isSelected,
    bool isFavorite,
    Bitmap? icon,
    ActionCommand selectCommand,
    ActionCommand openSettingsCommand,
    ActionCommand toggleFavoriteCommand,
    ActionCommand openFolderCommand,
    ActionCommand deleteCommand)
{
    private static readonly FrontendIcon NavigationSettingsIcon = FrontendIconCatalog.GetNavigationIcon("设置");

    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public string Detail { get; } = detail;

    public IReadOnlyList<string> Tags { get; } = tags;

    public bool HasTags => Tags.Count > 0;

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool HasMetadataContent => HasTags || HasSubtitle;

    public bool IsSelected { get; } = isSelected;

    public bool IsFavorite { get; } = isFavorite;

    public Bitmap? Icon { get; } = icon;

    public string SelectText => IsSelected ? "当前实例" : "设为启动实例";

    public string FavoriteIconData => IsFavorite
        ? FrontendIconCatalog.FavoriteFilled.Data
        : FrontendIconCatalog.FavoriteOutline.Data;

    public IBrush FavoriteIconBrush => Brush.Parse("#4592FF");

    public string OpenFolderIconData => FrontendIconCatalog.FolderOutline.Data;

    public string DeleteIconData => FrontendIconCatalog.DeleteOutline.Data;

    public string SettingsIconData => NavigationSettingsIcon.Data;

    public double SettingsIconScale => NavigationSettingsIcon.Scale;

    public ActionCommand SelectCommand { get; } = selectCommand;

    public ActionCommand OpenSettingsCommand { get; } = openSettingsCommand;

    public ActionCommand ToggleFavoriteCommand { get; } = toggleFavoriteCommand;

    public ActionCommand OpenFolderCommand { get; } = openFolderCommand;

    public ActionCommand DeleteCommand { get; } = deleteCommand;
}

internal sealed class InstanceSelectionGroupViewModel : ViewModelBase
{
    private bool _isExpanded;

    public InstanceSelectionGroupViewModel(string title, IReadOnlyList<InstanceSelectEntryViewModel> entries, bool isExpanded)
    {
        Title = title;
        Entries = entries;
        _isExpanded = isExpanded;
        ToggleExpandCommand = new ActionCommand(() => IsExpanded = !IsExpanded);
    }

    public string Title { get; }

    public IReadOnlyList<InstanceSelectEntryViewModel> Entries { get; }

    public int EntryCount => Entries.Count;

    public string HeaderText => Title == "收藏夹" ? Title : $"{Title} ({EntryCount})";

    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                RaisePropertyChanged(nameof(ChevronIconPath));
            }
        }
    }

    public string ChevronIconPath => IsExpanded
        ? "M256 640L512 384 768 640 704 704 512 512 320 704Z"
        : "M320 384L512 576 704 384 768 448 512 704 256 448Z";

    public ActionCommand ToggleExpandCommand { get; }
}

internal sealed class InstanceSelectionFolderEntryViewModel(
    string title,
    string path,
    bool isSelected,
    string iconPath,
    ActionCommand command,
    ActionCommand openFolderCommand)
{
    public string Title { get; } = title;

    public string Path { get; } = path;

    public bool IsSelected { get; } = isSelected;

    public string IconPath { get; } = iconPath;

    public ActionCommand Command { get; } = command;

    public ActionCommand OpenFolderCommand { get; } = openFolderCommand;
}

internal sealed class InstanceSelectionShortcutEntryViewModel(
    string title,
    string description,
    string iconPath,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string Description { get; } = description;

    public string IconPath { get; } = iconPath;

    public ActionCommand Command { get; } = command;
}

internal sealed class TaskManagerEntryViewModel(
    string title,
    TaskState taskState,
    string state,
    string summary,
    string progressText,
    double progressValue,
    bool hasProgress,
    string speedText,
    string remainingFilesText,
    int childCount,
    IReadOnlyList<TaskManagerStageEntryViewModel> stageEntries,
    bool canCancel,
    bool canPause,
    ActionCommand cancelCommand,
    ActionCommand pauseCommand)
{
    private static readonly IBrush RunningBadgeBackgroundBrush = Brush.Parse("#EDF5FF");
    private static readonly IBrush RunningBadgeBorderBrush = Brush.Parse("#CFE0FA");
    private static readonly IBrush RunningBadgeForegroundBrush = Brush.Parse("#5B87DA");
    private static readonly IBrush WaitingBadgeBackgroundBrush = Brush.Parse("#F4F7FB");
    private static readonly IBrush WaitingBadgeBorderBrush = Brush.Parse("#DAE3F0");
    private static readonly IBrush WaitingBadgeForegroundBrush = Brush.Parse("#7E8FA5");
    private static readonly IBrush SuccessBadgeBackgroundBrush = Brush.Parse("#EEF9F1");
    private static readonly IBrush SuccessBadgeBorderBrush = Brush.Parse("#CBE8D4");
    private static readonly IBrush SuccessBadgeForegroundBrush = Brush.Parse("#3D9B5A");
    private static readonly IBrush FailedBadgeBackgroundBrush = Brush.Parse("#FFF0F0");
    private static readonly IBrush FailedBadgeBorderBrush = Brush.Parse("#F1C8C8");
    private static readonly IBrush FailedBadgeForegroundBrush = Brush.Parse("#D05B5B");
    private static readonly IBrush CanceledBadgeBackgroundBrush = Brush.Parse("#F5F6F8");
    private static readonly IBrush CanceledBadgeBorderBrush = Brush.Parse("#D7DCE4");
    private static readonly IBrush CanceledBadgeForegroundBrush = Brush.Parse("#7B8796");

    public string Title { get; } = title;

    public TaskState TaskState { get; } = taskState;

    public string State { get; } = state;

    public string Summary { get; } = summary;

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool ShowSummary => HasSummary && !HasStageEntries;

    public string ProgressText { get; } = progressText;

    public double ProgressValue { get; } = Math.Clamp(progressValue, 0d, 1d) * 100d;

    public bool HasProgress => hasProgress;

    public string SpeedText { get; } = speedText;

    public string RemainingFilesText { get; } = remainingFilesText;

    public int ChildCount { get; } = childCount;

    public bool HasChildren => ChildCount > 0;

    public string ChildrenText => $"子任务 {ChildCount} 项";

    public IReadOnlyList<TaskManagerStageEntryViewModel> StageEntries { get; } = stageEntries;

    public bool HasStageEntries => StageEntries.Count > 0;

    public bool CanCancel { get; } = canCancel;

    public bool CanPause { get; } = canPause;

    public ActionCommand CancelCommand { get; } = cancelCommand;

    public ActionCommand PauseCommand { get; } = pauseCommand;

    public IBrush StateBadgeBackgroundBrush => TaskState switch
    {
        TaskState.Success => SuccessBadgeBackgroundBrush,
        TaskState.Failed => FailedBadgeBackgroundBrush,
        TaskState.Canceled => CanceledBadgeBackgroundBrush,
        TaskState.Waiting => WaitingBadgeBackgroundBrush,
        _ => RunningBadgeBackgroundBrush
    };

    public IBrush StateBadgeBorderBrush => TaskState switch
    {
        TaskState.Success => SuccessBadgeBorderBrush,
        TaskState.Failed => FailedBadgeBorderBrush,
        TaskState.Canceled => CanceledBadgeBorderBrush,
        TaskState.Waiting => WaitingBadgeBorderBrush,
        _ => RunningBadgeBorderBrush
    };

    public IBrush StateBadgeForegroundBrush => TaskState switch
    {
        TaskState.Success => SuccessBadgeForegroundBrush,
        TaskState.Failed => FailedBadgeForegroundBrush,
        TaskState.Canceled => CanceledBadgeForegroundBrush,
        TaskState.Waiting => WaitingBadgeForegroundBrush,
        _ => RunningBadgeForegroundBrush
    };
}

internal sealed class TaskManagerStageEntryViewModel(
    string indicator,
    string title,
    string message)
{
    private static readonly IBrush RunningIndicatorBrush = Brush.Parse("#4F86E9");
    private static readonly IBrush WaitingIndicatorBrush = Brush.Parse("#9AA8B8");
    private static readonly IBrush SuccessIndicatorBrush = Brush.Parse("#4F86E9");
    private static readonly IBrush FailedIndicatorBrush = Brush.Parse("#D05B5B");
    private static readonly IBrush CanceledIndicatorBrush = Brush.Parse("#9AA8B8");

    public string Indicator { get; } = indicator;

    public string Title { get; } = title;

    public string Message { get; } = message;

    public bool HasMessageDetail => !string.IsNullOrWhiteSpace(Message) && !string.Equals(Message, Title, StringComparison.Ordinal);

    public IBrush IndicatorBrush => ResolveIndicatorBrush();

    public double IndicatorFontSize => Indicator.EndsWith('%') ? 12.5 : 16d;

    private IBrush ResolveIndicatorBrush()
    {
        return Indicator switch
        {
            "✓" => SuccessIndicatorBrush,
            "×" => FailedIndicatorBrush,
            "···" => WaitingIndicatorBrush,
            _ when Indicator.EndsWith('%') => RunningIndicatorBrush,
            _ => CanceledIndicatorBrush
        };
    }
}

internal sealed record DedicatedGenericRouteMetadata(
    string Eyebrow,
    string Description,
    IReadOnlyList<LauncherFrontendPageFact> Facts);

internal sealed record InstanceSelectionFolderSnapshot(
    string Label,
    string Directory,
    string StoredPath);

internal sealed record InstanceSelectionSnapshot(
    string Name,
    string Subtitle,
    string Detail,
    IReadOnlyList<string> Tags,
    bool IsSelected,
    bool IsStarred,
    bool IsBroken,
    string Directory,
    int DisplayType,
    string VersionLabel,
    string? LoaderLabel,
    string? CustomInfo,
    bool IsCustomLogo,
    string RawLogoPath);

internal sealed record InstanceManifestSnapshot(
    string VersionLabel,
    string? LoaderLabel,
    bool IsBroken);
