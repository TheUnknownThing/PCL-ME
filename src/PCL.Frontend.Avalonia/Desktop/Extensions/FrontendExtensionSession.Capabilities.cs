using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private static readonly string[] AllCapabilities =
    [ExtensionCapabilities.Contributions, ExtensionCapabilities.ContextActions, ExtensionCapabilities.Instances,
        ExtensionCapabilities.Resources, ExtensionCapabilities.ResourceProviders, ExtensionCapabilities.Tasks,
        ExtensionCapabilities.Lifecycle, ExtensionCapabilities.LaunchHooks];

    private IReadOnlyDictionary<Type, object> CreateServices(IExtensionContext context)
    {
        var operations = CreateOperations(context);
        return new Dictionary<Type, object>
        {
            [typeof(IExtensionUi)] = new ScopedUi(this, context),
            [typeof(IExtensionContributions)] = new ScopedContributions(this, context),
            [typeof(IExtensionCapabilities)] = new Capabilities(),
            [typeof(IExtensionInstances)] = operations,
            [typeof(IExtensionResources)] = operations,
            [typeof(IExtensionResourceProviders)] = operations,
            [typeof(IExtensionTasks)] = new ScopedTasks(this, context),
            [typeof(IExtensionLaunchHooks)] = new ScopedLaunchHooks(this, context)
        };
    }

    private sealed class Capabilities : IExtensionCapabilities
    {
        public string SdkVersion => ExtensionCapabilities.SdkVersion;
        public IReadOnlyList<string> Available => Array.AsReadOnly(AllCapabilities);
        public bool Supports(string capability) => AllCapabilities.Contains(capability, StringComparer.Ordinal);
    }
}
