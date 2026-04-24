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

}

