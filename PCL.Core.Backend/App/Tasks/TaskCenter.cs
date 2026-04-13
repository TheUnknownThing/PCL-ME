using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务中心，用于管理任务
/// </summary>
public static class TaskCenter
{
    /// <summary>
    /// 可观察的任务模型集合
    /// </summary>
    public static ObservableCollection<TaskModel> Tasks { get; } = [];

    private static readonly ConditionalWeakTable<ITask, TaskModel> _ModelMap = [];
    private static SynchronizationContext? _SynchronizationContext;

    private static void RunOnContext(SynchronizationContext? context, Action action)
    {
        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
        {
            action();
            return;
        }

        context.Post(static state => ((Action)state!).Invoke(), action);
    }

    private static TaskModel _InitModel(ITask instance, SynchronizationContext? context)
    {
        // ReSharper disable SuspiciousTypeConversion.Global
        var cancelable = instance as ITaskCancelable;
        var pausable = instance as ITaskPausable;
        var progressive = instance as ITaskProgressive;
        var telemetry = instance as ITaskTelemetry;
        // ReSharper restore SuspiciousTypeConversion.Global

        TaskModel? model = null;
        model = new TaskModel
        {
            Title = instance.Title,
            SupportProgress = progressive != null,
            OnCancel = cancelable == null
                ? null
                : (() =>
                {
                    RunOnContext(context, () => model!.IsCancelRequested = true);
                    try
                    {
                        cancelable.Cancel();
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Warn(ex, "TaskCenter", $"{instance.Title}: cancel failed");
                        RunOnContext(context, () =>
                        {
                            model!.IsCancelRequested = false;
                            model!.StateMessage = $"取消失败：{ex.Message}";
                            TouchModel(model, updateStateSince: false);
                        });
                    }
                }),
            OnPause = pausable == null
                ? null
                : (() =>
                {
                    try
                    {
                        pausable.Pause();
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Warn(ex, "TaskCenter", $"{instance.Title}: pause failed");
                    }
                })
        };
        TouchModel(model, updateStateSince: false);

        if (telemetry != null)
        {
            ApplyTelemetry(model, telemetry.Telemetry);
        }

        // state event
        instance.StateChanged += (state, message) =>
        {
            LogWrapper.Trace("TaskCenter", $"{instance.Title}: state changed ({state}): {message}");
            RunOnContext(context, () =>
            {
                model.State = state;
                model.StateMessage = message;
                if (state is TaskState.Success or TaskState.Canceled or TaskState.Failed)
                {
                    model.IsCancelRequested = false;
                }

                TouchModel(model, state);
            });
        };

        // progress event
        if (progressive != null)
        {
            progressive.ProgressChanged += progress =>
            {
                RunOnContext(context, () =>
                {
                    model.Progress = Math.Clamp(progress, 0.0, 1.0);
                    TouchModel(model, updateStateSince: false);
                });
            };
        }

        if (telemetry != null)
        {
            telemetry.TelemetryChanged += snapshot =>
            {
                RunOnContext(context, () =>
                {
                    ApplyTelemetry(model, snapshot);
                    TouchModel(model, updateStateSince: false);
                });
            };
        }

        // group events
        if (instance is ITaskGroup group)
        {
            group.AddTask += task =>
            {
                RunOnContext(context, () =>
                {
                    var taskModel = _InitModel(task, context);
                    _ModelMap.Add(task, taskModel);
                    model.Children.Add(taskModel);
                    TouchModel(model, updateStateSince: false);
                });
            };
            group.RemoveTask += task =>
            {
                RunOnContext(context, () =>
                {
                    if (_ModelMap.TryGetValue(task, out var taskModel))
                    {
                        model.Children.Remove(taskModel);
                        TouchModel(model, updateStateSince: false);
                    }
                });
            };
        }

        return model;
    }

    private static void ApplyTelemetry(TaskModel model, TaskTelemetrySnapshot snapshot)
    {
        model.ProgressText = snapshot.ProgressText;
        model.SpeedText = snapshot.SpeedText;
        model.RemainingFileCount = snapshot.RemainingFileCount;
        model.RemainingThreadCount = snapshot.RemainingThreadCount;
    }

    private static void TouchModel(TaskModel model, TaskState? state = null, bool updateStateSince = true)
    {
        var now = DateTimeOffset.UtcNow;
        model.LastUpdatedAt = now;

        if (state is null)
        {
            return;
        }

        if (updateStateSince)
        {
            model.StateSince = now;
        }

        if (state == TaskState.Running)
        {
            model.StartedAt ??= now;
            model.FinishedAt = null;
        }
        else if (state is TaskState.Success or TaskState.Canceled or TaskState.Failed)
        {
            model.FinishedAt ??= now;
        }
    }

    /// <summary>
    /// 注册响应式任务实例
    /// </summary>
    /// <param name="instance">任务实例</param>
    /// <param name="start">是否立即启动该实例</param>
    public static void Register(ITask instance, bool start = true)
    {
        var synchronizationContext = SynchronizationContext.Current;
        _SynchronizationContext ??= synchronizationContext;
        var model = _InitModel(instance, synchronizationContext);
        RunOnContext(synchronizationContext, () => Tasks.Add(model));

        if (start)
        {
            _ = Task.Run(async () =>
            {
                try { await instance.ExecuteAsync(); }
                catch (OperationCanceledException) { /* ignoring */ }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "TaskCenter", $"{instance.Title}: exception thrown");
                    RunOnContext(synchronizationContext, () =>
                    {
                        model.State = TaskState.Failed;
                        model.StateMessage = ex.Message;
                        TouchModel(model, TaskState.Failed);
                    });
                }
            });
        }
    }

    /// <summary>
    /// 移除指定任务
    /// </summary>
    public static void Remove(TaskModel model)
    {
        RunOnContext(_SynchronizationContext, () => Tasks.Remove(model));
    }

    /// <summary>
    /// 移除所有已结束的任务
    /// </summary>
    public static void RemoveFinished()
    {
        foreach (var model in Tasks.Where(x => x.State > TaskState.Running).ToList())
            Remove(model);
    }
}
