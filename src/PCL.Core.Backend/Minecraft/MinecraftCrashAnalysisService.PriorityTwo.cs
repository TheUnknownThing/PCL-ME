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
        private void AnalyzeCriticalPriorityTwo()
        {
            bool MixinAnalyze(string logText)
            {
                var isMixin =
                    logText.Contains("Mixin prepare failed ", StringComparison.Ordinal) ||
                    logText.Contains("Mixin apply failed ", StringComparison.Ordinal) ||
                    logText.Contains("MixinApplyError", StringComparison.Ordinal) ||
                    logText.Contains("MixinTransformerError", StringComparison.Ordinal) ||
                    logText.Contains("mixin.injection.throwables.", StringComparison.Ordinal) ||
                    logText.Contains(".json] FAILED during )", StringComparison.Ordinal);
                if (!isMixin)
                {
                    return false;
                }

                var modName = MatchSingle(logText, "(?<=from mod )[^./ ]+(?=\\] from)") ??
                              MatchSingle(logText, "(?<=for mod )[^./ ]+(?= failed)");
                if (!string.IsNullOrWhiteSpace(modName))
                {
                    AppendReason(CrashReason.ModMixinFailure, TryAnalyzeModName(modName.Trim()));
                    return true;
                }

                foreach (var jsonName in MatchMany(logText, "(?<=^[^\\t]+[ \\[{(]{1})[^ \\[{(]+\\.[^ ]+(?=\\.json)", RegexOptions.Multiline))
                {
                    AppendReason(CrashReason.ModMixinFailure, TryAnalyzeModName(jsonName.Replace("mixins", "mixin", StringComparison.Ordinal).Replace(".mixin", string.Empty, StringComparison.Ordinal).Replace("mixin.", string.Empty, StringComparison.Ordinal)));
                    return true;
                }

                AppendReason(CrashReason.ModMixinFailure);
                return true;
            }

            if (_logMc is not null)
            {
                var isMixin = MixinAnalyze(_logMc);
                if (_logMc.Contains("An exception was thrown, the game will display an error screen and halt.", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ForgeError, MatchSingle(_logMc, "(?<=the game will display an error screen and halt.[\\n\\r]+[^\\n]+?Exception: )[\\s\\S]+?(?=\\n\\tat)")?.Trim('\r', '\n'));
                }

                foreach (var marker in new[]
                         {
                             "A potential solution has been determined:",
                             "A potential solution has been determined, this may resolve your problem:",
                             "确定了一种可能的解决方法，这样做可能会解决你的问题："
                         })
                {
                    if (_logMc.Contains(marker, StringComparison.Ordinal))
                    {
                        var section = marker switch
                        {
                            "A potential solution has been determined:" => MatchSingle(_logMc, "(?<=A potential solution has been determined:\\n)(\\s+ - [^\\n]+\\n)+"),
                            "A potential solution has been determined, this may resolve your problem:" => MatchSingle(_logMc, "(?<=A potential solution has been determined, this may resolve your problem:\\n)(\\s+ - [^\\n]+\\n)+"),
                            _ => MatchSingle(_logMc, "(?<=确定了一种可能的解决方法，这样做可能会解决你的问题：\\n)(\\s+ - [^\\n]+\\n)+")
                        };
                        var solutions = MatchMany(section ?? string.Empty, "(?<=\\s+)[^\\n]+");
                        AppendReason(CrashReason.FabricSolution, string.Join('\n', solutions));
                    }
                }

                if (!isMixin && _logMc.Contains("due to errors, provided by ", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ConfirmedModCrash, TryAnalyzeModName(MatchSingle(_logMc, "(?<=due to errors, provided by ')[^']+")?.Trim()));
                }
            }

            if (_logCrash is not null)
            {
                MixinAnalyze(_logCrash);
                if (_logCrash.Contains("Suspected Mod", StringComparison.Ordinal))
                {
                    var suspectsRaw = Between(_logCrash, "Suspected Mod", "Stacktrace");
                    if (!suspectsRaw.StartsWith("s: None", StringComparison.Ordinal))
                    {
                        var suspects = MatchMany(suspectsRaw, "(?<=\\n\\t[^(\t]+\\()[^)\\n]+");
                        if (suspects.Count > 0)
                        {
                            AppendReason(CrashReason.SuspectedModCrash, TryAnalyzeModName(suspects));
                        }
                    }
                }
            }
        }

    }
}
