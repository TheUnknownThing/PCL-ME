using System.Text.Json;
using PCL.Core.App.Tasks;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    // Mutations are serialized across extensions, including shared game directories.
    private readonly SemaphoreSlim _extensionMutation = new(1);
    private FrontendExtensionOperationsService CreateOperations(IExtensionContext context) => new(new(
        this, _actions.RuntimePaths, _text, _extensionMutation,
        () => _launcher.SelectedDownloadSourceIndex, _actions.GetDownloadTransferOptions,
        token => DispatchAsync(() => { token.ThrowIfCancellationRequested(); _launcher.RefreshExtensionInstances(); return ValueTask.CompletedTask; }),
        owner => DispatchAsync(() =>
        {
            if (!_launcher.CanMutateExtensionInstances && !(_executingHookOwner == owner.Manifest.Id && _launcher.CanMutateDuringExtensionHook))
                throw new ExtensionOperationException("launcher.busy", T("unavailable"));
            if (TaskCenter.ActiveTasks.Any(task => !_extensionTasks.Contains(task)))
                throw new ExtensionOperationException("launcher.busy", T("unavailable"));
            return ValueTask.CompletedTask;
        }),
        async () =>
        {
            string? directory = null;
            await DispatchAsync(() => { directory = _launcher.ExtensionLauncherDirectory; return ValueTask.CompletedTask; });
            return directory!;
        }, ResourceProviders, RegisterResourceProvider,
        (topic, id, isNew) => PublishLifecycle(topic, id, isNewInstance: isNew), (id, error) => Host.LogContributionError(id, error)), context);

    private IDisposable RegisterResourceProvider(ExtensionResourceProviderDefinition definition, IExtensionResourceProvider provider, IExtensionContext context)
    {
        lock (_resourceSync) _resourceProviders.Add(definition.Id, new(definition, provider, context));
        ResourceProvidersChanged();
        return new ExtensionRegistration(() =>
        {
            lock (_resourceSync) _resourceProviders.Remove(definition.Id);
            if (_currentViewOwner == context.Manifest.Id) CloseWorkspace();
            ResourceProvidersChanged();
        });
    }

    internal void PublishLifecycle(string topic, string instanceId, int? processId = null, int? exitCode = null, bool? isNewInstance = null)
        => Host.PublishHostEvent(topic, JsonSerializer.SerializeToElement(new ExtensionLifecycleEvent { InstanceId = instanceId, ProcessId = processId, ExitCode = exitCode, IsNewInstance = isNewInstance }));

    internal async ValueTask WaitForExtensionMutationsAsync(CancellationToken token)
    {
        if (!await _extensionMutation.WaitAsync(0, token)) throw new ExtensionOperationException("launcher.busy", T("unavailable"));
        _extensionMutation.Release();
    }

    internal async ValueTask<IDisposable> BeginLauncherInstallAsync(CancellationToken token)
    {
        await _extensionMutation.WaitAsync(token);
        return new PCL.Extensions.Runtime.ExtensionRegistration(() => _extensionMutation.Release());
    }
}
