using System.Diagnostics.CodeAnalysis;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed class FrontendConfigProviderAdapter(Func<JsonFileProvider> providerFactory) : IConfigProvider
{
    private readonly Func<JsonFileProvider> _providerFactory =
        providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

    public FrontendConfigProviderAdapter(JsonFileProvider provider)
        : this(() => provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
    }

    public FrontendConfigProviderAdapter(FrontendRuntimePaths runtimePaths)
        : this(CreateProviderFactory(runtimePaths))
    {
    }

    public bool GetValue<T>(string key, [NotNullWhen(true)] out T? value, object? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var provider = _providerFactory();
        if (!provider.Exists(key))
        {
            value = default;
            return false;
        }

        value = provider.Get<T>(key);
        return value is not null;
    }

    public void SetValue<T>(string key, T value, object? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var provider = _providerFactory();
        provider.Set(key, value);
        provider.Sync();
    }

    public void Delete(string key, object? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var provider = _providerFactory();
        provider.Remove(key);
        provider.Sync();
    }

    public bool Exists(string key, object? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var provider = _providerFactory();
        return provider.Exists(key);
    }

    private static Func<JsonFileProvider> CreateProviderFactory(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        return runtimePaths.OpenSharedConfigProvider;
    }
}
