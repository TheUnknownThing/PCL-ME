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

internal sealed partial class LauncherViewModel
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

    public bool ShowTaskManagerSurface => IsStandardRoute && _currentRoute.Page == LauncherFrontendPageKey.TaskManager;

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

    public bool ShowGameLogSurface => IsStandardRoute && _currentRoute.Page == LauncherFrontendPageKey.GameLog;

    public bool HasGameLogFiles => GameLogFileEntries.Count > 0;

    public bool HasNoGameLogFiles => !HasGameLogFiles;

    public bool ShowGameLogLiveOutput => HasLaunchLogLines;

    public bool ShowGameLogEmptyState => !ShowGameLogLiveOutput && HasNoGameLogFiles;

    public int GameLogLiveLineCount => _launchLogLines.Count;

    public int GameLogRecentFileCount => _gameLogRecentFileCount;

    public string GameLogLatestUpdateLabel => _gameLogLatestUpdateLabel;
}
