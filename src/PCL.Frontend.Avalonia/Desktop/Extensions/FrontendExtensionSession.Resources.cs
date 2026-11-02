using PCL.Extensions.Contracts;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private readonly object _resourceSync = new();
    private readonly Dictionary<string, FrontendExtensionResourceProviderRegistration> _resourceProviders = new(StringComparer.Ordinal);

    private FrontendExtensionResourceProviderRegistration[] ResourceProviders()
    {
        lock (_resourceSync)
        {
            if (!_resourceProviders.ContainsKey("pcl.modrinth"))
                foreach (var source in new[] { "Modrinth", "CurseForge" })
                {
                    var definition = new ExtensionResourceProviderDefinition
                    {
                        Id = "pcl." + source.ToLowerInvariant(), Title = source,
                        Kinds = Array.AsReadOnly(new[] { ExtensionResourceKind.Mod, ExtensionResourceKind.ResourcePack, ExtensionResourceKind.Shader, ExtensionResourceKind.World, ExtensionResourceKind.DataPack })
                    };
                    _resourceProviders.Add(definition.Id, new(definition, new LauncherResourceProvider(this, definition), null));
                }
            return _resourceProviders.Values.OrderBy(item => item.Definition.Id, StringComparer.Ordinal).ToArray();
        }
    }


}
