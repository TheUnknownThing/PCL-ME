using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstallWorkflowService
{

    private static void ExecuteForgelikeProcessors(
        ZipArchive archive,
        JsonObject installProfile,
        string launcherDirectory,
        string installerPath,
        FrontendInstallChoice minecraftChoice,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        CancellationToken cancelToken = default)
    {
        if (installProfile["processors"] is not JsonArray processors)
        {
            return;
        }

        var librariesDirectory = Path.Combine(launcherDirectory, "libraries");
        var tempDataDirectory = Path.Combine(launcherDirectory, "PCL", "InstallerData");
        Directory.CreateDirectory(tempDataDirectory);

        var variables = BuildForgelikeProcessorVariables(
            archive,
            installProfile["data"] as JsonObject,
            tempDataDirectory,
            launcherDirectory,
            installerPath,
            minecraftChoice.Version,
            librariesDirectory);

        foreach (var node in processors)
        {
            cancelToken.ThrowIfCancellationRequested();
            if (node is not JsonObject processor || !IsClientProcessor(processor))
            {
                continue;
            }

            ExecuteForgelikeProcessor(
                processor,
                variables,
                librariesDirectory,
                launcherDirectory,
                minecraftChoice,
                downloadProvider,
                speedLimiter,
                cancelToken);
        }
    }


    private static Dictionary<string, string> BuildForgelikeProcessorVariables(
        ZipArchive archive,
        JsonObject? data,
        string tempDataDirectory,
        string launcherDirectory,
        string installerPath,
        string minecraftVersion,
        string librariesDirectory)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SIDE"] = "client",
            ["MINECRAFT_JAR"] = Path.GetFullPath(Path.Combine(launcherDirectory, "versions", minecraftVersion, minecraftVersion + ".jar")),
            ["MINECRAFT_VERSION"] = Path.GetFullPath(Path.Combine(launcherDirectory, "versions", minecraftVersion, minecraftVersion + ".jar")),
            ["ROOT"] = Path.GetFullPath(launcherDirectory),
            ["INSTALLER"] = Path.GetFullPath(installerPath),
            ["LIBRARY_DIR"] = Path.GetFullPath(librariesDirectory)
        };

        if (data is null)
        {
            return variables;
        }

        foreach (var pair in data)
        {
            var rawValue = pair.Value is JsonObject datum
                ? datum["client"]?.GetValue<string>()
                : pair.Value?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            variables[pair.Key] = ParseForgelikeLiteral(
                rawValue,
                new Dictionary<string, string>(StringComparer.Ordinal),
                librariesDirectory,
                path => ExtractInstallerEntryToTempFile(archive, path, tempDataDirectory));
        }

        return variables;
    }


    private static bool IsClientProcessor(JsonObject processor)
    {
        if (processor["sides"] is not JsonArray sides || sides.Count == 0)
        {
            return true;
        }

        return sides
            .Select(node => node?.GetValue<string>())
            .Any(side => string.Equals(side, "client", StringComparison.OrdinalIgnoreCase));
    }


    private static void ExecuteForgelikeProcessor(
        JsonObject processor,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory,
        string launcherDirectory,
        FrontendInstallChoice minecraftChoice,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        CancellationToken cancelToken = default)
    {
        var outputs = ResolveProcessorOutputs(processor, variables, librariesDirectory);
        if (AreProcessorOutputsSatisfied(outputs))
        {
            return;
        }

        if (TryHandleDownloadMojmapsProcessor(processor, variables, librariesDirectory, minecraftChoice, downloadProvider, speedLimiter, cancelToken))
        {
            EnsureProcessorOutputs(outputs);
            return;
        }

        var processorJar = GetRequiredArtifactPath(
            processor["jar"],
            librariesDirectory,
            "Installer processor is missing an executable JAR.");
        if (!File.Exists(processorJar))
        {
            throw new InvalidOperationException($"Installer processor dependency file is missing: {processorJar}");
        }

        var mainClass = ReadJarMainClass(processorJar);
        if (string.IsNullOrWhiteSpace(mainClass))
        {
            throw new InvalidOperationException($"Processor JAR is missing Main-Class: {processorJar}");
        }

        var classpath = new List<string>();
        if (processor["classpath"] is JsonArray classpathNodes)
        {
            foreach (var node in classpathNodes)
            {
                var entryPath = GetRequiredArtifactPath(
                    node,
                    librariesDirectory,
                    "Installer processor is missing a classpath dependency.");
                if (!File.Exists(entryPath))
                {
                    throw new InvalidOperationException($"Installer processor classpath dependency is missing: {entryPath}");
                }

                classpath.Add(entryPath);
            }
        }

        classpath.Add(processorJar);

        var arguments = new List<string>
        {
            "-cp",
            string.Join(Path.PathSeparator, classpath),
            mainClass
        };

        if (processor["args"] is JsonArray argNodes)
        {
            foreach (var node in argNodes)
            {
                var rawArgument = node?.GetValue<string>()
                                  ?? throw new InvalidOperationException("Installer processor argument is missing a text value.");
                arguments.Add(ParseForgelikeLiteral(rawArgument, variables, librariesDirectory));
            }
        }

        RunProcess(
            ResolveJavaExecutable(),
            arguments,
            launcherDirectory,
            "Forge-like installer processor execution failed.",
            cancelToken);

        EnsureProcessorOutputs(outputs);
    }


    private static IReadOnlyDictionary<string, string> ResolveProcessorOutputs(
        JsonObject processor,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory)
    {
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (processor["outputs"] is not JsonObject outputNodes)
        {
            return outputs;
        }

        foreach (var pair in outputNodes)
        {
            var rawPath = pair.Key;
            var rawHash = pair.Value?.GetValue<string>()
                          ?? throw new InvalidOperationException("Installer processor output is missing a checksum.");
            var resolvedPath = ParseForgelikeLiteral(rawPath, variables, librariesDirectory);
            var resolvedHash = ParseForgelikeLiteral(rawHash, variables, librariesDirectory);
            outputs[resolvedPath] = resolvedHash;
        }

        return outputs;
    }


    private static bool AreProcessorOutputsSatisfied(IReadOnlyDictionary<string, string> outputs)
    {
        if (outputs.Count == 0)
        {
            return false;
        }

        foreach (var pair in outputs)
        {
            if (!File.Exists(pair.Key))
            {
                return false;
            }

            if (!string.Equals(ComputeFileSha1(pair.Key), pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(pair.Key);
                return false;
            }
        }

        return true;
    }


    private static void EnsureProcessorOutputs(IReadOnlyDictionary<string, string> outputs)
    {
        foreach (var pair in outputs)
        {
            if (!File.Exists(pair.Key))
            {
                throw new InvalidOperationException($"Installer processor did not generate the expected file: {pair.Key}");
            }

            var actualHash = ComputeFileSha1(pair.Key);
            if (!string.Equals(actualHash, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(pair.Key);
                throw new InvalidOperationException(
                    $"Installer processor output checksum mismatch for {pair.Key}: expected {pair.Value}, actual {actualHash}.");
            }
        }
    }


    private static bool TryHandleDownloadMojmapsProcessor(
        JsonObject processor,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory,
        FrontendInstallChoice minecraftChoice,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        CancellationToken cancelToken = default)
    {
        if (processor["args"] is not JsonArray argNodes)
        {
            return false;
        }

        var options = ParseProcessorOptions(
            argNodes.Select(node => node?.GetValue<string>() ?? string.Empty),
            variables,
            librariesDirectory);
        if (!string.Equals(options.GetValueOrDefault("task"), "DOWNLOAD_MOJMAPS", StringComparison.Ordinal)
            || !string.Equals(options.GetValueOrDefault("side"), "client", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var version = options.GetValueOrDefault("version");
        var output = options.GetValueOrDefault("output");
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var mappings = ResolveClientMappingsDownload(version, minecraftChoice, downloadProvider);
        DownloadFileToPath(mappings.Url, output, mappings.Sha1, downloadProvider, speedLimiter, cancelToken);

        if (!string.IsNullOrWhiteSpace(mappings.Sha1))
        {
            var actualHash = ComputeFileSha1(output);
            if (!string.Equals(actualHash, mappings.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(output);
                throw new InvalidOperationException(
                    $"Mojang mappings download checksum mismatch: expected {mappings.Sha1}, actual {actualHash}.");
            }
        }

        return true;
    }


    private static Dictionary<string, string> ParseProcessorOptions(
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? optionName = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (optionName is not null)
                {
                    options[optionName] = string.Empty;
                }

                optionName = arg[2..];
                continue;
            }

            if (optionName is null)
            {
                continue;
            }

            options[optionName] = ParseForgelikeLiteral(arg, variables, librariesDirectory);
            optionName = null;
        }

        if (optionName is not null)
        {
            options[optionName] = string.Empty;
        }

        return options;
    }


    private static (string Url, string? Sha1) ResolveClientMappingsDownload(
        string version,
        FrontendInstallChoice minecraftChoice,
        FrontendDownloadProvider downloadProvider)
    {
        JsonObject versionManifest;
        if (string.Equals(version, minecraftChoice.Version, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(minecraftChoice.ManifestUrl))
        {
            versionManifest = ReadJsonObject(minecraftChoice.ManifestUrl, downloadProvider);
        }
        else
        {
            var manifest = ReadJsonObject(MojangVersionManifestUrl, downloadProvider);
            var versionUrl = manifest["versions"] is JsonArray versions
                ? versions
                    .Select(node => node as JsonObject)
                    .FirstOrDefault(node => string.Equals(node?["id"]?.GetValue<string>(), version, StringComparison.OrdinalIgnoreCase))
                    ?["url"]?.GetValue<string>()
                : null;
            if (string.IsNullOrWhiteSpace(versionUrl))
            {
                throw new InvalidOperationException($"Unable to find the Mojang version manifest for Minecraft {version}.");
            }

            versionManifest = ReadJsonObject(versionUrl, downloadProvider);
        }

        var clientMappings = versionManifest["downloads"]?["client_mappings"] as JsonObject;
        var url = clientMappings?["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException($"Minecraft {version} is missing the client_mappings download URL.");
        }

        return (url, clientMappings?["sha1"]?.GetValue<string>());
    }


    private static string ParseForgelikeLiteral(
        string literal,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory,
        Func<string, string>? plainValueResolver = null)
    {
        if (literal.Length >= 2 && literal[0] == '{' && literal[^1] == '}')
        {
            var key = literal[1..^1];
            return variables.TryGetValue(key, out var value)
                ? value
                : throw new InvalidOperationException($"Missing installer variable: {key}");
        }

        if (literal.Length >= 2 && literal[0] == '\'' && literal[^1] == '\'')
        {
            return literal[1..^1];
        }

        if (literal.Length >= 2 && literal[0] == '[' && literal[^1] == ']')
        {
            return GetArtifactAbsolutePath(librariesDirectory, literal[1..^1]);
        }

        var replaced = ReplaceForgelikeTokens(literal, variables);
        return plainValueResolver is null ? replaced : plainValueResolver(replaced);
    }


    private static string ReplaceForgelikeTokens(string value, IReadOnlyDictionary<string, string> variables)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '\\')
            {
                if (index == value.Length - 1)
                {
                    throw new InvalidOperationException($"Invalid installer argument: {value}");
                }

                builder.Append(value[++index]);
                continue;
            }

            if (current is '{' or '\'')
            {
                var close = current == '{' ? '}' : '\'';
                var key = new StringBuilder();
                while (++index < value.Length)
                {
                    var tokenChar = value[index];
                    if (tokenChar == '\\')
                    {
                        if (index == value.Length - 1)
                        {
                            throw new InvalidOperationException($"Invalid installer argument: {value}");
                        }

                        key.Append(value[++index]);
                        continue;
                    }

                    if (tokenChar == close)
                    {
                        break;
                    }

                    key.Append(tokenChar);
                }

                if (index >= value.Length)
                {
                    throw new InvalidOperationException($"Invalid installer argument: {value}");
                }

                if (current == '\'')
                {
                    builder.Append(key);
                    continue;
                }

                if (!variables.TryGetValue(key.ToString(), out var replacement))
                {
                    throw new InvalidOperationException($"Missing installer variable: {key}");
                }

                builder.Append(replacement);
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

}
