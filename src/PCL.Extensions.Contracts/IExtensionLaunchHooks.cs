namespace PCL.Extensions.Contracts;

public enum ExtensionHookFailurePolicy { StopLaunch, ContinueLaunch }

public sealed record ExtensionLaunchHook
{
    public required string Id { get; init; }
    public int Order { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public ExtensionHookFailurePolicy FailurePolicy { get; init; }
}

/// <summary>Contains no authentication tokens or mutable launcher internals.</summary>
public sealed record ExtensionLaunchContext
{
    public required string InstanceId { get; init; }
    public string? MinecraftVersion { get; init; }
}

public sealed record ExtensionLaunchChanges
{
    public IReadOnlyList<string> JvmArguments { get; init; } = [];
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}

public interface IExtensionLaunchHooks
{
    /// <summary>Hooks run in Order/Id order off the UI thread; returned changes are validated together.</summary>
    IDisposable Register(ExtensionLaunchHook hook,
        Func<ExtensionLaunchContext, CancellationToken, ValueTask<ExtensionLaunchChanges>> prepare);
}
