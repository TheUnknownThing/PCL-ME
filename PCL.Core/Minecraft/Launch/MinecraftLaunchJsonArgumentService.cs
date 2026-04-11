using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchJsonArgumentService
{
    private static readonly IReadOnlyDictionary<string, bool> EmptyFeatures =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    public static IReadOnlyList<string> ExtractValues(MinecraftLaunchJsonArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SectionJsons);

        var values = new List<string>();
        foreach (var sectionJson in request.SectionJsons)
        {
            if (string.IsNullOrWhiteSpace(sectionJson))
            {
                continue;
            }

            using var document = JsonDocument.Parse(sectionJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("启动参数节必须是 JSON 数组。");
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    values.Add(item.GetString()!);
                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!EvaluateRules(item, request))
                {
                    continue;
                }

                if (!item.TryGetProperty("value", out var valueElement))
                {
                    continue;
                }

                switch (valueElement.ValueKind)
                {
                    case JsonValueKind.String:
                        values.Add(valueElement.GetString()!);
                        break;
                    case JsonValueKind.Array:
                        values.AddRange(valueElement.EnumerateArray()
                            .Where(value => value.ValueKind == JsonValueKind.String)
                            .Select(value => value.GetString()!)
                            .ToList());
                        break;
                }
            }
        }

        return values;
    }

    private static bool EvaluateRules(JsonElement item, MinecraftLaunchJsonArgumentRequest request)
    {
        if (!item.TryGetProperty("rules", out var ruleArray) ||
            ruleArray.ValueKind == JsonValueKind.Null ||
            ruleArray.ValueKind == JsonValueKind.Undefined)
        {
            return true;
        }

        var required = false;
        foreach (var rule in ruleArray.EnumerateArray())
        {
            var isRightRule = true;
            if (rule.TryGetProperty("os", out var osRule) && osRule.ValueKind == JsonValueKind.Object)
            {
                if (osRule.TryGetProperty("name", out var osNameProperty))
                {
                    var osName = osNameProperty.GetString();
                    if (string.Equals(osName, "unknown", StringComparison.Ordinal))
                    {
                    }
                    else if (string.Equals(osName, GetCurrentOperatingSystemName(), StringComparison.Ordinal))
                    {
                        if (osRule.TryGetProperty("version", out var versionProperty))
                        {
                            var pattern = versionProperty.GetString();
                            if (!string.IsNullOrWhiteSpace(pattern))
                            {
                                isRightRule &= Regex.IsMatch(request.OperatingSystemVersion, pattern);
                            }
                        }
                    }
                    else
                    {
                        isRightRule = false;
                    }
                }

                if (osRule.TryGetProperty("arch", out var archProperty))
                {
                    isRightRule &= (string.Equals(archProperty.GetString(), "x86", StringComparison.Ordinal) == request.Is32BitOperatingSystem);
                }
            }

            if (rule.TryGetProperty("features", out var featuresRule) && featuresRule.ValueKind == JsonValueKind.Object)
            {
                var featureStates = request.Features ?? EmptyFeatures;
                foreach (var property in featuresRule.EnumerateObject())
                {
                    var expectedState = property.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => false
                    };
                    var actualState = featureStates.TryGetValue(property.Name, out var value) && value;
                    if (actualState != expectedState)
                    {
                        isRightRule = false;
                        break;
                    }
                }
            }

            var action = rule.TryGetProperty("action", out var actionProperty)
                ? actionProperty.GetString()
                : "allow";
            if (string.Equals(action, "allow", StringComparison.Ordinal))
            {
                if (isRightRule)
                {
                    required = true;
                }
            }
            else if (isRightRule)
            {
                required = false;
            }
        }

        return required;
    }

    private static string GetCurrentOperatingSystemName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        return "unknown";
    }
}

public sealed record MinecraftLaunchJsonArgumentRequest(
    IReadOnlyList<string> SectionJsons,
    string OperatingSystemVersion,
    bool Is32BitOperatingSystem,
    IReadOnlyDictionary<string, bool>? Features = null);
