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
    private string _taskManagerActiveTaskTitle = string.Empty;
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
    private string _gameLogLatestUpdateLabel = string.Empty;

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

    public string InstanceSelectionSearchToolTip => LT("shell.instance_select.search.tooltip");

    public string InstanceSelectionSearchWatermark => LT("shell.instance_select.search.watermark");

    public string InstanceSelectionEmptyTitle => _instanceSelectionTotalCount == 0
        ? LT("shell.instance_select.empty.none_title")
        : LT("shell.instance_select.empty.no_match_title");

    public string InstanceSelectionEmptyDescription => _instanceSelectionTotalCount == 0
        ? LT("shell.instance_select.empty.none_description")
        : LT("shell.instance_select.empty.no_match_description", ("query", InstanceSelectionSearchQuery.Trim()));

    public string InstanceSelectionEmptyDownloadButtonText => LT("shell.instance_select.empty.download");

    public string InstanceSelectionFoldersHeader => LT("shell.instance_select.left.folders");

    public string InstanceSelectionShortcutsHeader => LT("shell.instance_select.left.shortcuts");

    public string InstanceSelectionLauncherDirectoryLabel => GetInstanceSelectionDirectoryLabel(_instanceSelectionLauncherDirectory);

    public string InstanceSelectionLauncherDirectoryPath => _instanceSelectionLauncherDirectory;

    public string InstanceSelectionLauncherDirectory => _instanceSelectionLauncherDirectory;

    public string InstanceSelectionResultSummary => HasInstanceSelectionEntries
        ? LT("shell.instance_select.results.count", ("count", InstanceSelectionEntries.Count))
        : _instanceSelectionTotalCount == 0
            ? LT("shell.instance_select.results.none")
            : LT("shell.instance_select.results.filtered_none");

    public bool ShowTaskManagerSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.TaskManager;

    public bool HasTaskManagerEntries => TaskManagerEntries.Count > 0;

    public bool HasNoTaskManagerEntries => !HasTaskManagerEntries;

    public string TaskManagerEmptyTitle => LT("shell.task_manager.empty.title");

    public string TaskManagerEmptyDescription => LT("shell.task_manager.empty.description");

    public string TaskManagerLeftOverallProgressLabel => LT("shell.task_manager.left.overall_progress");

    public string TaskManagerLeftDownloadSpeedLabel => LT("shell.task_manager.left.download_speed");

    public string TaskManagerLeftRemainingFilesLabel => LT("shell.task_manager.left.remaining_files");

    public int TaskManagerWaitingCount => _taskManagerWaitingCount;

    public int TaskManagerRunningCount => _taskManagerRunningCount;

    public int TaskManagerFinishedCount => _taskManagerFinishedCount;

    public int TaskManagerFailedCount => _taskManagerFailedCount;

    public string TaskManagerSummary => HasTaskManagerEntries
        ? LT("shell.task_manager.summary.active", ("running", TaskManagerRunningCount), ("waiting", TaskManagerWaitingCount))
        : LT("shell.task_manager.summary.none");

    public string TaskManagerActiveTaskTitle => _taskManagerActiveTaskTitle;

    public double TaskManagerOverallProgress => _taskManagerOverallProgress;

    public double TaskManagerOverallProgressValue => _taskManagerOverallProgress * 100d;

    public string TaskManagerOverallProgressText => $"{Math.Round(TaskManagerOverallProgress * 100, 1, MidpointRounding.AwayFromZero)} %";

    public string TaskManagerDownloadSpeedText => _taskManagerDownloadSpeedText;

    public string TaskManagerRemainingFilesText => _taskManagerRemainingFilesText;

    public string GameLogRefreshButtonText => LT("shell.game_log.actions.refresh");

    public string GameLogExportButtonText => LT("shell.game_log.actions.export");

    public string GameLogOpenDirectoryButtonText => LT("shell.game_log.actions.open_directory");

    public string GameLogClearCacheButtonText => LT("shell.game_log.actions.clear_cache");

    public string GameLogLiveLineCountLabel => LT("shell.game_log.labels.live_line_count");

    public string GameLogRecentFilesLabel => LT("shell.game_log.labels.recent_files");

    public string GameLogCurrentSessionOutputTitle => LT("shell.game_log.labels.current_session_output");

    public string GameLogRecentGeneratedFilesTitle => LT("shell.game_log.labels.recent_generated_files");

    public string GameLogEmptyTitle => LT("shell.game_log.empty.title");

    public string GameLogEmptyDescription => LT("shell.game_log.empty.description");

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
                    LT("shell.navigation.pages.instance_select.title"),
                    LT("shell.instance_select.route.description"),
                    [
                        new LauncherFrontendPageFact(
                            LT("shell.instance_select.route.facts.launch_directory"),
                            string.IsNullOrWhiteSpace(_instanceSelectionLauncherDirectory)
                                ? LT("shell.instance_select.route.values.unresolved")
                                : _instanceSelectionLauncherDirectory),
                        new LauncherFrontendPageFact(
                            LT("shell.instance_select.route.facts.selected_instance"),
                            _instanceComposition.Selection.HasSelection
                                ? _instanceComposition.Selection.InstanceName
                                : LT("shell.instance_select.route.values.none_selected")),
                        new LauncherFrontendPageFact(
                            LT("shell.instance_select.route.facts.result_count"),
                            $"{InstanceSelectionEntries.Count} / {_instanceSelectionTotalCount}")
                    ]);
                return true;
            case LauncherFrontendPageKey.TaskManager:
                metadata = new DedicatedGenericRouteMetadata(
                    LT("shell.navigation.pages.task_manager.title"),
                    LT("shell.task_manager.route.description"),
                    [
                        new LauncherFrontendPageFact(LT("shell.task_manager.route.facts.waiting"), TaskManagerWaitingCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.task_manager.route.facts.running"), TaskManagerRunningCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.task_manager.route.facts.finished"), TaskManagerFinishedCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.task_manager.route.facts.failed"), TaskManagerFailedCount.ToString())
                    ]);
                return true;
            case LauncherFrontendPageKey.GameLog:
                metadata = new DedicatedGenericRouteMetadata(
                    LT("shell.navigation.pages.game_log.title"),
                    LT("shell.game_log.route.description"),
                    [
                        new LauncherFrontendPageFact(LT("shell.game_log.route.facts.live_lines"), GameLogLiveLineCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.game_log.route.facts.recent_files"), GameLogRecentFileCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.game_log.route.facts.latest_update"), GameLogLatestUpdateLabel)
                    ]);
                return true;
            case LauncherFrontendPageKey.CompDetail:
                metadata = new DedicatedGenericRouteMetadata(
                    LT("shell.comp_detail.route.eyebrow"),
                    LT("shell.comp_detail.route.description"),
                    [
                        new LauncherFrontendPageFact(LT("shell.comp_detail.route.facts.source"), CommunityProjectSource),
                        new LauncherFrontendPageFact(LT("shell.comp_detail.route.facts.status"), CommunityProjectStatus),
                        new LauncherFrontendPageFact(LT("shell.comp_detail.route.facts.updated"), CommunityProjectUpdatedLabel),
                        new LauncherFrontendPageFact(LT("shell.comp_detail.route.facts.downloads"), CommunityProjectDownloadCountLabel)
                    ]);
                return true;
            case LauncherFrontendPageKey.HelpDetail:
                metadata = new DedicatedGenericRouteMetadata(
                    HelpDetailTitle,
                    LT("shell.help_detail.route.description"),
                    [
                        new LauncherFrontendPageFact(LT("shell.help_detail.route.facts.source"), HelpDetailSource),
                        new LauncherFrontendPageFact(LT("shell.help_detail.route.facts.lines"), HelpDetailSections.Sum(section => section.Lines.Count).ToString()),
                        new LauncherFrontendPageFact(LT("shell.help_detail.route.facts.actions"), HelpDetailSections.Sum(section => section.Actions.Count).ToString())
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
                    LT("shell.instance_select.shortcuts.add_folder.title"),
                    LT("shell.instance_select.shortcuts.add_folder.description"),
                    "F1 m 12 7 a 1 1 0 0 0 -1 1 v 8 a 1 1 0 0 0 1 1 a 1 1 0 0 0 1 -1 V 8 A 1 1 0 0 0 12 7 Z m -4 4 a 1 1 0 0 0 -1 1 a 1 1 0 0 0 1 1 h 8 a 1 1 0 0 0 1 -1 a 1 1 0 0 0 -1 -1 z M 12 1 C 5.93671 1 1 5.93671 1 12 C 1 18.0633 5.93671 23 12 23 C 18.0633 23 23 18.0633 23 12 C 23 5.93671 18.0633 1 12 1 Z m 0 2 c 4.98241 0 9 4.01759 9 9 c 0 4.98241 -4.01759 9 -9 9 C 7.01759 21 3 16.9824 3 12 C 3 7.01759 7.01759 3 12 3 Z",
                    _addInstanceSelectionFolderCommand),
                CreateInstanceSelectionShortcutEntry(
                    LT("shell.instance_select.shortcuts.import_pack.title"),
                    LT("shell.instance_select.shortcuts.import_pack.description"),
                    "F1 m 11.293 11.293 l -3 3 a 1 1 0 0 0 0 1.41406 a 1 1 0 0 0 1.41406 0 L 12 13.4141 l 2.29297 2.29297 a 1 1 0 0 0 1.41406 0 a 1 1 0 0 0 0 -1.41406 l -3 -3 a 1.0001 1.0001 0 0 0 -1.41406 0 z M 12 11 a 1 1 0 0 0 -1 1 v 6 a 1 1 0 0 0 1 1 a 1 1 0 0 0 1 -1 V 12 A 1 1 0 0 0 12 11 Z M 14 1 a 1 1 0 0 0 -1 1 v 5 c 0 1.09272 0.907275 2 2 2 h 5 A 1 1 0 0 0 21 8 A 1 1 0 0 0 20 7 H 15 V 2 A 1 1 0 0 0 14 1 Z M 6 1 C 4.35499 1 3 2.35499 3 4 v 16 c 0 1.64501 1.35499 3 3 3 h 12 c 1.64501 0 3 -1.35499 3 -3 V 8.00195 V 8 C 21.001 7.09394 20.6387 6.22279 19.9961 5.58398 L 16.4121 2 L 16.4101 1.99805 C 15.7718 1.35838 14.9038 0.999054 14 1 Z m 0 2 h 8 a 1.0001 1.0001 0 0 0 0.002 0 c 0.373356 -0.0006051 0.730614 0.147632 0.994141 0.412109 a 1.0001 1.0001 0 0 0 0 0.00195 l 3.58789 3.58789 a 1.0001 1.0001 0 0 0 0.0039 0.00195 C 18.8531 7.26753 19.0006 7.62412 19 7.99805 A 1.0001 1.0001 0 0 0 19 8 v 12 c 0 0.564129 -0.435871 1 -1 1 H 6 C 5.43587 21 5 20.5641 5 20 V 4 C 5 3.43587 5.43587 3 6 3 Z",
                    _importInstanceSelectionPackCommand),
                CreateInstanceSelectionShortcutEntry(
                    LT("shell.instance_select.shortcuts.trash.title"),
                    LT("shell.instance_select.shortcuts.trash.description"),
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
        RaisePropertyChanged(nameof(InstanceSelectionSearchToolTip));
        RaisePropertyChanged(nameof(InstanceSelectionSearchWatermark));
        RaisePropertyChanged(nameof(HasInstanceSelectionEntries));
        RaisePropertyChanged(nameof(HasNoInstanceSelectionEntries));
        RaisePropertyChanged(nameof(InstanceSelectionResultSummary));
        RaisePropertyChanged(nameof(InstanceSelectionEmptyTitle));
        RaisePropertyChanged(nameof(InstanceSelectionEmptyDescription));
        RaisePropertyChanged(nameof(ShowInstanceSelectionEmptyDownloadAction));
        RaisePropertyChanged(nameof(ShowInstanceSelectionEmptyClearAction));
        RaisePropertyChanged(nameof(InstanceSelectionEmptyDownloadButtonText));
        RaisePropertyChanged(nameof(InstanceSelectionFoldersHeader));
        RaisePropertyChanged(nameof(InstanceSelectionShortcutsHeader));
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
        _taskManagerActiveTaskTitle = primaryTask?.Title ?? LT("shell.task_manager.summary.none");
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
        RaisePropertyChanged(nameof(TaskManagerEmptyTitle));
        RaisePropertyChanged(nameof(TaskManagerEmptyDescription));
        RaisePropertyChanged(nameof(TaskManagerLeftOverallProgressLabel));
        RaisePropertyChanged(nameof(TaskManagerLeftDownloadSpeedLabel));
        RaisePropertyChanged(nameof(TaskManagerLeftRemainingFilesLabel));
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
                    runtimePaths.DataDirectory,
                    _shellActionService.PlatformAdapter))
            .Concat(
            [
                Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "RawOutput.log"),
                Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "latest.log"),
                preferredLauncherLogPath ?? Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "PCL.log"),
                Path.Combine(runtimePaths.DataDirectory, "Log", "RawOutput.log"),
                Path.Combine(runtimePaths.DataDirectory, "Log", "latest.log"),
                Path.Combine(runtimePaths.DataDirectory, "Log", "PCL.log")
            ]);
        var recentFiles = candidateFiles
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log")))
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.DataDirectory, "Log")))
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
            : LT("shell.game_log.latest_update.none");

        ReplaceItems(
            GameLogFileEntries,
            recentFiles.Select(file =>
                new SimpleListEntryViewModel(
                    file.Name,
                    $"{file.DirectoryName} • {file.LastWriteTime:yyyy-MM-dd HH:mm}",
                    CreateOpenTargetCommand(
                        LT("shell.game_log.actions.open_file", ("name", file.Name)),
                        file.FullName,
                        file.FullName))));

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
            LT("shell.instance_select.navigation.launch", ("name", entry.Name)));
    }

    private async Task AddInstanceSelectionFolderAsync()
    {
        try
        {
            var pickedFolderPath = await _shellActionService.PickFolderAsync(LT("shell.instance_select.shortcuts.add_folder.pick_title"));
            if (string.IsNullOrWhiteSpace(pickedFolderPath))
            {
                AddActivity(
                    LT("shell.instance_select.shortcuts.add_folder.title"),
                    LT("shell.instance_select.shortcuts.add_folder.cancelled"));
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
                ? LT("shell.instance_select.shortcuts.add_folder.added_and_switched", ("path", resolvedFolderPath))
                : LT("shell.instance_select.shortcuts.add_folder.switched", ("path", resolvedFolderPath)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.shortcuts.add_folder.failure"), ex.Message);
        }
    }

    private async Task ImportInstanceSelectionPackAsync()
    {
        try
        {
            var sourcePath = await _shellActionService.PickOpenFileAsync(
                LT("shell.instance_select.shortcuts.import_pack.pick_title"),
                LT("shell.instance_select.shortcuts.import_pack.pick_filter"),
                "*.zip",
                "*.mrpack",
                "*.rar");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                AddActivity(
                    LT("shell.instance_select.shortcuts.import_pack.title"),
                    LT("shell.instance_select.shortcuts.import_pack.cancelled"));
                return;
            }

            await StartInstanceSelectionPackInstallAsync(sourcePath);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.shortcuts.import_pack.failure"), ex.Message);
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
            AddFailureActivity(LT("shell.instance_select.shortcuts.import_pack.name_failure"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(instanceName))
        {
            AddActivity(
                LT("shell.instance_select.shortcuts.import_pack.title"),
                LT("shell.instance_select.shortcuts.import_pack.no_name"));
            return;
        }

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".zip";
        }

        var normalizedExtension = extension.ToLowerInvariant();
        var targetDirectory = Path.Combine(versionsDirectory, instanceName);
        var archivePath = Path.Combine(targetDirectory, $"original-modpack{normalizedExtension}");
        var taskTitle = LT("shell.instance_select.tasks.install_modpack", ("name", instanceName));

        TaskCenter.Register(new FrontendManagedModpackInstallTask(
            taskTitle,
            new FrontendModpackInstallRequest(
                SourceUrl: null,
                SourceArchivePath: sourcePath,
                ArchivePath: archivePath,
                LauncherDirectory: launcherDirectory,
                DownloadSourceIndex: SelectedDownloadSourceIndex,
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
                    AddFailureActivity(LT("shell.instance_select.shortcuts.import_pack.failure"), message);
                });
            }));
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
            LT("shell.instance_select.shortcuts.import_pack.task_queued", ("task", taskTitle)));
    }

    private async Task DeleteInstanceSelectionFolderAsync(InstanceSelectionFolderSnapshot folder)
    {
        if (!folder.IsPersisted)
        {
            AddActivity(
                LT("shell.instance_select.folder_remove.activity"),
                LT("shell.instance_select.folder_remove.not_saved"));
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            LT("shell.instance_select.folder_remove.confirm_title"),
            LT("shell.instance_select.folder_remove.confirm_message", ("path", folder.Directory), ("newline", Environment.NewLine)),
            LT("shell.instance_select.folder_remove.confirm_action"),
            isDanger: false);
        if (!confirmed)
        {
            AddActivity(
                LT("shell.instance_select.folder_remove.activity"),
                LT("shell.instance_select.folder_remove.cancelled"));
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
                AddActivity(
                    LT("shell.instance_select.folder_remove.activity"),
                    LT("shell.instance_select.folder_remove.removed", ("path", folder.Directory)));
                return;
            }

            var fallbackDirectory = ResolveNextInstanceSelectionFolder(configuredFolders, runtimePaths);
            if (string.Equals(fallbackDirectory, folder.Directory, GetPathComparison()))
            {
                RefreshInstanceSelectionSurface();
                RefreshInstanceSelectionRouteMetadata();
                AddActivity(
                    LT("shell.instance_select.folder_remove.activity"),
                    LT("shell.instance_select.folder_remove.still_active", ("path", folder.Directory)));
                return;
            }

            RefreshSelectedLauncherFolderSmoothly(
                StoreLauncherFolderPath(fallbackDirectory, runtimePaths),
                fallbackDirectory,
                LT("shell.instance_select.folder_remove.removed_and_switched", ("removed", folder.Directory), ("target", fallbackDirectory)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.folder_remove.failure"), ex.Message);
        }
    }

    private void OpenInstanceSelectionFolder(InstanceSelectionFolderSnapshot folder)
    {
        if (string.IsNullOrWhiteSpace(folder.Directory))
        {
            AddFailureActivity(
                LT("shell.instance_select.open_folder.failure"),
                LT("shell.instance_select.open_folder.missing_path"));
            return;
        }

        if (!Directory.Exists(folder.Directory))
        {
            AddFailureActivity(
                LT("shell.instance_select.open_folder.failure"),
                LT("shell.instance_select.open_folder.not_found", ("path", folder.Directory)));
            return;
        }

        if (_shellActionService.TryRevealExternalTarget(folder.Directory, out var error))
        {
            AddActivity(LT("shell.instance_select.open_folder.activity"), folder.Directory);
            return;
        }

        AddFailureActivity(LT("shell.instance_select.open_folder.failure"), error ?? folder.Directory);
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
            LT("shell.instance_select.navigation.overview", ("name", entry.Name)));
    }

    private void ToggleInstanceSelectionFavorite(InstanceSelectionSnapshot entry)
    {
        try
        {
            var nextIsFavorite = !entry.IsStarred;
            _shellActionService.PersistInstanceValue(entry.Directory, "IsStar", nextIsFavorite);
            RefreshInstanceSelectionSurface();
            AddActivity(
                nextIsFavorite
                    ? LT("shell.instance_select.favorite.added_activity")
                    : LT("shell.instance_select.favorite.removed_activity"),
                entry.Name);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.favorite.failure"), ex.Message);
        }
    }

    private async Task DeleteInstanceSelectionEntryAsync(InstanceSelectionSnapshot entry)
    {
        var confirmed = await _shellActionService.ConfirmAsync(
            LT("shell.instance_select.delete.confirm_title"),
            LT("shell.instance_select.delete.confirm_message", ("name", entry.Name)),
            LT("shell.instance_select.delete.confirm_action"),
            isDanger: true);
        if (!confirmed)
        {
            AddActivity(LT("shell.instance_select.delete.activity"), LT("shell.instance_select.delete.cancelled"));
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
                AddActivity(
                    LT("shell.instance_select.delete.activity"),
                    LT("shell.instance_select.delete.permanently_deleted", ("name", outcome.InstanceName)));
                return;
            }

            AddActivity(
                LT("shell.instance_select.delete.activity"),
                LT("shell.instance_select.delete.moved_to_trash", ("name", outcome.InstanceName), ("path", outcome.TrashDirectory)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.delete.failure"), ex.Message);
        }
    }

    private void ClearGameLogSurface()
    {
        ClearLaunchLogBuffer();
        RaiseGameLogSurfaceProperties();
        AddActivity(LT("shell.game_log.actions.clear_cache"), LT("shell.game_log.clear_cache.completed"));
    }

    private void RaiseGameLogSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowGameLogLiveOutput));
        RaisePropertyChanged(nameof(ShowGameLogEmptyState));
        RaisePropertyChanged(nameof(GameLogRefreshButtonText));
        RaisePropertyChanged(nameof(GameLogExportButtonText));
        RaisePropertyChanged(nameof(GameLogOpenDirectoryButtonText));
        RaisePropertyChanged(nameof(GameLogClearCacheButtonText));
        RaisePropertyChanged(nameof(GameLogLiveLineCountLabel));
        RaisePropertyChanged(nameof(GameLogRecentFilesLabel));
        RaisePropertyChanged(nameof(GameLogCurrentSessionOutputTitle));
        RaisePropertyChanged(nameof(GameLogRecentGeneratedFilesTitle));
        RaisePropertyChanged(nameof(GameLogEmptyTitle));
        RaisePropertyChanged(nameof(GameLogEmptyDescription));
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

    private InstanceSelectionSnapshot? BuildInstanceSelectionSnapshot(string directory, string selectedInstance)
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
            tags.Add(LT("shell.instance_select.tags.favorite"));
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
            tags.Add(LT("shell.instance_select.tags.broken"));
        }

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(manifest.VersionLabel))
        {
            subtitleParts.Add(manifest.VersionLabel);
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
            ? LT("shell.instance_select.subtitle.unknown_version")
            : string.Join(" • ", subtitleParts);
        var detail = LT(
            "shell.instance_select.detail.last_modified",
            ("path", directory),
            ("time", Directory.GetLastWriteTime(directory).ToString("yyyy-MM-dd HH:mm")));

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
            entry.IsSelected
                ? LT("shell.instance_select.entry.current")
                : LT("shell.instance_select.entry.set_as_launch"),
            LT("shell.instance_select.entry.favorite_tooltip"),
            LT("shell.instance_select.entry.open_folder_tooltip"),
            LT("shell.instance_select.entry.delete_tooltip"),
            LT("shell.instance_select.entry.open_settings_tooltip"),
            new ActionCommand(() => SelectInstanceAndCloseSelection(entry)),
            new ActionCommand(() => OpenInstanceSelectionEntry(entry)),
            new ActionCommand(() => ToggleInstanceSelectionFavorite(entry)),
            new ActionCommand(() =>
            {
                if (_shellActionService.TryOpenExternalTarget(entry.Directory, out var error))
                {
                    AddActivity(LT("shell.instance_select.entry.open_instance_directory"), entry.Directory);
                }
                else
                {
                    AddFailureActivity(LT("shell.instance_select.entry.open_instance_directory_failure"), error ?? entry.Directory);
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
            groups.Add(CreateInstanceSelectionGroup(
                LT("shell.instance_select.groups.favorites"),
                favorites,
                groupExpansionStates,
                isExpandedByDefault: true,
                isCountSuppressed: true));
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
        bool isExpandedByDefault,
        bool isCountSuppressed = false)
    {
        return new InstanceSelectionGroupViewModel(
            title,
            isCountSuppressed ? title : LT("shell.instance_select.groups.header", ("title", title), ("count", entries.Count)),
            entries.Select(CreateInstanceSelectionEntry).ToArray(),
            groupExpansionStates.TryGetValue(title, out var isExpanded)
                ? isExpanded
                : isExpandedByDefault);
    }

    private IReadOnlyList<string> BuildInstanceSelectionDisplayTags(InstanceSelectionSnapshot entry)
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
            tags.Add(LT("shell.instance_select.tags.broken"));
        }
        else if (entry.DisplayType == 4)
        {
            tags.Add(LT("shell.instance_select.tags.rarely_used"));
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

    private string ResolveInstanceSelectionGroupTitle(string key, IReadOnlyList<InstanceSelectionSnapshot> entries)
    {
        if (key.StartsWith("loader:", StringComparison.Ordinal))
        {
            return ResolveSingleLoaderInstanceGroupTitle(key["loader:".Length..]);
        }

        return key switch
        {
            "api" => ResolveApiInstanceGroupTitle(entries),
            "error" => LT("shell.instance_select.groups.error"),
            "hidden" => LT("shell.instance_select.groups.hidden"),
            "rarely-used" => LT("shell.instance_select.groups.rarely_used"),
            _ => LT("shell.instance_select.groups.regular")
        };
    }

    private string ResolveSingleLoaderInstanceGroupTitle(string loaderKey)
    {
        return loaderKey switch
        {
            "forge" => LT("shell.instance_select.groups.loaders.forge"),
            "neoforge" => LT("shell.instance_select.groups.loaders.neoforge"),
            "cleanroom" => LT("shell.instance_select.groups.loaders.cleanroom"),
            "labymod" => LT("shell.instance_select.groups.loaders.labymod"),
            "liteloader" => LT("shell.instance_select.groups.loaders.liteloader"),
            "quilt" => LT("shell.instance_select.groups.loaders.quilt"),
            "legacy-fabric" => LT("shell.instance_select.groups.loaders.legacy_fabric"),
            _ => LT("shell.instance_select.groups.loaders.fabric")
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

    private string ResolveApiInstanceGroupTitle(IReadOnlyList<InstanceSelectionSnapshot> entries)
    {
        var loaderLabels = entries
            .Select(entry => entry.LoaderLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (loaderLabels.Length > 1)
        {
            return LT("shell.instance_select.groups.api_installable");
        }

        if (loaderLabels.Length == 1)
        {
            return loaderLabels[0] switch
            {
                "Forge" => LT("shell.instance_select.groups.loaders.forge"),
                "NeoForge" => LT("shell.instance_select.groups.loaders.neoforge"),
                "Cleanroom" => LT("shell.instance_select.groups.loaders.cleanroom"),
                "LabyMod" => LT("shell.instance_select.groups.loaders.labymod"),
                "LiteLoader" => LT("shell.instance_select.groups.loaders.liteloader"),
                "Quilt" => LT("shell.instance_select.groups.loaders.quilt"),
                _ => LT("shell.instance_select.groups.loaders.fabric")
            };
        }

        return LT("shell.instance_select.groups.api_installable");
    }

    private int GetInstanceSelectionGroupPriority(string title)
    {
        return title switch
        {
            var value when value == LT("shell.instance_select.groups.favorites") => 0,
            var value when value == LT("shell.instance_select.groups.regular") => 1,
            var value when value == LT("shell.instance_select.groups.loaders.fabric") => 2,
            var value when value == LT("shell.instance_select.groups.loaders.forge") => 3,
            var value when value == LT("shell.instance_select.groups.loaders.neoforge") => 4,
            var value when value == LT("shell.instance_select.groups.loaders.quilt") => 5,
            var value when value == LT("shell.instance_select.groups.loaders.liteloader") => 6,
            var value when value == LT("shell.instance_select.groups.loaders.cleanroom") => 7,
            var value when value == LT("shell.instance_select.groups.loaders.labymod") => 8,
            var value when value == LT("shell.instance_select.groups.api_installable") => 9,
            var value when value == LT("shell.instance_select.groups.rarely_used") => 10,
            var value when value == LT("shell.instance_select.groups.error") => 11,
            var value when value == LT("shell.instance_select.groups.hidden") => 12,
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
            LT("shell.instance_select.folder.open_tooltip"),
            LT("shell.instance_select.folder.remove_tooltip"),
            new ActionCommand(() =>
            {
                if (string.Equals(folder.Directory, _instanceSelectionLauncherDirectory, GetPathComparison()))
                {
                    AddActivity(LT("shell.instance_select.folder.current_activity"), folder.Directory);
                    return;
                }

                RefreshSelectedLauncherFolderSmoothly(
                    folder.StoredPath,
                    folder.Directory,
                    LT("shell.instance_select.folder.switched", ("directory", folder.Directory)));
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
            this,
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

    private void UpdateTaskManagerEntry(TaskManagerEntryViewModel entry, TaskModel task, DateTimeOffset now)
    {
        var canCancel = (task.State is TaskState.Waiting or TaskState.Running) && task.Cancel.CanExecute(null);
        var canDismiss = task.State is TaskState.Success or TaskState.Canceled or TaskState.Failed;

        entry.Update(
            task.Title,
            task.State,
            MapTaskStateLabel(task.State),
            string.IsNullOrWhiteSpace(task.StateMessage) ? LT("shell.task_manager.placeholders.waiting_state_message") : task.StateMessage,
            BuildTaskActivityText(task, now),
            task.SupportProgress
                ? (string.IsNullOrWhiteSpace(task.ProgressText)
                    ? $"{Math.Round(task.Progress * 100, 1, MidpointRounding.AwayFromZero)}%"
                    : task.ProgressText)
                : LT("shell.task_manager.placeholders.no_progress"),
            task.Progress,
            task.SupportProgress,
            string.IsNullOrWhiteSpace(task.SpeedText) ? "0 B/s" : task.SpeedText,
            task.RemainingFileCount?.ToString() ?? "0",
            task.Children.Count,
            task.Children.Select(child => CreateTaskManagerStageEntry(child, now)).ToArray(),
            LT("shell.task_manager.labels.install_progress"),
            LT("shell.task_manager.labels.current_speed", ("speed", string.IsNullOrWhiteSpace(task.SpeedText) ? "0 B/s" : task.SpeedText)),
            LT("shell.task_manager.labels.remaining_files", ("count", task.RemainingFileCount?.ToString() ?? "0")),
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

    private TaskManagerStageEntryViewModel CreateTaskManagerStageEntry(TaskModel task, DateTimeOffset now)
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

    private string BuildTaskActivityText(TaskModel task, DateTimeOffset now)
    {
        var activeDuration = now - task.StateSince;
        var recentDuration = now - task.LastUpdatedAt;

        return task.State switch
        {
            TaskState.Running => LT(
                "shell.task_manager.activity.running",
                ("duration", FormatTaskDuration(now - (task.StartedAt ?? task.StateSince))),
                ("recent", FormatRecentActivity(recentDuration))),
            TaskState.Waiting => LT("shell.task_manager.activity.waiting", ("duration", FormatTaskDuration(activeDuration))),
            TaskState.Success => LT("shell.task_manager.activity.success", ("duration", FormatTaskDuration((task.FinishedAt ?? now) - (task.StartedAt ?? task.CreatedAt)))),
            TaskState.Canceled => LT("shell.task_manager.activity.canceled", ("duration", FormatTaskDuration((task.FinishedAt ?? now) - (task.StartedAt ?? task.CreatedAt)))),
            TaskState.Failed => LT("shell.task_manager.activity.failed", ("duration", FormatTaskDuration((task.FinishedAt ?? now) - (task.StartedAt ?? task.CreatedAt)))),
            _ => string.Empty
        };
    }

    private string BuildStageActivityText(TaskModel task, DateTimeOffset now)
    {
        return task.State switch
        {
            TaskState.Running => LT("shell.task_manager.stage.running", ("duration", FormatTaskDuration(now - task.StateSince))),
            TaskState.Waiting => LT("shell.task_manager.stage.waiting"),
            TaskState.Success => LT("shell.task_manager.stage.success", ("duration", FormatTaskDuration((task.FinishedAt ?? now) - task.StateSince))),
            TaskState.Canceled => LT("shell.task_manager.stage.canceled", ("duration", FormatTaskDuration((task.FinishedAt ?? now) - task.StateSince))),
            TaskState.Failed => LT("shell.task_manager.stage.failed", ("duration", FormatTaskDuration((task.FinishedAt ?? now) - task.StateSince))),
            _ => string.Empty
        };
    }

    private string FormatTaskDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration.TotalHours >= 1d)
        {
            return LT("shell.task_manager.duration.hours_minutes", ("hours", (int)duration.TotalHours), ("minutes", duration.Minutes));
        }

        if (duration.TotalMinutes >= 1d)
        {
            return LT("shell.task_manager.duration.minutes_seconds", ("minutes", (int)duration.TotalMinutes), ("seconds", duration.Seconds));
        }

        return LT("shell.task_manager.duration.seconds", ("seconds", Math.Max(1, duration.Seconds)));
    }

    private string FormatRecentActivity(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(2))
        {
            return LT("shell.task_manager.recent.just_now");
        }

        if (duration.TotalMinutes >= 1d)
        {
            return LT("shell.task_manager.recent.minutes_ago", ("minutes", (int)duration.TotalMinutes));
        }

        return LT("shell.task_manager.recent.seconds_ago", ("seconds", Math.Max(1, duration.Seconds)));
    }

    private string MapTaskStateLabel(TaskState state)
    {
        return state switch
        {
            TaskState.Waiting => LT("shell.task_manager.states.waiting"),
            TaskState.Running => LT("shell.task_manager.states.running"),
            TaskState.Success => LT("shell.task_manager.states.success"),
            TaskState.Failed => LT("shell.task_manager.states.failed"),
            TaskState.Canceled => LT("shell.task_manager.states.canceled"),
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
            return new InstanceManifestSnapshot(string.Empty, null, true);
        }

        var profile = FrontendVersionManifestInspector.ReadProfileFromManifestPath(manifestPath);
        return new InstanceManifestSnapshot(
            profile.VanillaVersion,
            profile.PrimaryLoaderName,
            !profile.IsManifestValid);
    }

    private string MapInstanceCategory(int displayType)
    {
        return displayType switch
        {
            1 => LT("shell.instance_select.tags.favorite"),
            2 => LT("shell.instance_select.tags.api"),
            3 => LT("shell.instance_select.tags.hidden"),
            4 => LT("shell.instance_select.tags.rarely_used"),
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
            return string.Empty;
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
    string selectText,
    string favoriteToolTip,
    string openFolderToolTip,
    string deleteToolTip,
    string settingsToolTip,
    ActionCommand selectCommand,
    ActionCommand openSettingsCommand,
    ActionCommand toggleFavoriteCommand,
    ActionCommand openFolderCommand,
    ActionCommand deleteCommand)
{
    private static readonly FrontendIcon NavigationSettingsIcon = FrontendIconCatalog.GetNavigationIcon("settings");

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

    public string SelectText { get; } = selectText;

    public string FavoriteToolTip { get; } = favoriteToolTip;

    public string FavoriteIconData => IsFavorite
        ? FrontendIconCatalog.FavoriteFilled.Data
        : FrontendIconCatalog.FavoriteOutline.Data;

    public IBrush FavoriteIconBrush => FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3");

    public string OpenFolderIconData => FrontendIconCatalog.FolderOutline.Data;

    public string OpenFolderToolTip { get; } = openFolderToolTip;

    public string DeleteIconData => FrontendIconCatalog.DeleteOutline.Data;

    public string DeleteToolTip { get; } = deleteToolTip;

    public string SettingsIconData => NavigationSettingsIcon.Data;

    public double SettingsIconScale => NavigationSettingsIcon.Scale;

    public string SettingsToolTip { get; } = settingsToolTip;

    public ActionCommand SelectCommand { get; } = selectCommand;

    public ActionCommand OpenSettingsCommand { get; } = openSettingsCommand;

    public ActionCommand ToggleFavoriteCommand { get; } = toggleFavoriteCommand;

    public ActionCommand OpenFolderCommand { get; } = openFolderCommand;

    public ActionCommand DeleteCommand { get; } = deleteCommand;
}

internal sealed class InstanceSelectionGroupViewModel : ViewModelBase
{
    private bool _isExpanded;

    public InstanceSelectionGroupViewModel(string title, string headerText, IReadOnlyList<InstanceSelectEntryViewModel> entries, bool isExpanded)
    {
        Title = title;
        HeaderText = headerText;
        Entries = entries;
        _isExpanded = isExpanded;
        ToggleExpandCommand = new ActionCommand(() => IsExpanded = !IsExpanded);
    }

    public string Title { get; }

    public string HeaderText { get; }

    public IReadOnlyList<InstanceSelectEntryViewModel> Entries { get; }

    public int EntryCount => Entries.Count;

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
    string openFolderToolTip,
    string deleteToolTip,
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

    public string OpenFolderToolTip { get; } = openFolderToolTip;

    public ActionCommand OpenFolderCommand { get; } = openFolderCommand;

    public string DeleteIconData => FrontendIconCatalog.DeleteOutline.Data;

    public string DeleteToolTip { get; } = deleteToolTip;

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
    FrontendShellViewModel owner,
    ActionCommand primaryActionCommand,
    ActionCommand pauseCommand) : ViewModelBase
{
    private readonly FrontendShellViewModel _owner = owner;
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
    private string _progressLabel = string.Empty;
    private string _speedSummaryText = string.Empty;
    private string _remainingFilesSummaryText = string.Empty;
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

    public string ProgressLabel => _progressLabel;

    public string SpeedSummaryText => _speedSummaryText;

    public string RemainingFilesSummaryText => _remainingFilesSummaryText;

    public int ChildCount => _childCount;

    public bool HasChildren => ChildCount > 0;

    public string ChildrenText => _owner.LT("shell.task_manager.entries.children", ("count", ChildCount));

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
        string progressLabel,
        string speedSummaryText,
        string remainingFilesSummaryText,
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
        SetProperty(ref _progressLabel, progressLabel, nameof(ProgressLabel));
        SetProperty(ref _speedSummaryText, speedSummaryText, nameof(SpeedSummaryText));
        SetProperty(ref _remainingFilesSummaryText, remainingFilesSummaryText, nameof(RemainingFilesSummaryText));

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
