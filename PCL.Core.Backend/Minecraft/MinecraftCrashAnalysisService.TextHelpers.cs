using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft;


public static partial class MinecraftCrashAnalysisService
{
    private sealed partial class Analyzer
    {
        private string Text(string key, string fallback)
        {
            var translated = _localize?.Invoke(key);
            return string.IsNullOrWhiteSpace(translated) || string.Equals(translated, key, StringComparison.Ordinal)
                ? fallback
                : translated;
        }


        private static int CountOccurrences(string input, string value, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(value))
            {
                return 0;
            }

            var count = 0;
            var index = 0;
            while ((index = input.IndexOf(value, index, comparison)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }


        private static List<string> MatchMany(string input, string pattern, RegexOptions options = RegexOptions.None)
        {
            if (string.IsNullOrEmpty(input))
            {
                return [];
            }

            return Regex.Matches(input, pattern, options)
                .Select(match => match.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }


        private static string? MatchSingle(string input, string pattern, RegexOptions options = RegexOptions.None)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            var match = Regex.Match(input, pattern, options);
            return match.Success ? match.Value : null;
        }


        private static string GetHeadTailLines(IReadOnlyList<string> raw, int headLines, int tailLines)
        {
            if (raw.Count <= headLines + tailLines)
            {
                return string.Join('\n', raw.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct());
            }

            var lines = new List<string>();
            var realHeadLines = 0;
            var viewedLines = 0;
            for (; viewedLines < raw.Count; viewedLines++)
            {
                if (lines.Contains(raw[viewedLines], StringComparer.Ordinal))
                {
                    continue;
                }

                realHeadLines++;
                lines.Add(raw[viewedLines]);
                if (realHeadLines >= headLines)
                {
                    break;
                }
            }

            var realTailLines = 0;
            for (var index = raw.Count - 1; index > viewedLines; index--)
            {
                if (lines.Contains(raw[index], StringComparer.Ordinal))
                {
                    continue;
                }

                realTailLines++;
                lines.Insert(realHeadLines, raw[index]);
                if (realTailLines >= tailLines)
                {
                    break;
                }
            }

            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                builder.Append(line);
                builder.Append('\n');
            }

            return builder.ToString();
        }


        private static DateTime GetLastWriteTime(string path)
        {
            try
            {
                return File.GetLastWriteTime(path);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }


        private static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
        }


        private static string? TrimToNull(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }


        private static bool PathsEqual(string left, string? right)
        {
            if (string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }


        private static string BeforeFirst(string value, string separator)
        {
            var index = value.IndexOf(separator, StringComparison.Ordinal);
            return index < 0 ? value : value[..index];
        }


        private static string AfterLast(string value, string separator)
        {
            var index = value.LastIndexOf(separator, StringComparison.Ordinal);
            return index < 0 ? value : value[(index + separator.Length)..];
        }


        private static string Between(string value, string start, string end)
        {
            var startIndex = value.IndexOf(start, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return string.Empty;
            }

            startIndex += start.Length;
            var endIndex = value.IndexOf(end, startIndex, StringComparison.Ordinal);
            return endIndex < 0 ? value[startIndex..] : value[startIndex..endIndex];
        }


        private static readonly string[] IgnoredStackKeywords =
        [
            "com", "org", "net", "asm", "fml", "mod", "jar", "sun", "lib", "map", "gui", "dev", "nio", "api", "dsi", "top", "mcp",
            "core", "init", "mods", "main", "file", "game", "load", "read", "done", "util", "tile", "item", "base", "oshi", "impl", "data", "pool", "task",
            "forge", "setup", "block", "model", "mixin", "event", "unimi", "netty", "world", "lwjgl", "gitlab", "common", "server", "config", "mixins", "compat",
            "loader", "launch", "entity", "assist", "client", "plugin", "modapi", "mojang", "shader", "events", "github", "recipe", "render", "packet",
            "preinit", "preload", "machine", "reflect", "channel", "general", "handler", "content", "systems", "modules", "service", "fastutil", "optifine", "internal",
            "platform", "override", "fabricmc", "neoforge", "injection", "listeners", "scheduler", "minecraft", "universal", "multipart", "neoforged", "microsoft",
            "transformer", "transformers", "minecraftforge", "blockentity", "spongepowered", "electronwill"
        ];
    }
}
