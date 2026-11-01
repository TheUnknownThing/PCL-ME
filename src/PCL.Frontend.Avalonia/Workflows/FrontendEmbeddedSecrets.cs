using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendEmbeddedSecrets
{
    private const string EmbeddedSecretPrefix = "PCL_SECRET_";
    private static readonly Lazy<IReadOnlyDictionary<string, string>> EmbeddedSecrets = new(LoadEmbeddedSecrets);

    public static string GetCurseForgeApiKey()
    {
        return GetSecret("CURSEFORGE_API_KEY", "PCL_CURSEFORGE_API_KEY", "CURSEFORGE_API_KEY");
    }

    public static string GetMicrosoftClientId()
    {
        return GetSecret("MS_CLIENT_ID", "PCL_MS_CLIENT_ID", "MS_CLIENT_ID");
    }

    private static string GetSecret(string key, string prefixedEnvironmentKey, string compatibilityEnvironmentKey)
    {
        if (EmbeddedSecrets.Value.TryGetValue(key, out var embeddedValue) && !string.IsNullOrWhiteSpace(embeddedValue))
        {
            return embeddedValue;
        }

#if DEBUG
        var prefixedValue = Environment.GetEnvironmentVariable(prefixedEnvironmentKey);
        if (!string.IsNullOrWhiteSpace(prefixedValue))
        {
            return prefixedValue;
        }

        return Environment.GetEnvironmentVariable(compatibilityEnvironmentKey) ?? string.Empty;
#else
        return string.Empty;
#endif
    }

    private static IReadOnlyDictionary<string, string> LoadEmbeddedSecrets()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute =>
                !string.IsNullOrWhiteSpace(attribute.Value)
                && attribute.Key.StartsWith(EmbeddedSecretPrefix, StringComparison.Ordinal))
            .ToDictionary(
                attribute => attribute.Key[EmbeddedSecretPrefix.Length..],
                attribute => attribute.Value!,
                StringComparer.Ordinal);
    }
}
