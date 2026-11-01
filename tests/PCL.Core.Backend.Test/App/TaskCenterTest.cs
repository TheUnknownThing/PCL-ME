using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Tasks;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class TaskCenterTest
{
    [TestMethod]
    public async Task ActiveTasks_TracksCompletionBeforeQueuedPresentationUpdates()
    {
        var originalContext = SynchronizationContext.Current;
        var context = new QueuedSynchronizationContext();
        var task = new PassiveTask("queued completion");
        TaskModel model;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            TaskCenter.Register(task, start: false);
            model = TaskCenter.Tasks.Single(item => item.Title == task.Title);
        }
        finally { SynchronizationContext.SetSynchronizationContext(originalContext); }

        Assert.Contains(task, TaskCenter.ActiveTasks);
        await Task.Run(() => task.ExecuteAsync());
        Assert.DoesNotContain(task, TaskCenter.ActiveTasks);
        Assert.AreEqual(TaskState.Waiting, model.State);
        context.Drain();
        Assert.AreEqual(TaskState.Success, model.State);
    }

    [TestMethod]
    public async Task ActiveTasks_CleansUpTasksThatThrowWithoutReportingState()
    {
        var task = new ThrowingTask();
        TaskCenter.Register(task);
        await task.Started.Task.WaitAsync(System.TimeSpan.FromSeconds(5));
        Assert.Contains(task, TaskCenter.ActiveTasks);
        task.Release.SetResult();
        for (var attempt = 0; attempt < 200 && TaskCenter.ActiveTasks.Contains(task); attempt++) await Task.Delay(10);
        Assert.DoesNotContain(task, TaskCenter.ActiveTasks);
        Assert.AreEqual(TaskState.Failed, TaskCenter.Tasks.Single(item => item.Title == task.Title).State);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var task in TaskCenter.Tasks.ToArray())
        {
            TaskCenter.Remove(task);
        }
    }

    [TestMethod]
    public void Remove_UsesTaskSynchronizationContextInsteadOfFirstRegisteredContext()
    {
        var originalContext = SynchronizationContext.Current;
        var firstContext = new TrackingSynchronizationContext();
        var secondContext = new TrackingSynchronizationContext();

        try
        {
            SynchronizationContext.SetSynchronizationContext(firstContext);
            var first = new PassiveTask("first");
            TaskCenter.Register(first, start: false);

            SynchronizationContext.SetSynchronizationContext(secondContext);
            var second = new PassiveTask("second");
            TaskCenter.Register(second, start: false);

            var firstTask = TaskCenter.Tasks.Single(task => task.Title == "first");
            var secondTask = TaskCenter.Tasks.Single(task => task.Title == "second");
            firstContext.Reset();
            secondContext.Reset();

            SynchronizationContext.SetSynchronizationContext(null);
            TaskCenter.Remove(secondTask);

            Assert.IsFalse(TaskCenter.Tasks.Contains(secondTask));
            Assert.AreEqual(0, firstContext.PostCount);
            Assert.AreEqual(1, secondContext.PostCount);

            TaskCenter.Remove(firstTask);
            first.ExecuteAsync().GetAwaiter().GetResult();
            second.ExecuteAsync().GetAwaiter().GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    private sealed class PassiveTask(string title) : ITask
    {
        public string Title { get; } = title;

        public event TaskStateEvent StateChanged = delegate { };

        public Task ExecuteAsync(CancellationToken cancelToken = default)
        {
            StateChanged(TaskState.Success, "done");
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public void Reset()
        {
            PostCount = 0;
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostCount++;
            d(state);
        }
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<System.Action> _pending = new();
        public override void Post(SendOrPostCallback callback, object? state) => _pending.Enqueue(() => callback(state));
        public void Drain() { while (_pending.TryDequeue(out var action)) action(); }
    }

    private sealed class ThrowingTask : ITask
    {
        public string Title => "throwing task";
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event TaskStateEvent StateChanged { add { } remove { } }
        public async Task ExecuteAsync(CancellationToken cancelToken = default)
        {
            Started.SetResult();
            await Release.Task;
            throw new System.InvalidOperationException("execution failed");
        }
    }
}
