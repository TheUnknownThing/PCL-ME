using PCL.Core.App.Tasks;
using PCL.Extensions.Contracts;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private readonly HashSet<ITask> _extensionTasks = [];
    private readonly HashSet<string> _extensionRunningTaskIds = new(StringComparer.Ordinal);
    private sealed class ScopedTasks(FrontendExtensionSession session, IExtensionContext context) : IExtensionTasks
    {

        public async ValueTask RunAsync(ExtensionTaskDefinition definition,
            Func<IProgress<ExtensionTaskProgress>, CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            ValidateContributionId(context, definition.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(definition.Title);
            lock (session._extensionRunningTaskIds)
                if (!session._extensionRunningTaskIds.Add(definition.Id)) throw new ExtensionOperationException("task.running", "This extension task is already running.");
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.Lifetime, cancellationToken);
                using var task = new ExtensionManagedTask(definition.Title, operation, linked.Token);
                try
                {
                    await DispatchAsync(() => { session._extensionTasks.Add(task); TaskCenter.Register(task, start: false); return ValueTask.CompletedTask; });
                    await task.ExecuteAsync(linked.Token);
                }
                finally { await DispatchAsync(() => { session._extensionTasks.Remove(task); return ValueTask.CompletedTask; }); }
            }
            finally { lock (session._extensionRunningTaskIds) session._extensionRunningTaskIds.Remove(definition.Id); }
        }
    }

    private sealed class ExtensionManagedTask(string title,
        Func<IProgress<ExtensionTaskProgress>, CancellationToken, Task> operation, CancellationToken lifetime)
        : ITask, ITaskCancelable, ITaskProgressive, ITaskProgressStatus, IProgress<ExtensionTaskProgress>, IDisposable
    {
        private readonly CancellationTokenSource _cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        private readonly object _stateSync = new();
        private volatile bool _finished;
        public string Title { get; } = title;
        public TaskProgressStatusSnapshot ProgressStatus { get; private set; } = new("", "", null, null);
        public event TaskStateEvent StateChanged = delegate { };
        public event TaskProgressEvent ProgressChanged = delegate { };
        public event TaskProgressStatusEvent ProgressStatusChanged = delegate { };
        public void Cancel() { lock (_stateSync) { if (!_finished) _cancel.Cancel(); } }
        public void Report(ExtensionTaskProgress value)
        {
            lock (_stateSync)
            {
                if (_finished || _cancel.IsCancellationRequested) return;
                var fraction = double.IsFinite(value.Fraction) ? Math.Clamp(value.Fraction, 0, 1) : 0;
                ProgressStatus = new($"{fraction:P0}", value.Speed, value.RemainingFiles, null);
                ProgressChanged(fraction);
                ProgressStatusChanged(ProgressStatus);
                StateChanged(TaskState.Running, value.Message);
            }
        }
        private void Complete(TaskState state, string message)
        {
            lock (_stateSync)
            {
                _finished = true;
                if (state == TaskState.Success) ProgressChanged(1);
                StateChanged(state, message);
            }
        }
        public async Task ExecuteAsync(CancellationToken cancelToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancel.Token);
            try
            {
                StateChanged(TaskState.Running, "");
                await Task.Run(() => operation(this, linked.Token), linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                Complete(TaskState.Success, "");
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            { Complete(TaskState.Canceled, ""); throw; }
            catch (Exception ex) { Complete(TaskState.Failed, ex.Message); throw; }
            finally { _finished = true; }
        }
        public void Dispose() { lock (_stateSync) { _finished = true; _cancel.Dispose(); } }
    }
}
