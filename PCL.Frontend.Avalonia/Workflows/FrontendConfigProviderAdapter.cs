using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using System.Diagnostics.CodeAnalysis;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed class FrontendConfigProviderAdapter(JsonFileProvider provider) : IConfigProvider
{
    public bool GetValue<T>(string key, [NotNullWhen(true)] out T? value, object? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

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
        provider.Set(key, value);
        provider.Sync();
    }

    public void Delete(string key, object? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        provider.Remove(key);
        provider.Sync();
    }

    public bool Exists(string key, object? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return provider.Exists(key);
    }
}
