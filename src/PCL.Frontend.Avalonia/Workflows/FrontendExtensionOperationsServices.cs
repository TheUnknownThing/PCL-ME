using PCL.Extensions.Contracts;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed record FrontendExtensionResourceProviderRegistration(ExtensionResourceProviderDefinition Definition,
    IExtensionResourceProvider Provider, IExtensionContext? Context);

/// <summary>Host adapters for workflows. No controls, view models or configuration providers cross the SDK.</summary>
internal sealed record FrontendExtensionOperationsServices(
    ILauncherServices Launcher,
    FrontendRuntimePaths Paths,
    II18nService Text,
    SemaphoreSlim Mutation,
    Func<int> DownloadSourceIndex,
    Func<FrontendDownloadTransferOptions> DownloadOptions,
    Func<CancellationToken, ValueTask> Refresh,
    Func<IExtensionContext, ValueTask> CheckMutation,
    Func<ValueTask<string>> LauncherDirectory,
    Func<FrontendExtensionResourceProviderRegistration[]> GetProviders,
    Func<ExtensionResourceProviderDefinition, IExtensionResourceProvider, IExtensionContext, IDisposable> RegisterProvider,
    Action<string, string, bool?> PublishEvent,
    Action<string, Exception> Log)
{
    public void Publish(string topic, string instanceId, bool? isNewInstance = null) => PublishEvent(topic, instanceId, isNewInstance);
}
