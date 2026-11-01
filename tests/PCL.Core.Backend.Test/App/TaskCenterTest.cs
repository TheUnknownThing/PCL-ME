using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Tasks;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class TaskCenterTest
{
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
            TaskCenter.Register(new PassiveTask("first"), start: false);

            SynchronizationContext.SetSynchronizationContext(secondContext);
            TaskCenter.Register(new PassiveTask("second"), start: false);

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
}
