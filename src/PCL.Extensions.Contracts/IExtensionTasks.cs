namespace PCL.Extensions.Contracts;

public sealed record ExtensionTaskDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
}

public sealed record ExtensionTaskProgress
{
    public double Fraction { get; init; }
    public string Message { get; init; } = "";
    public string Speed { get; init; } = "";
    public int? RemainingFiles { get; init; }
}

public interface IExtensionTasks
{
    /// <summary>Runs off the UI thread. Cancellation includes caller, task-manager and extension lifetime.</summary>
    ValueTask RunAsync(ExtensionTaskDefinition definition,
        Func<IProgress<ExtensionTaskProgress>, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
