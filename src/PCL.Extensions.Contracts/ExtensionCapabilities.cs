namespace PCL.Extensions.Contracts;

/// <summary>Names describe optional contracts, independently of the launcher release.</summary>
public static class ExtensionCapabilities
{
    public const string SdkVersion = "1.1.0";
    public const string Contributions = "pcl.ui.contributions";
    public const string ContextActions = "pcl.ui.context-actions";
    public const string Instances = "pcl.instances";
    public const string Resources = "pcl.resources";
    public const string ResourceProviders = "pcl.resource-providers";
    public const string Tasks = "pcl.tasks";
    public const string Lifecycle = "pcl.lifecycle";
    public const string LaunchHooks = "pcl.launch-hooks";
}

public interface IExtensionCapabilities
{
    string SdkVersion { get; }
    IReadOnlyList<string> Available { get; }
    bool Supports(string capability);
}

public static class ExtensionTopics
{
    public const string InstanceCreated = "launcher.instance.created";
    public const string InstanceInstalled = "launcher.instance.installed";
    public const string InstanceUpdated = "launcher.instance.updated";
    public const string InstanceRemoved = "launcher.instance.removed";
    public const string ResourcesChanged = "launcher.resources.changed";
    public const string GameStarted = "launcher.game.started";
    public const string GameExited = "launcher.game.exited";
}

/// <summary>JSON event payload. Optional fields are null when inapplicable.</summary>
public sealed record ExtensionLifecycleEvent
{
    public required string InstanceId { get; init; }
    public int? ProcessId { get; init; }
    public int? ExitCode { get; init; }
    public bool? IsNewInstance { get; init; }
}
