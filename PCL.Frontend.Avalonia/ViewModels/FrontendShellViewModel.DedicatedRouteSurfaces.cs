using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private readonly HashSet<TaskModel> _observedTaskModels = [];
    private readonly Dictionary<TaskModel, TaskManagerEntryViewModel> _taskManagerEntryLookup = [];
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
    private DispatcherTimer? _taskManagerHeartbeatTimer;
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

    public bool HasSharedRouteSurface =>
        ShowGameLogSurface
        || ShowCompDetailSurface
        || ShowHelpDetailSurface;

    public bool ShowSharedRouteFallbackSurface => !HasSharedRouteSurface;

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

    public bool ShowGameLogLiveOutput => HasLaunchLogLines;

    public bool ShowGameLogEmptyState => !ShowGameLogLiveOutput && HasNoGameLogFiles;

    public int GameLogLiveLineCount => _launchLogLines.Count;

    public int GameLogRecentFileCount => _gameLogRecentFileCount;

    public string GameLogLatestUpdateLabel => _gameLogLatestUpdateLabel;

    private void InitializeStepOneSurfaces()
    {
        TaskCenter.Tasks.CollectionChanged += OnTaskCenterCollectionChanged;
        EnsureTaskManagerRefreshTimer();
        SyncTaskSubscriptions();
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
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var launcherDirectory = ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var selectedInstance = ReadRememberedLaunchInstanceName(localConfig);
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
        var now = DateTimeOffset.UtcNow;

        var tasks = TaskCenter.Tasks.ToArray();
        var orderedTasks = tasks
            .OrderByDescending(task => task.State == TaskState.Running)
            .ThenByDescending(task => task.State == TaskState.Waiting)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        _taskManagerWaitingCount = tasks.Count(task => task.State == TaskState.Waiting);
        _taskManagerRunningCount = tasks.Count(task => task.State == TaskState.Running);
        _taskManagerFinishedCount = tasks.Count(task => task.State == TaskState.Success || task.State == TaskState.Canceled);
        _taskManagerFailedCount = tasks.Count(task => task.State == TaskState.Failed);

        SyncTaskManagerEntries(orderedTasks, now);

        var primaryTask = orderedTasks.FirstOrDefault();
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
        UpdateTaskManagerHeartbeatState();

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
        var preferredLauncherLogPath = runtimePaths.ResolveCurrentLauncherLogFilePath();
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
                preferredLauncherLogPath ?? Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "PCL.log"),
                Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "RawOutput.log"),
                Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "latest.log"),
                Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log", "PCL.log")
            ]);
        var recentFiles = candidateFiles
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log")))
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "Log")))
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.FrontendArtifactDirectory, "crash-logs")))
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
            var localConfig = runtimePaths.OpenLocalConfigProvider();
            var sharedConfig = runtimePaths.OpenSharedConfigProvider();
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
                    StoreLauncherFolderPath(resolvedFolderPath, runtimePaths),
                    IsPersisted: true));
                PersistInstanceSelectionFolders(configuredFolders, runtimePaths);
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
            AddFailureActivity("添加已有文件夹失败", ex.Message);
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
                "*.rar");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                AddActivity("导入整合包", "未选择任何整合包文件。");
                return;
            }

            await StartInstanceSelectionPackInstallAsync(sourcePath);
        }
        catch (Exception ex)
        {
            AddFailureActivity("导入整合包失败", ex.Message);
        }
    }

    private async Task StartInstanceSelectionPackInstallAsync(string sourcePath)
    {
        var launcherDirectory = string.IsNullOrWhiteSpace(_instanceSelectionLauncherDirectory)
            ? ResolveLauncherFolder(
                ReadValue(_shellActionService.RuntimePaths.OpenLocalConfigProvider(), "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
                _shellActionService.RuntimePaths)
            : _instanceSelectionLauncherDirectory;
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        Directory.CreateDirectory(versionsDirectory);

        string? instanceName;
        try
        {
            var suggestion = SanitizeInstallDirectoryName(FrontendModpackInstallWorkflowService.SuggestInstanceName(sourcePath));
            instanceName = await PromptForCommunityProjectInstanceNameAsync(versionsDirectory, suggestion);
        }
        catch (Exception ex)
        {
            AddFailureActivity("输入实例名称失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(instanceName))
        {
            AddActivity("导入整合包", "没有输入实例名称。");
            return;
        }

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".zip";
        }

        var normalizedExtension = extension.ToLowerInvariant();
        var targetDirectory = Path.Combine(versionsDirectory, instanceName);
        var archivePath = Path.Combine(targetDirectory, $"原始整合包{normalizedExtension}");
        var taskTitle = $"整合包安装：{instanceName}";

        TaskCenter.Register(new FrontendManagedModpackInstallTask(
            taskTitle,
            new FrontendModpackInstallRequest(
                SourceUrl: null,
                SourceArchivePath: sourcePath,
                ArchivePath: archivePath,
                LauncherDirectory: launcherDirectory,
                InstanceName: instanceName,
                TargetDirectory: targetDirectory,
                ProjectId: null,
                ProjectSource: null,
                IconPath: null,
                ProjectDescription: null,
                CommunitySourcePreference: SelectedCommunityDownloadSourceIndex),
            ResolveDownloadRequestTimeout(),
            _shellActionService.GetDownloadTransferOptions(),
            onCompleted: result =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    HandleCommunityProjectModpackInstalled(result);
                });
            },
            onFailed: message =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AddFailureActivity("导入整合包失败", message);
                });
            }));
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
            $"{taskTitle} 已加入任务中心。");
    }

    private async Task DeleteInstanceSelectionFolderAsync(InstanceSelectionFolderSnapshot folder)
    {
        if (!folder.IsPersisted)
        {
            AddActivity("移除文件夹记录", "当前文件夹未保存到列表中。");
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            "移除文件夹确认",
            $"确定要从文件夹列表中移除 {folder.Directory} 吗？{Environment.NewLine}该操作只会删除列表记录，不会删除磁盘上的文件。",
            "从列表移除",
            isDanger: false);
        if (!confirmed)
        {
            AddActivity("移除文件夹记录", "已取消移除。");
            return;
        }

        try
        {
            var runtimePaths = _shellActionService.RuntimePaths;
            var localConfig = runtimePaths.OpenLocalConfigProvider();
            var sharedConfig = runtimePaths.OpenSharedConfigProvider();
            var currentStoredPath = ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw);
            var currentDirectory = ResolveLauncherFolder(currentStoredPath, runtimePaths);
            var configuredFolders = LoadConfiguredInstanceSelectionFolders(sharedConfig, localConfig, runtimePaths)
                .Where(candidate => !string.Equals(candidate.Directory, folder.Directory, GetPathComparison()))
                .ToList();

            PersistInstanceSelectionFolders(configuredFolders, runtimePaths);

            if (!string.Equals(currentDirectory, folder.Directory, GetPathComparison()))
            {
                RefreshInstanceSelectionSurface();
                RefreshInstanceSelectionRouteMetadata();
                AddActivity("移除文件夹记录", $"已从文件夹列表中移除 {folder.Directory}。");
                return;
            }

            var fallbackDirectory = ResolveNextInstanceSelectionFolder(configuredFolders, runtimePaths);
            if (string.Equals(fallbackDirectory, folder.Directory, GetPathComparison()))
            {
                RefreshInstanceSelectionSurface();
                RefreshInstanceSelectionRouteMetadata();
                AddActivity("移除文件夹记录", $"已移除 {folder.Directory} 的保存记录。当前仍在使用该目录，因此它会继续显示。");
                return;
            }

            RefreshSelectedLauncherFolderSmoothly(
                StoreLauncherFolderPath(fallbackDirectory, runtimePaths),
                fallbackDirectory,
                $"已从文件夹列表中移除 {folder.Directory}，并切换到 {fallbackDirectory}。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("移除文件夹记录失败", ex.Message);
        }
    }

    private void OpenInstanceSelectionFolder(InstanceSelectionFolderSnapshot folder)
    {
        if (string.IsNullOrWhiteSpace(folder.Directory))
        {
            AddFailureActivity("打开文件夹失败", "缺少可打开的文件夹路径。");
            return;
        }

        if (!Directory.Exists(folder.Directory))
        {
            AddFailureActivity("打开文件夹失败", $"文件夹不存在：{folder.Directory}");
            return;
        }

        if (_shellActionService.TryRevealExternalTarget(folder.Directory, out var error))
        {
            AddActivity("打开文件夹", folder.Directory);
            return;
        }

        AddFailureActivity("打开文件夹失败", error ?? folder.Directory);
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
            SetOptimisticLaunchInstanceName(entry.Name);
            var refreshVersion = System.Threading.Interlocked.Increment(ref _instanceSelectionRefreshVersion);
            QueueSelectedInstanceStateRefresh(refreshVersion);
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
            AddFailureActivity("切换实例收藏状态失败", ex.Message);
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

            AddActivity("删除实例", $"实例 {outcome.InstanceName} 已移入回收区：{outcome.TrashDirectory}");
        }
        catch (Exception ex)
        {
            AddFailureActivity("删除实例失败", ex.Message);
        }
    }

    private void ClearGameLogSurface()
    {
        ClearLaunchLogBuffer();
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

        var instanceConfig = FrontendRuntimePaths.OpenInstanceConfigProvider(directory);
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
                    AddFailureActivity("打开实例目录失败", error ?? entry.Directory);
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
            new ActionCommand(() => OpenInstanceSelectionFolder(folder)),
            folder.IsPersisted ? new ActionCommand(() => _ = DeleteInstanceSelectionFolderAsync(folder)) : null);
    }

    private static InstanceSelectionShortcutEntryViewModel CreateInstanceSelectionShortcutEntry(
        string title,
        string description,
        string iconPath,
        ActionCommand command)
    {
        return new InstanceSelectionShortcutEntryViewModel(title, description, iconPath, command);
    }

    private TaskManagerEntryViewModel CreateTaskManagerEntry(TaskModel task, DateTimeOffset now)
    {
        if (_taskManagerEntryLookup.TryGetValue(task, out var existingEntry))
        {
            UpdateTaskManagerEntry(existingEntry, task, now);
            return existingEntry;
        }

        var entry = new TaskManagerEntryViewModel(
            new ActionCommand(() =>
            {
                if ((task.State is TaskState.Waiting or TaskState.Running) && task.Cancel.CanExecute(null))
                {
                    task.Cancel.Execute(null);
                    return;
                }

                if (task.State is TaskState.Success or TaskState.Canceled or TaskState.Failed)
                {
                    TaskCenter.Remove(task);
                }
            }, () =>
                ((task.State is TaskState.Waiting or TaskState.Running) && task.Cancel.CanExecute(null)) ||
                task.State is TaskState.Success or TaskState.Canceled or TaskState.Failed),
            new ActionCommand(() => task.Pause.Execute(null), () => task.Pause.CanExecute(null)));
        _taskManagerEntryLookup[task] = entry;
        UpdateTaskManagerEntry(entry, task, now);
        return entry;
    }

    private static void UpdateTaskManagerEntry(TaskManagerEntryViewModel entry, TaskModel task, DateTimeOffset now)
    {
        var canCancel = (task.State is TaskState.Waiting or TaskState.Running) && task.Cancel.CanExecute(null);
        var canDismiss = task.State is TaskState.Success or TaskState.Canceled or TaskState.Failed;

        entry.Update(
            task.Title,
            task.State,
            MapTaskStateLabel(task.State),
            string.IsNullOrWhiteSpace(task.StateMessage) ? "等待状态消息" : task.StateMessage,
            BuildTaskActivityText(task, now),
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
            task.Children.Select(child => CreateTaskManagerStageEntry(child, now)).ToArray(),
            canCancel || canDismiss,
            task.Pause.CanExecute(null));
    }

    private void SyncTaskManagerEntries(IReadOnlyList<TaskModel> orderedTasks, DateTimeOffset now)
    {
        var activeTaskSet = orderedTasks.ToHashSet();
        foreach (var staleTask in _taskManagerEntryLookup.Keys.Where(task => !activeTaskSet.Contains(task)).ToArray())
        {
            _taskManagerEntryLookup.Remove(staleTask);
        }

        var desiredEntries = orderedTasks
            .Select(task => CreateTaskManagerEntry(task, now))
            .ToArray();
        var desiredEntrySet = desiredEntries.ToHashSet();

        for (var index = TaskManagerEntries.Count - 1; index >= 0; index--)
        {
            if (!desiredEntrySet.Contains(TaskManagerEntries[index]))
            {
                TaskManagerEntries.RemoveAt(index);
            }
        }

        for (var index = 0; index < desiredEntries.Length; index++)
        {
            var desiredEntry = desiredEntries[index];
            if (index < TaskManagerEntries.Count && ReferenceEquals(TaskManagerEntries[index], desiredEntry))
            {
                continue;
            }

            var existingIndex = TaskManagerEntries.IndexOf(desiredEntry);
            if (existingIndex >= 0)
            {
                TaskManagerEntries.Move(existingIndex, index);
                continue;
            }

            TaskManagerEntries.Insert(index, desiredEntry);
        }

        while (TaskManagerEntries.Count > desiredEntries.Length)
        {
            TaskManagerEntries.RemoveAt(TaskManagerEntries.Count - 1);
        }
    }

    private static TaskManagerStageEntryViewModel CreateTaskManagerStageEntry(TaskModel task, DateTimeOffset now)
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
        var activityText = BuildStageActivityText(task, now);
        if (!string.IsNullOrWhiteSpace(activityText))
        {
            message = $"{message} • {activityText}";
        }

        return new TaskManagerStageEntryViewModel(indicator, task.Title, message);
    }

    private static string BuildTaskActivityText(TaskModel task, DateTimeOffset now)
    {
        var activeDuration = now - task.StateSince;
        var recentDuration = now - task.LastUpdatedAt;

        return task.State switch
        {
            TaskState.Running => $"已运行 {FormatTaskDuration(now - (task.StartedAt ?? task.StateSince))}，最近更新 {FormatRecentActivity(recentDuration)}",
            TaskState.Waiting => $"已等待 {FormatTaskDuration(activeDuration)}",
            TaskState.Success => $"总耗时 {FormatTaskDuration((task.FinishedAt ?? now) - (task.StartedAt ?? task.CreatedAt))}",
            TaskState.Canceled => $"运行 {FormatTaskDuration((task.FinishedAt ?? now) - (task.StartedAt ?? task.CreatedAt))} 后已取消",
            TaskState.Failed => $"运行 {FormatTaskDuration((task.FinishedAt ?? now) - (task.StartedAt ?? task.CreatedAt))} 后失败",
            _ => string.Empty
        };
    }

    private static string BuildStageActivityText(TaskModel task, DateTimeOffset now)
    {
        return task.State switch
        {
            TaskState.Running => $"已持续 {FormatTaskDuration(now - task.StateSince)}",
            TaskState.Waiting => "等待开始",
            TaskState.Success => $"耗时 {FormatTaskDuration((task.FinishedAt ?? now) - task.StateSince)}",
            TaskState.Canceled => $"已取消，持续 {FormatTaskDuration((task.FinishedAt ?? now) - task.StateSince)}",
            TaskState.Failed => $"已失败，持续 {FormatTaskDuration((task.FinishedAt ?? now) - task.StateSince)}",
            _ => string.Empty
        };
    }

    private static string FormatTaskDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration.TotalHours >= 1d)
        {
            return $"{(int)duration.TotalHours} 小时 {duration.Minutes} 分";
        }

        if (duration.TotalMinutes >= 1d)
        {
            return $"{(int)duration.TotalMinutes} 分 {duration.Seconds} 秒";
        }

        return $"{Math.Max(1, duration.Seconds)} 秒";
    }

    private static string FormatRecentActivity(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(2))
        {
            return "刚刚";
        }

        if (duration.TotalMinutes >= 1d)
        {
            return $"{(int)duration.TotalMinutes} 分钟前";
        }

        return $"{Math.Max(1, duration.Seconds)} 秒前";
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
            _taskManagerEntryLookup.Remove(staleTask);
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

    private void UpdateTaskManagerHeartbeatState()
    {
        EnsureTaskManagerHeartbeatTimer();
        if (_taskManagerRunningCount > 0)
        {
            _taskManagerHeartbeatTimer!.Start();
            return;
        }

        _taskManagerHeartbeatTimer?.Stop();
    }

    private void EnsureTaskManagerHeartbeatTimer()
    {
        if (_taskManagerHeartbeatTimer is not null)
        {
            return;
        }

        _taskManagerHeartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _taskManagerHeartbeatTimer.Tick += (_, _) =>
        {
            if (TaskCenter.Tasks.All(task => task.State != TaskState.Running))
            {
                _taskManagerHeartbeatTimer?.Stop();
                return;
            }

            RefreshTaskManagerSurface();
        };
    }

    private static InstanceManifestSnapshot ParseInstanceManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return new InstanceManifestSnapshot("Unknown", null, true);
        }

        var profile = FrontendVersionManifestInspector.ReadProfileFromManifestPath(manifestPath);
        return new InstanceManifestSnapshot(
            profile.VanillaVersion,
            profile.PrimaryLoaderName,
            !profile.IsManifestValid);
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

    private static IReadOnlyList<InstanceSelectionFolderSnapshot> BuildInstanceSelectionFolderSnapshots(
        IKeyValueFileProvider sharedConfig,
        IKeyValueFileProvider localConfig,
        FrontendRuntimePaths runtimePaths,
        string selectedDirectory)
    {
        var folders = LoadConfiguredInstanceSelectionFolders(sharedConfig, localConfig, runtimePaths).ToList();
        var seenDirectories = folders
            .Select(folder => folder.Directory)
            .ToHashSet(GetPathComparer());

        if (seenDirectories.Add(selectedDirectory))
        {
            folders.Insert(0, new InstanceSelectionFolderSnapshot(
                GetInstanceSelectionDirectoryLabel(selectedDirectory),
                selectedDirectory,
                StoreLauncherFolderPath(selectedDirectory, runtimePaths),
                IsPersisted: false));
        }

        return folders;
    }

    private static IReadOnlyList<InstanceSelectionFolderSnapshot> LoadConfiguredInstanceSelectionFolders(
        IKeyValueFileProvider sharedConfig,
        IKeyValueFileProvider localConfig,
        FrontendRuntimePaths runtimePaths)
    {
        var rawFolders = ReadValue(sharedConfig, "LaunchFolders", string.Empty);
        if (string.IsNullOrWhiteSpace(rawFolders))
        {
            rawFolders = ReadValue(localConfig, "LaunchFolders", string.Empty);
        }

        var folders = new List<InstanceSelectionFolderSnapshot>();
        var seenDirectories = new HashSet<string>(GetPathComparer());
        foreach (var rawEntry in rawFolders.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var folder = ParseInstanceSelectionFolderSnapshot(rawEntry, runtimePaths);
            if (folder is null || !seenDirectories.Add(folder.Directory))
            {
                continue;
            }

            folders.Add(folder);
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
            rawPath,
            IsPersisted: true);
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

    private void PersistInstanceSelectionFolders(
        IReadOnlyList<InstanceSelectionFolderSnapshot> folders,
        FrontendRuntimePaths runtimePaths)
    {
        _shellActionService.PersistSharedValue("LaunchFolders", SerializeInstanceSelectionFolders(folders, runtimePaths));
        _shellActionService.RemoveLocalValues(["LaunchFolders"]);
    }

    private static string ResolveNextInstanceSelectionFolder(
        IReadOnlyList<InstanceSelectionFolderSnapshot> configuredFolders,
        FrontendRuntimePaths runtimePaths)
    {
        if (configuredFolders.Count > 0)
        {
            return configuredFolders[0].Directory;
        }

        return ResolveLauncherFolder(FrontendLauncherPathService.DefaultLauncherFolderRaw, runtimePaths);
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

    public IBrush FavoriteIconBrush => FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3");

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
    ActionCommand openFolderCommand,
    ActionCommand? deleteCommand)
{
    public string Title { get; } = title;

    public string Path { get; } = path;

    public bool IsSelected { get; } = isSelected;

    public string IconPath { get; } = iconPath;

    public ActionCommand Command { get; } = command;

    public string OpenFolderIconData => FrontendIconCatalog.OpenFolder.Data;

    public string OpenFolderToolTip => "打开对应文件夹";

    public ActionCommand OpenFolderCommand { get; } = openFolderCommand;

    public string DeleteIconData => FrontendIconCatalog.DeleteOutline.Data;

    public string DeleteToolTip => "从列表中移除";

    public ActionCommand? DeleteCommand { get; } = deleteCommand;
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
    ActionCommand primaryActionCommand,
    ActionCommand pauseCommand) : ViewModelBase
{
    private string _title = string.Empty;
    private TaskState _taskState;
    private string _state = string.Empty;
    private string _summary = string.Empty;
    private string _activityText = string.Empty;
    private string _progressText = string.Empty;
    private double _progressValue;
    private bool _hasProgress;
    private string _speedText = string.Empty;
    private string _remainingFilesText = string.Empty;
    private int _childCount;
    private IReadOnlyList<TaskManagerStageEntryViewModel> _stageEntries = [];
    private bool _hasPrimaryAction;
    private bool _canPause;

    public string Title => _title;

    public TaskState TaskState => _taskState;

    public string State => _state;

    public string Summary => _summary;

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool ShowSummary => HasSummary && !HasStageEntries;

    public string ActivityText => _activityText;

    public bool HasActivityText => !string.IsNullOrWhiteSpace(ActivityText);

    public string ProgressText => _progressText;

    public double ProgressValue => _progressValue;

    public bool HasProgress => _hasProgress;

    public string SpeedText => _speedText;

    public string RemainingFilesText => _remainingFilesText;

    public int ChildCount => _childCount;

    public bool HasChildren => ChildCount > 0;

    public string ChildrenText => $"子任务 {ChildCount} 项";

    public IReadOnlyList<TaskManagerStageEntryViewModel> StageEntries => _stageEntries;

    public bool HasStageEntries => StageEntries.Count > 0;

    public bool HasPrimaryAction => _hasPrimaryAction;

    public bool CanPause => _canPause;

    public ActionCommand PrimaryActionCommand { get; } = primaryActionCommand;

    public ActionCommand PauseCommand { get; } = pauseCommand;

    public IBrush StateBadgeBackgroundBrush => TaskState switch
    {
        TaskState.Success => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticSuccessBackground", "#EAF7F4"),
        TaskState.Failed => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticErrorBackground", "#FFF0F0"),
        TaskState.Canceled => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralBackground", "#F4F7FB"),
        TaskState.Waiting => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralBackground", "#F4F7FB"),
        _ => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoBackground", "#EDF5FF")
    };

    public IBrush StateBadgeBorderBrush => TaskState switch
    {
        TaskState.Success => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticSuccessBorder", "#C8E6DF"),
        TaskState.Failed => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticErrorBorder", "#F1C8C8"),
        TaskState.Canceled => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralBorder", "#DAE3F0"),
        TaskState.Waiting => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralBorder", "#DAE3F0"),
        _ => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoBorder", "#CFE0FA")
    };

    public IBrush StateBadgeForegroundBrush => TaskState switch
    {
        TaskState.Success => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticSuccessForeground", "#24534E"),
        TaskState.Failed => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticErrorForeground", "#D05B5B"),
        TaskState.Canceled => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralForeground", "#7E8FA5"),
        TaskState.Waiting => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralForeground", "#7E8FA5"),
        _ => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoForeground", "#5B87DA")
    };

    public void Update(
        string title,
        TaskState taskState,
        string state,
        string summary,
        string activityText,
        string progressText,
        double progressValue,
        bool hasProgress,
        string speedText,
        string remainingFilesText,
        int childCount,
        IReadOnlyList<TaskManagerStageEntryViewModel> stageEntries,
        bool hasPrimaryAction,
        bool canPause)
    {
        SetProperty(ref _title, title, nameof(Title));

        if (SetProperty(ref _taskState, taskState, nameof(TaskState)))
        {
            RaisePropertyChanged(nameof(StateBadgeBackgroundBrush));
            RaisePropertyChanged(nameof(StateBadgeBorderBrush));
            RaisePropertyChanged(nameof(StateBadgeForegroundBrush));
        }

        SetProperty(ref _state, state, nameof(State));

        if (SetProperty(ref _summary, summary, nameof(Summary)))
        {
            RaisePropertyChanged(nameof(HasSummary));
            RaisePropertyChanged(nameof(ShowSummary));
        }

        if (SetProperty(ref _activityText, activityText, nameof(ActivityText)))
        {
            RaisePropertyChanged(nameof(HasActivityText));
        }

        SetProperty(ref _progressText, progressText, nameof(ProgressText));
        SetProperty(ref _progressValue, Math.Clamp(progressValue, 0d, 1d) * 100d, nameof(ProgressValue));
        SetProperty(ref _hasProgress, hasProgress, nameof(HasProgress));
        SetProperty(ref _speedText, speedText, nameof(SpeedText));
        SetProperty(ref _remainingFilesText, remainingFilesText, nameof(RemainingFilesText));

        if (SetProperty(ref _childCount, childCount, nameof(ChildCount)))
        {
            RaisePropertyChanged(nameof(HasChildren));
            RaisePropertyChanged(nameof(ChildrenText));
        }

        if (SetProperty(ref _stageEntries, stageEntries, nameof(StageEntries)))
        {
            RaisePropertyChanged(nameof(HasStageEntries));
            RaisePropertyChanged(nameof(ShowSummary));
        }

        SetProperty(ref _hasPrimaryAction, hasPrimaryAction, nameof(HasPrimaryAction));
        SetProperty(ref _canPause, canPause, nameof(CanPause));

        PrimaryActionCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
    }
}

internal sealed class TaskManagerStageEntryViewModel(
    string indicator,
    string title,
    string message)
{
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
            "✓" => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticSuccessForeground", "#24534E"),
            "×" => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticErrorForeground", "#D05B5B"),
            "···" => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralForeground", "#7E8FA5"),
            _ when Indicator.EndsWith('%') => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoForeground", "#5B87DA"),
            _ => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralForeground", "#7E8FA5")
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
    string StoredPath,
    bool IsPersisted);

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
