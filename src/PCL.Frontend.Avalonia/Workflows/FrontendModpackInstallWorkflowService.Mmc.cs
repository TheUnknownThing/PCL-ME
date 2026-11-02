using System.IO.Compression;
using System.Text.Json.Nodes;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendModpackInstallWorkflowService
{
    private static FrontendMmcManifestPatch? BuildMmcManifestPatch(ZipArchive archive, string baseFolder, JsonObject packRoot)
    {
        if (packRoot["components"] is not JsonArray components)
        {
            return null;
        }

        var componentUids = components
            .Select(node => node?["uid"]?.GetValue<string>())
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (componentUids.Count == 0)
        {
            return null;
        }

        var patchesPrefix = baseFolder + "patches/";
        var patches = archive.Entries
            .Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal) &&
                            entry.FullName.StartsWith(patchesPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(entry => ReadMmcPatchEntry(archive, entry.FullName))
            .Where(patch => patch.Root is not null &&
                            componentUids.Contains(patch.Root["uid"]?.GetValue<string>() ?? string.Empty))
            .OrderBy(patch => patch.Order)
            .Select(patch => patch.Root!)
            .ToArray();
        if (patches.Length == 0)
        {
            return null;
        }

        var libraries = new JsonArray();
        var gameArguments = new JsonArray();
        var jvmArguments = new JsonArray();
        var extraProperties = new JsonObject();
        var removeLegacyMinecraftArguments = false;

        foreach (var patch in patches)
        {
            AppendMmcLibraries(libraries, patch["libraries"] as JsonArray);
            AppendMmcLibraries(libraries, patch["+libraries"] as JsonArray);
            AppendJsonArrayValues(jvmArguments, patch["+jvmArgs"] as JsonArray);

            var minecraftArguments = patch["minecraftArguments"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(minecraftArguments))
            {
                foreach (var argument in minecraftArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    gameArguments.Add(argument);
                }

                removeLegacyMinecraftArguments = true;
            }

            if (patch["+tweakers"] is JsonArray tweakers && tweakers.Count > 0)
            {
                var tweaker = tweakers[0]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(tweaker))
                {
                    gameArguments.Add("--tweakClass");
                    gameArguments.Add(tweaker);
                }
            }

            CopyPatchProperty(extraProperties, patch, "mainClass");
            CopyPatchProperty(extraProperties, patch, "assetIndex");
            CopyPatchProperty(extraProperties, patch, "javaVersion");
            if (patch["compatibleJavaMajors"] is JsonArray javaMajors &&
                BuildJavaVersionFromMmcMajors(javaMajors) is { } javaVersion)
            {
                extraProperties["javaVersion"] = javaVersion;
            }
        }

        return libraries.Count == 0 &&
               gameArguments.Count == 0 &&
               jvmArguments.Count == 0 &&
               extraProperties.Count == 0 &&
               !removeLegacyMinecraftArguments
            ? null
            : new FrontendMmcManifestPatch(
                libraries,
                gameArguments,
                jvmArguments,
                extraProperties,
                removeLegacyMinecraftArguments);
    }

    private static (JsonObject? Root, int Order) ReadMmcPatchEntry(ZipArchive archive, string entryPath)
    {
        try
        {
            var root = ReadJsonObjectFromEntry(archive, entryPath);
            var order = root["order"]?.GetValue<int?>() ?? 0;
            return (root, order);
        }
        catch
        {
            return (null, 0);
        }
    }

    private static void AppendMmcLibraries(JsonArray target, JsonArray? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var node in source)
        {
            if (node is not JsonObject library)
            {
                continue;
            }

            var clone = (JsonObject)library.DeepClone();
            if (clone["MMC-hint"] is { } hint)
            {
                clone["hint"] = hint.DeepClone();
                clone.Remove("MMC-hint");
            }

            target.Add(clone);
        }
    }

    private static void AppendJsonArrayValues(JsonArray target, JsonArray? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var node in source)
        {
            if (node is not null)
            {
                target.Add(node.DeepClone());
            }
        }
    }

    private static void CopyPatchProperty(JsonObject target, JsonObject source, string propertyName)
    {
        if (source[propertyName] is { } value)
        {
            target[propertyName] = value.DeepClone();
        }
    }

    private static JsonObject? BuildJavaVersionFromMmcMajors(JsonArray javaMajors)
    {
        var selected = 0;
        foreach (var node in javaMajors)
        {
            if (!TryReadInt(node, out var major))
            {
                continue;
            }

            if (selected == 0 ||
                GetJavaMajorPriority(major) > GetJavaMajorPriority(selected) ||
                GetJavaMajorPriority(major) == GetJavaMajorPriority(selected) && major > selected)
            {
                selected = major;
            }
        }

        if (selected == 0)
        {
            return null;
        }

        var javaVersion = new JsonObject
        {
            ["majorVersion"] = selected
        };
        var component = selected switch
        {
            21 => "java-runtime-delta",
            17 => "java-runtime-gamma",
            8 => "jre-legacy",
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(component))
        {
            javaVersion["component"] = component;
        }

        return javaVersion;
    }

    private static int GetJavaMajorPriority(int major)
    {
        return major switch
        {
            21 => 4,
            17 => 3,
            11 => 2,
            8 => 1,
            _ => 0
        };
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is null)
        {
            return false;
        }

        try
        {
            return node.GetValue<int?>() is { } parsed && (value = parsed) >= 0;
        }
        catch
        {
            return int.TryParse(node.ToString(), out value);
        }
    }
}
