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
        private void AnalyzeStack()
        {
            if (!_logAll.Contains("orge", StringComparison.OrdinalIgnoreCase) &&
                !_logAll.Contains("abric", StringComparison.OrdinalIgnoreCase) &&
                !_logAll.Contains("uilt", StringComparison.OrdinalIgnoreCase) &&
                !_logAll.Contains("iteloader", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var keywords = new List<string>();
            if (_logCrash is not null)
            {
                keywords.AddRange(AnalyzeStackKeyword(BeforeFirst(_logCrash, "System Details")));
            }

            if (_logMc is not null)
            {
                var fatals = MatchMany(_logMc, @"/FATAL] .+?(?=[\n]+\[)", RegexOptions.Singleline);
                if (_logMc.Contains("Unreported exception thrown!", StringComparison.Ordinal))
                {
                    fatals.Add(Between(_logMc, "Unreported exception thrown!", "at oolloo.jlw.Wrapper"));
                }

                foreach (var fatal in fatals)
                {
                    keywords.AddRange(AnalyzeStackKeyword(fatal));
                }
            }

            if (_logHs is not null)
            {
                keywords.AddRange(AnalyzeStackKeyword(Between(_logHs, "T H R E A D", "Registers:")));
            }

            keywords = keywords
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (keywords.Count == 0)
            {
                return;
            }

            var modNames = AnalyzeModName(keywords);
            if (modNames is null)
            {
                AppendReason(CrashReason.StackKeyword, keywords);
            }
            else
            {
                AppendReason(CrashReason.StackModName, modNames);
            }
        }


        private void AnalyzeCriticalPriorityThree()
        {
            if (_logMc is not null)
            {
                if (!_logMc.Contains("at net.", StringComparison.Ordinal) &&
                    !_logMc.Contains("INFO]", StringComparison.Ordinal) &&
                    _logHs is null &&
                    _logCrash is null &&
                    _logMc.Length < 100)
                {
                    AppendReason(CrashReason.VeryShortOutput, _logMc);
                }

                if (_logMc.Contains("Mod resolution failed", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ModLoaderFailure);
                }

                if (_logMc.Contains("Failed to create mod instance.", StringComparison.Ordinal))
                {
                    var modId = MatchSingle(_logMc, "(?<=Failed to create mod instance. ModID: )[^,]+") ??
                                MatchSingle(_logMc, "(?<=Failed to create mod instance. ModId )[^\n]+(?= for )");
                    AppendReason(CrashReason.ModInitializationFailure, TryAnalyzeModName(modId?.TrimEnd('\r', '\n')));
                }
            }

            if (_logCrash is not null)
            {
                if (_logCrash.Contains("\tBlock location: World: ", StringComparison.Ordinal))
                {
                    AppendReason(
                        CrashReason.ProblematicBlock,
                        $"{MatchSingle(_logCrash, "(?<=\\tBlock: Block\\{)[^\\}]+") ?? string.Empty} {MatchSingle(_logCrash, "(?<=\\tBlock location: World: )\\([^\\)]+\\)") ?? string.Empty}".Trim());
                }

                if (_logCrash.Contains("\tEntity's Exact location: ", StringComparison.Ordinal))
                {
                    var entityType = MatchSingle(_logCrash, "(?<=\\tEntity Type: )[^\n]+(?= \\()") ?? string.Empty;
                    var entityLocation = MatchSingle(_logCrash, "(?<=\\tEntity's Exact location: )[^\n]+")?.Trim() ?? string.Empty;
                    AppendReason(CrashReason.ProblematicEntity, $"{entityType} ({entityLocation})".Trim());
                }
            }
        }


        private List<string> AnalyzeStackKeyword(string? errorStack)
        {
            var stack = $"\n{errorStack ?? string.Empty}\n";
            var stackMatches = MatchMany(stack, "(?<=\\n[^{]+)[a-zA-Z_]+\\w+\\.[a-zA-Z_]+[\\w\\.]+(?=\\.[\\w\\.$]+\\.)");
            stackMatches.AddRange(MatchMany(stack, "(?<=at [^(]+?\\.\\w+\\$\\w+\\$)[\\w$]+?(?=\\$\\w+\\()").Select(match => match.Replace('$', '.')));

            var possibleStacks = stackMatches
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(value =>
                {
                    var ignoredPrefixes = new[]
                    {
                        "java", "sun", "javax", "jdk", "oolloo",
                        "org.lwjgl", "com.sun", "net.minecraftforge", "paulscode.sound", "com.mojang", "net.minecraft", "cpw.mods", "com.google", "org.apache", "org.spongepowered", "net.fabricmc", "com.mumfrey", "org.quiltmc",
                        "com.electronwill.nightconfig", "it.unimi.dsi", "MojangTricksIntelDriversForPerformance_javaw"
                    };
                    return ignoredPrefixes.All(prefix => !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            var possibleWords = new List<string>();
            foreach (var possibleStack in possibleStacks)
            {
                var parts = possibleStack.Split('.');
                for (var index = 0; index <= Math.Min(3, parts.Length - 1); index++)
                {
                    var word = parts[index];
                    if (word.Length <= 2 || word.StartsWith("func_", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (IgnoredStackKeywords.Contains(word, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    possibleWords.Add(word.Trim());
                }
            }

            var keywords = possibleWords
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return keywords.Count > 10 ? [] : keywords;
        }


        private List<string>? AnalyzeModName(IEnumerable<string> keywords)
        {
            var modFileNames = new List<string>();
            var realKeywords = keywords
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .SelectMany(keyword => keyword.Split('(').Select(part => part.Trim(' ', ')')))
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .ToList();

            if (_logCrash is not null && _logCrash.Contains("A detailed walkthrough of the error", StringComparison.Ordinal))
            {
                var details = _logCrash.Replace("A detailed walkthrough of the error", "¨", StringComparison.Ordinal);
                var isFabricDetail = details.Contains("Fabric Mods", StringComparison.Ordinal);
                if (isFabricDetail)
                {
                    details = details.Replace("Fabric Mods", "¨", StringComparison.Ordinal);
                }

                var isQuiltDetail = details.Contains("quilt-loader", StringComparison.OrdinalIgnoreCase);
                if (isQuiltDetail)
                {
                    details = details.Replace("Mod Table Version", "¨", StringComparison.Ordinal);
                }

                details = AfterLast(details, "¨");
                var modNameLines = SplitLines(details)
                    .Where(line =>
                        (line.Contains(".jar", StringComparison.OrdinalIgnoreCase) &&
                         CountOccurrences(line, ".jar", StringComparison.OrdinalIgnoreCase) == 1) ||
                        (isFabricDetail &&
                         line.StartsWith("\t\t", StringComparison.Ordinal) &&
                         !Regex.IsMatch(line, @"\t\tfabric[\w-]*: Fabric")))
                    .ToList();

                var hintLines = new List<string>();
                foreach (var keyword in realKeywords)
                {
                    foreach (var modString in modNameLines)
                    {
                        var normalized = modString.ToLowerInvariant().Replace("_", string.Empty, StringComparison.Ordinal);
                        var normalizedKeyword = keyword.ToLowerInvariant().Replace("_", string.Empty, StringComparison.Ordinal);
                        if (!normalized.Contains(normalizedKeyword, StringComparison.Ordinal) ||
                            normalized.Contains("minecraft.jar", StringComparison.Ordinal) ||
                            normalized.Contains(" forge-", StringComparison.Ordinal) ||
                            normalized.Contains(" mixin-", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        hintLines.Add(modString.Trim());
                        break;
                    }
                }

                foreach (var line in hintLines.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var name = isFabricDetail
                        ? MatchSingle(line, "(?<=: )[^\n]+(?= [^\n]+)")
                        : MatchSingle(line, "(?<=\\()[^\\t]+.jar(?=\\))|(?<=((\\t\\t)|(\\| )))[^\\t\\|]+.jar", RegexOptions.IgnoreCase);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        modFileNames.Add(name);
                    }
                }
            }

            if (_logMcDebug is not null)
            {
                var modNameLines = MatchMany(_logMcDebug, "(?<=valid mod file ).*", RegexOptions.Multiline);
                var hintLines = new List<string>();
                foreach (var keyword in realKeywords)
                {
                    hintLines.AddRange(modNameLines.Where(line => line.Contains($"{{{keyword}}}", StringComparison.Ordinal)));
                }

                foreach (var line in hintLines.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var name = MatchSingle(line, ".*(?= with)");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        modFileNames.Add(name);
                    }
                }
            }

            var results = modFileNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return results.Count == 0 ? null : results;
        }


        private List<string> TryAnalyzeModName(string? keyword)
        {
            var rawList = new List<string> { keyword ?? string.Empty };
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return rawList;
            }

            return AnalyzeModName(rawList) ?? rawList;
        }


        private List<string> TryAnalyzeModName(IEnumerable<string> keywords)
        {
            var keywordList = keywords
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .ToList();
            if (keywordList.Count == 0)
            {
                return [];
            }

            return AnalyzeModName(keywordList) ?? keywordList;
        }


        private void AppendReason(CrashReason reason, string? additional = null)
        {
            AppendReason(
                reason,
                string.IsNullOrWhiteSpace(additional)
                    ? null
                    : new[] { additional });
        }


        private void AppendReason(CrashReason reason, IEnumerable<string>? additional)
        {
            if (!_reasons.TryGetValue(reason, out var existing))
            {
                existing = [];
                _reasons.Add(reason, existing);
            }

            if (additional is null)
            {
                return;
            }

            foreach (var value in additional)
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    !existing.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    existing.Add(value);
                }
            }
        }


        private string GetAnalyzeResult()
        {
            if (_reasons.Count == 0)
            {
                return $"{Text("crash.analysis.generic_failure.message", "Sorry, your game encountered a problem...")}\r\n{Text("crash.analysis.help_suffix", "If you need help, send the crash report file to the other person instead of a photo or screenshot of this window.")}";
            }

            var results = new List<string>();
            const string loaderIncompatiblePrefix = "The mod loader version is incompatible with the mod. Go to Instance Settings - Modify and change the loader version.\n\nDetails:\n";

            foreach (var (reason, additional) in _reasons)
            {
                switch (reason)
                {
                    case CrashReason.ExtractedModJar:
                        results.Add("A mod archive was extracted, which prevented the game from continuing.\nPlace the entire mod file directly in the Mods folder; extracting it will break the game.\n\nRemove the extracted mod from the Mods folder, then start the game again.");
                        break;
                    case CrashReason.OutOfMemory:
                        results.Add("Minecraft ran out of memory and could not continue.\nThis is usually caused by low system memory, too little memory allocated to the game, or a pack that is too demanding.\n\nIncrease the memory allocated to the game and remove demanding resource packs, mods, or shaders.\nIf that does not help, close other software before launching, or... try a different computer?\\h");
                        break;
                    case CrashReason.UsingOpenJ9:
                        results.Add("The game crashed because it is using OpenJ9.\nIn the Java selection for launch settings, switch to a non-OpenJ9 Java and start the game again.");
                        break;
                    case CrashReason.UsingJdk:
                        results.Add("The game appears to have crashed because it is using a JDK or a Java version that is too new.\nIn the Java selection for launch settings, switch to JRE 8 (Java 8) and start the game again.\nIf you do not have JRE 8 installed, download and install it.");
                        break;
                    case CrashReason.JavaTooHigh:
                        results.Add("The game appears to have crashed because the Java version is too new.\nIn the Java selection for launch settings, switch to an older Java version and start the game again.\nIf needed, download and install one from the internet.");
                        break;
                    case CrashReason.IncompatibleJava:
                        results.Add("The current Java version is incompatible with the game.\nIf you do not have a suitable Java installed, download and install one from the internet.");
                        break;
                    case CrashReason.SpecialCharsInModName:
                        results.Add("A mod name contains special characters, which caused the game to crash.\nRename the mod file so it uses only common characters such as letters, numbers, hyphens, and underscores, then start the game again.");
                        break;
                    case CrashReason.MissingMixinBootstrap:
                        results.Add("The game could not continue because MixinBootstrap is missing.\nTry reinstalling the matching Forge version or the relevant modpack.");
                        break;
                    case CrashReason.StackKeyword:
                        results.Add(additional.Count == 1
                            ? $"Your game encountered a problem, and PCL found a suspicious keyword: {additional[0]}.\n\nIf you know which mod matches that keyword, it may be responsible for the crash. You can also inspect the crash report for details.\\h"
                            : $"Your game encountered a problem, and PCL found the following suspicious keywords:\n - {string.Join(", ", additional)}\n\nIf you know which mod matches one of those keywords, it may be responsible for the crash. You can also inspect the crash report for details.\\h");
                        break;
                    case CrashReason.StackModName:
                    case CrashReason.SuspectedModCrash:
                        results.Add(additional.Count == 1
                            ? $"PCL suspects the mod named {additional[0]} caused the game to fail, but it is not certain.\nTry disabling that mod and see whether the crash still happens.\n\\e\\h"
                            : $"PCL suspects the following mods caused the game to fail, but it is not certain:\n - {string.Join("\n - ", additional)}\n\nTry disabling them one by one and see whether the crash still happens.\n\\e\\h");
                        break;
                    case CrashReason.ConfirmedModCrash:
                        results.Add(additional.Count == 1
                            ? $"The mod named {additional[0]} caused the game to fail.\nTry disabling that mod and see whether the crash still happens.\n\\e\\h"
                            : $"The following mods caused the game to fail:\n - {string.Join("\n - ", additional)}\n\nTry disabling them one by one and see whether the crash still happens.\n\\e\\h");
                        break;
                    case CrashReason.ModMixinFailure:
                        if (additional.Count == 0)
                        {
                            results.Add("Some mods failed to inject, which caused the game to fail.\nThis usually means one or more mods are incompatible with other mods or the current environment, or that they contain a bug.\nTry disabling mods gradually to identify the one causing the crash.\n\\e\\h");
                        }
                        else if (additional.Count == 1)
                        {
                            results.Add($"The mod named {additional[0]} failed to inject, which caused the game to fail.\nThis usually means it is incompatible with other mods or the current environment, or that it contains a bug.\nTry disabling that mod and see whether the crash still happens.\n\\e\\h");
                        }
                        else
                        {
                            results.Add($"The following mods caused the game to fail:\n - {string.Join("\n - ", additional)}\nThis usually means they are incompatible with other mods or the current environment, or that they contain bugs.\nTry disabling them one by one and see whether the crash still happens.\n\\e\\h");
                        }

                        break;
                    case CrashReason.ModConfigCrash:
                        results.Add(additional.Count >= 2
                            ? $"The mod named {additional[0]} caused the game to fail:\nIts configuration file {additional[1]} is invalid and cannot be read."
                            : $"The mod named {additional.FirstOrDefault() ?? "unknown"} caused the game to fail.\n\\e\\h");
                        break;
                    case CrashReason.ModInitializationFailure:
                        results.Add(additional.Count == 1
                            ? $"The mod named {additional[0]} failed to initialize, which prevented the game from continuing to load.\nTry disabling that mod and see whether the crash still happens.\n\\e\\h"
                            : $"The following mods failed to initialize, which caused the game to fail:\n - {string.Join("\n - ", additional)}\n\nTry disabling them one by one and see whether the crash still happens.\n\\e\\h");
                        break;
                    case CrashReason.ProblematicBlock:
                        results.Add(additional.Count == 1
                            ? $"The game appears to have a problem with block {additional[0]}.\n\nCreate a new world and observe the game:\n - If it runs normally, that block is likely causing the issue and you may need to remove it somehow.\n - If it still crashes, the cause is probably something else...\\h"
                            : "The game appears to have a problem with some blocks in the world.\n\nCreate a new world and observe the game:\n - If it runs normally, some blocks are likely causing the issue and you may need to delete the world.\n - If it still crashes, the cause is probably something else...\\h");
                        break;
                    case CrashReason.DuplicateMods:
                        results.Add(additional.Count >= 2
                            ? $"You have installed multiple copies of the same mod:\n - {string.Join("\n - ", additional)}\n\nEach mod can appear only once. Remove the duplicates, then start the game again."
                            : "You may have installed multiple copies of the same mod, which caused the game to fail.\n\nEach mod can appear only once. Remove the duplicates, then start the game again.\\e\\h");
                        break;
                    case CrashReason.ProblematicEntity:
                        results.Add(additional.Count == 1
                            ? $"The game appears to have a problem with entity {additional[0]}.\n\nCreate a new world and spawn that entity, then observe the game:\n - If it runs normally, that entity is likely causing the issue and you may need to remove it somehow.\n - If it still crashes, the cause is probably something else...\\h"
                            : "The game appears to have a problem with some entities in the world.\n\nCreate a new world and spawn various entities, then observe the game:\n - If it runs normally, some entities are likely causing the issue and you may need to delete the world.\n - If it still crashes, the cause is probably something else...\\h");
                        break;
                    case CrashReason.OptiFineForgeIncompatible:
                        results.Add("OptiFine is incompatible with the current Forge version, which caused the game to crash.\n\nVisit the OptiFine website (https://optifine.net/downloads) to check which Forge versions it supports, then reinstall the game using the matching versions.");
                        break;
                    case CrashReason.ShadersModWithOptiFine:
                        results.Add("You do not need to install both OptiFine and Shaders Mod. OptiFine already includes Shaders Mod functionality.\nRemove Shaders Mod and the game should run normally.");
                        break;
                    case CrashReason.LegacyForgeHighJavaIncompatible:
                        results.Add("An older Forge version is incompatible with the current Java version, which caused the game to crash.\n\nTry the following:\n - Update Forge to 36.2.26 or later\n - Switch to a Java version below 1.8.0.320");
                        break;
                    case CrashReason.MultipleForgeInJson:
                        results.Add("Another launcher may have modified the Forge version, leaving the current instance files in an invalid state and causing the game to crash.\nTry reinstalling Forge from scratch instead of modifying the Forge version with another launcher.");
                        break;
                    case CrashReason.ManualDebugCrash:
                        results.Add(Text(
                            "crash.analysis.manual_debug.message",
                            "* In fact, there is nothing wrong with your game. This crash was triggered intentionally.\n* Do you not have something more important to do?"));
                        break;
                    case CrashReason.ModNeedsJava11:
                        results.Add("Some of the installed mods appear to require Java 11.\nIn the Java selection for launch settings, switch to Java 11 and start the game again.\nIf you do not have Java 11 installed, download and install it.");
                        break;
                    case CrashReason.VeryShortOutput:
                        results.Add($"The program returned the following information:\n{additional.FirstOrDefault() ?? string.Empty}\n\\h");
                        break;
                    case CrashReason.OptiFineWorldLoadFailure:
                        results.Add("The OptiFine version you are using may be causing the game to fail.\n\nThis issue only occurs in specific OptiFine versions, so try changing the OptiFine version.\\h");
                        break;
                    case CrashReason.PixelFormatUnsupported:
                    case CrashReason.IntelDriverAccessViolation:
                    case CrashReason.AmdDriverAccessViolation:
                    case CrashReason.NvidiaDriverAccessViolation:
                    case CrashReason.UnsupportedOpenGl:
                        results.Add(_logAll.Contains("hd graphics ", StringComparison.OrdinalIgnoreCase)
                            ? "There is a problem with your graphics driver, or the game is not using the discrete GPU, which prevents it from running normally.\n\nIf your computer has a discrete GPU, start PCL and Minecraft on the discrete GPU instead of the Intel integrated GPU.\nIf the problem persists, try updating the graphics driver to the latest version or rolling back to the factory version.\nIf that still does not help, try Java 8.0.51 or an earlier version.\\h"
                            : "There is a problem with your graphics driver, which prevents the game from running normally.\n\nTry updating the graphics driver to the latest version or rolling back to the factory version, then start the game again.\nIf that still does not help, try Java 8.0.51 or an earlier version.\nIf the problem persists, you may need a better graphics card...\\h");
                        break;
                    case CrashReason.TooLargeResourcePackOrWeakGpu:
                        results.Add("The resource pack resolution is too high, or the GPU is too weak, which prevented the game from continuing.\n\nIf you are using high-resolution textures, remove them.\nIf you are not using a resource pack, you may need to update the graphics driver or use a better GPU...\\h");
                        break;
                    case CrashReason.NightConfigBug:
                        results.Add("A problem in Night Config caused the game to crash.\nTry installing the Night Config Fixes mod, which may resolve the issue.\\h");
                        break;
                    case CrashReason.OpenGl1282FromShadersOrResources:
                        results.Add("The shaders or resource packs you are using caused a problem with the game...\n\nTry removing those additional resources.\\h");
                        break;
                    case CrashReason.ExceededIdLimit:
                        results.Add("You have installed too many mods, exceeding the game's ID limit and causing a crash.\nTry installing a fix mod such as JEID, or remove some large mods.");
                        break;
                    case CrashReason.FileValidationFailed:
                        results.Add("Some files or content failed validation, which caused a problem with the game.\n\nTry deleting the game (including mods) and downloading it again, or try using a VPN while re-downloading.\\h");
                        break;
                    case CrashReason.IncompleteForgeInstall:
                        results.Add("Some installed Forge files are missing, which prevented the game from running normally.\nGo to instance settings, reset the instance, and then start the game again.\nDeleting the libraries folder while packaging the game can cause this error.\\h");
                        break;
                    case CrashReason.FabricError:
                        results.Add(additional.Count == 1
                            ? $"Fabric provided the following error information:\n{additional[0]}\n\nUse the information above to take the appropriate action. If you do not understand the English text, use a translation tool."
                            : "Fabric may already have provided an error message. Use the log information in the crash report to take the appropriate action. If you do not understand the English text, use a translation tool.\\h");
                        break;
                    case CrashReason.IncompatibleMods:
                        if (additional.Count == 1)
                        {
                            var info = additional[0];
                            results.Add(RegexPatterns.IncompatibleModLoaderErrorHint.IsMatch(info)
                                ? loaderIncompatiblePrefix + info
                                : $"The installed mod is incompatible:\n{info}\n\nUse the information above to take the appropriate action. If you do not understand the English text, use a translation tool.");
                        }
                        else
                        {
                            results.Add("The installed mods are incompatible. The mod loader may already have provided error information. Use the log information in the crash report to take the appropriate action. If you do not understand the English text, use a translation tool.\\h");
                        }

                        break;
                    case CrashReason.ModLoaderFailure:
                        results.Add(additional.Count == 1
                            ? $"The mod loader provided the following error information:\n{additional[0]}\n\nUse the information above to take the appropriate action. If you do not understand the English text, use a translation tool."
                            : "The mod loader may already have provided error information. Use the log information in the crash report to take the appropriate action. If you do not understand the English text, use a translation tool.\\h");
                        break;
                    case CrashReason.FabricSolution:
                        results.Add(additional.Count == 1
                            ? $"Fabric provided the following solution:\n{additional[0]}\n\nUse the information above to take the appropriate action. If you do not understand the English text, use a translation tool."
                            : "Fabric may already have provided a solution. Use the log information in the crash report to take the appropriate action. If you do not understand the English text, use a translation tool.\\h");
                        break;
                    case CrashReason.ForgeError:
                        results.Add(additional.Count == 1
                            ? $"Forge provided the following error information:\n{additional[0]}\n\nUse the information above to take the appropriate action. If you do not understand the English text, use a translation tool."
                            : "Forge may already have provided error information. Use the log information in the crash report to take the appropriate action. If you do not understand the English text, use a translation tool.\\h");
                        break;
                    case CrashReason.NoAnalysisFiles:
                        results.Add("Your game encountered a problem, but PCL could not find any relevant log files, so it cannot analyze the crash.\\h");
                        break;
                    case CrashReason.MissingDependencyOrWrongMcVersion:
                        results.Add(additional.Count > 0
                            ? $"A mod is missing a dependency, or the installed mod does not match the current Minecraft version:\n - {string.Join("\n - ", additional)}\n\nCheck the dependency chain, mod version, and game version using the information above."
                            : "Some mods are missing dependencies or do not match the current Minecraft version.\nCheck the dependency chain, mod version, and game version using the log information in the crash report.");
                        break;
                    default:
                        results.Add($"PCL found an error reason without detailed information ({reason}). Please report it to the PCL author for more details.\\h");
                        break;
                }
            }

            var combined = string.Join("\n\nAdditionally, ", results)
                .Replace("\n", "\r\n", StringComparison.Ordinal)
                .Replace("\\h", string.Empty, StringComparison.Ordinal)
                .Replace("\\e", "\r\nYou can inspect the crash report to see exactly how the error occurred.", StringComparison.Ordinal)
                .Trim('\r', '\n');

            if (!results.Any(result => result.EndsWith("\\h", StringComparison.Ordinal)))
            {
                combined += $"\r\n{Text("crash.analysis.help_suffix", "If you need help, send the crash report file to the other person instead of a photo or screenshot of this window.")}";
            }

            return combined;
        }

    }
}
