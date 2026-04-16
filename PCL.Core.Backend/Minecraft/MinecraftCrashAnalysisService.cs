using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft;

public static class MinecraftCrashAnalysisService
{
    public static MinecraftCrashAnalysisResult Analyze(MinecraftCrashAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourceFilePaths);

        var analyzer = new Analyzer(request);
        analyzer.Collect();
        analyzer.Prepare();
        analyzer.Analyze();
        return analyzer.BuildResult();
    }

    private sealed class Analyzer
    {
        private readonly MinecraftCrashAnalysisRequest _request;
        private readonly List<AnalyzedFile> _rawFiles = [];
        private readonly Dictionary<CrashReason, List<string>> _reasons = [];

        private string? _logMc;
        private string? _logMcDebug;
        private string? _logHs;
        private string? _logCrash;
        private string _logAll = string.Empty;
        private string? _directFilePath;

        public Analyzer(MinecraftCrashAnalysisRequest request)
        {
            _request = request;
        }

        public void Collect()
        {
            foreach (var path in EnumerateCandidatePaths())
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    continue;
                }

                try
                {
                    var lines = SplitLines(File.ReadAllText(path));
                    if (lines.Length == 0)
                    {
                        continue;
                    }

                    _rawFiles.Add(new AnalyzedFile(path, lines));
                }
                catch
                {
                    // Ignore unreadable artifacts and continue with the rest.
                }
            }
        }

        public void Prepare()
        {
            _directFilePath = null;

            var files = new List<(AnalyzeFileType Type, AnalyzedFile File)>();
            foreach (var rawFile in _rawFiles)
            {
                var fileType = Classify(rawFile);
                if (fileType is null)
                {
                    continue;
                }

                files.Add((fileType.Value, rawFile));
            }

            if (files.Count > 0 && files.All(entry => entry.Type == AnalyzeFileType.ExtraLogFile))
            {
                files = files
                    .Select(entry => (AnalyzeFileType.MinecraftLog, entry.File))
                    .ToList();
            }

            LoadNewestFile(files, AnalyzeFileType.HsErr, lines => _logHs = GetHeadTailLines(lines, 200, 100));
            LoadNewestFile(files, AnalyzeFileType.CrashReport, lines => _logCrash = GetHeadTailLines(lines, 300, 700));

            var minecraftLogs = files
                .Where(entry => entry.Type == AnalyzeFileType.MinecraftLog)
                .Select(entry => entry.File)
                .ToArray();
            if (minecraftLogs.Length > 0)
            {
                BuildMinecraftLogs(minecraftLogs);
            }
        }

        public void Analyze()
        {
            _logAll = $"{_logMc}{_logMcDebug}{_logHs}{_logCrash}";
            if (_logAll.Contains("quilt", StringComparison.OrdinalIgnoreCase) &&
                _logAll.Contains("Mod Table Version", StringComparison.Ordinal))
            {
                var tableStart = _logAll.IndexOf("| Index", StringComparison.Ordinal);
                var tableEnd = _logAll.IndexOf("Mod Table Version:", StringComparison.Ordinal);
                if (tableStart >= 0 && tableEnd >= 0 && tableEnd < _logAll.Length)
                {
                    _logAll = _logAll[..tableStart] + _logAll[(tableEnd + "Mod Table Version:".Length)..];
                }
            }

            AnalyzeCriticalPriorityOne();
            if (_reasons.Count > 0)
            {
                return;
            }

            AnalyzeCriticalPriorityTwo();
            if (_reasons.Count > 0)
            {
                return;
            }

            AnalyzeStack();
            if (_reasons.Count > 0)
            {
                return;
            }

            AnalyzeCriticalPriorityThree();
        }

        public MinecraftCrashAnalysisResult BuildResult()
        {
            var resultText = GetAnalyzeResult();
            var hasKnownReason = _reasons.Keys.Any(reason => reason != CrashReason.NoAnalysisFiles);
            var hasModLoaderVersionMismatch = HasModLoaderVersionMismatch();
            return new MinecraftCrashAnalysisResult(
                resultText,
                hasKnownReason,
                _directFilePath is not null,
                _directFilePath,
                hasModLoaderVersionMismatch);
        }

        private bool HasModLoaderVersionMismatch()
        {
            return _reasons.TryGetValue(CrashReason.IncompatibleMods, out var details)
                   && details.Any(RegexPatterns.IncompatibleModLoaderErrorHint.IsMatch);
        }

        private IEnumerable<string> EnumerateCandidatePaths()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in _request.SourceFilePaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && seen.Add(Path.GetFullPath(path)))
                {
                    yield return path;
                }
            }

            if (!string.IsNullOrWhiteSpace(_request.CurrentLauncherLogFilePath))
            {
                var launcherLogPath = Path.GetFullPath(_request.CurrentLauncherLogFilePath);
                if (seen.Add(launcherLogPath))
                {
                    yield return _request.CurrentLauncherLogFilePath;
                }
            }
        }

        private AnalyzeFileType? Classify(AnalyzedFile file)
        {
            var matchName = Path.GetFileName(file.Path).ToLowerInvariant();
            if (matchName.StartsWith("hs_err", StringComparison.Ordinal))
            {
                _directFilePath ??= file.Path;
                return AnalyzeFileType.HsErr;
            }

            if (matchName.StartsWith("crash-", StringComparison.Ordinal))
            {
                _directFilePath ??= file.Path;
                return AnalyzeFileType.CrashReport;
            }

            if (matchName is "latest.log"
                or "latest log.txt"
                or "debug.log"
                or "debug log.txt"
                or "pre-crash output.txt"
                or "游戏崩溃前的输出.txt"
                or "rawoutput.log")
            {
                _directFilePath ??= file.Path;
                return AnalyzeFileType.MinecraftLog;
            }

            if (PathsEqual(file.Path, _request.CurrentLauncherLogFilePath) ||
                matchName is "pcl launcher log.txt"
                    or "launcher log.txt"
                    or "启动器日志.txt"
                    or "pcl2 启动器日志.txt"
                    or "pcl 启动器日志.txt"
                    or "log1.txt"
                    or "log-ce1.log"
                    or "pcl.log")
            {
                if (file.Lines.Any(line => line.Contains("以下为游戏输出的最后一段内容", StringComparison.Ordinal)))
                {
                    _directFilePath ??= file.Path;
                    return AnalyzeFileType.MinecraftLog;
                }

                return AnalyzeFileType.ExtraLogFile;
            }

            if (matchName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzeFileType.ExtraLogFile;
            }

            if (matchName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzeFileType.ExtraReportFile;
            }

            return null;
        }

        private static void LoadNewestFile(
            IEnumerable<(AnalyzeFileType Type, AnalyzedFile File)> files,
            AnalyzeFileType type,
            Action<string[]> assign)
        {
            var candidate = files
                .Where(entry => entry.Type == type)
                .Select(entry => entry.File)
                .OrderBy(entry => GetLastWriteTime(entry.Path))
                .LastOrDefault();
            if (candidate is not null)
            {
                assign(candidate.Lines);
            }
        }

        private void BuildMinecraftLogs(IReadOnlyList<AnalyzedFile> selectedFiles)
        {
            var filesByName = new Dictionary<string, AnalyzedFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in selectedFiles)
            {
                filesByName[Path.GetFileName(file.Path)] = file;
            }

            var logMcBuilder = new StringBuilder();
            foreach (var fileName in new[]
                     {
                         "rawoutput.log",
                         "Pre-Crash Output.txt",
                         "PCL Launcher Log.txt",
                         "启动器日志.txt",
                         "log1.txt",
                         "log-ce1.log",
                         "游戏崩溃前的输出.txt",
                         "PCL2 启动器日志.txt",
                         "PCL 启动器日志.txt",
                         "PCL.log"
                     })
            {
                if (!filesByName.TryGetValue(fileName, out var currentLog))
                {
                    continue;
                }

                var hasMarker = false;
                foreach (var line in currentLog.Lines)
                {
                    if (hasMarker)
                    {
                        logMcBuilder.AppendLine(line);
                    }
                    else if (line.Contains("以下为游戏输出的最后一段内容", StringComparison.Ordinal))
                    {
                        hasMarker = true;
                    }
                }

                if (!hasMarker)
                {
                    logMcBuilder.Append(GetHeadTailLines(currentLog.Lines, 0, 500));
                }

                break;
            }

            foreach (var fileName in new[] { "latest.log", "latest log.txt", "debug.log", "debug log.txt" })
            {
                if (!filesByName.TryGetValue(fileName, out var currentLog))
                {
                    continue;
                }

                logMcBuilder.Append(GetHeadTailLines(currentLog.Lines, 1500, 500));
                break;
            }

            foreach (var fileName in new[] { "debug.log", "debug log.txt" })
            {
                if (!filesByName.TryGetValue(fileName, out var currentLog))
                {
                    continue;
                }

                _logMcDebug = GetHeadTailLines(currentLog.Lines, 1000, 0);
                break;
            }

            _logMc = TrimToNull(logMcBuilder.ToString());

            if (_logMc is null)
            {
                if (_logMcDebug is not null)
                {
                    _logMc = _logMcDebug;
                }
                else if (selectedFiles.Count > 0)
                {
                    _logMc = GetHeadTailLines(selectedFiles[0].Lines, 1500, 500);
                }
            }
        }

        private void AnalyzeCriticalPriorityOne()
        {
            if (_logMc is null && _logHs is null && _logCrash is null)
            {
                AppendReason(CrashReason.NoAnalysisFiles);
                return;
            }

            if (_logCrash is not null &&
                _logCrash.Contains("Unable to make protected final java.lang.Class java.lang.ClassLoader.defineClass", StringComparison.Ordinal))
            {
                AppendReason(CrashReason.JavaTooHigh);
            }

            if (_logMc is not null)
            {
                if (_logMc.Contains("Found multiple arguments for option fml.forgeVersion, but you asked for only one", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.MultipleForgeInJson);
                }

                if (_logMc.Contains("The driver does not appear to support OpenGL", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.UnsupportedOpenGl);
                }

                if (_logMc.Contains("java.lang.ClassCastException: java.base/jdk", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.ClassCastException: class jdk.", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.UsingJdk);
                }

                if (_logMc.Contains("TRANSFORMER/net.optifine/net.optifine.reflect.Reflector.<clinit>(Reflector.java", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.texture.SpriteContents.<init>", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.NoSuchMethodError: 'java.lang.String com.mojang.blaze3d.systems.RenderSystem.getBackendDescription", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui.renderSelectedItemName", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.NoSuchMethodError: 'net.minecraft.network.chat.FormattedText net.minecraft.client.gui.Font.ellipsize", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.OptiFineForgeIncompatible);
                }

                if (_logMc.Contains("Open J9 is not supported", StringComparison.Ordinal) ||
                    _logMc.Contains("OpenJ9 is incompatible", StringComparison.Ordinal) ||
                    _logMc.Contains(".J9VMInternals.", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.UsingOpenJ9);
                }

                if (_logMc.Contains("java.lang.NoSuchFieldException: ucp", StringComparison.Ordinal) ||
                    _logMc.Contains("because module java.base does not export", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.JavaTooHigh);
                }

                if (_logMc.Contains("The directories below appear to be extracted jar files. Fix this before you continue.", StringComparison.Ordinal) ||
                    _logMc.Contains("Extracted mod jars found, loading will NOT continue", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ExtractedModJar);
                }

                if (_logMc.Contains("java.lang.ClassNotFoundException: org.spongepowered.asm.launch.MixinTweaker", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.MissingMixinBootstrap);
                }

                if (_logMc.Contains("Couldn't set pixel format", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.PixelFormatUnsupported);
                }

                if (_logMc.Contains("java.lang.OutOfMemoryError", StringComparison.Ordinal) ||
                    _logMc.Contains("an out of memory error", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.OutOfMemory);
                }

                if (_logMc.Contains("java.lang.RuntimeException: Shaders Mod detected. Please remove it, OptiFine has built-in support for shaders.", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ShadersModWithOptiFine);
                }

                if (_logMc.Contains("java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.LegacyForgeHighJavaIncompatible);
                }

                if (_logMc.Contains("1282: Invalid operation", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.OpenGl1282FromShadersOrResources);
                }

                if (_logMc.Contains("signer information does not match signer information of other classes in the same package", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.FileValidationFailed, MatchSingle(_logMc, "(?<=class \")[^']+(?=\"'s signer information)")?.Trim());
                }

                if (_logMc.Contains("Maybe try a lower resolution resourcepack?", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.TooLargeResourcePackOrWeakGpu);
                }

                if (_logMc.Contains("java.lang.NoSuchMethodError: net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks(J)Z", StringComparison.Ordinal) &&
                    _logMc.Contains("OptiFine", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.OptiFineWorldLoadFailure);
                }

                if (_logMc.Contains("Unsupported class file major version", StringComparison.Ordinal) ||
                    _logMc.Contains("Unsupported major.minor version", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.IncompatibleJava);
                }

                if (_logMc.Contains("com.electronwill.nightconfig.core.io.ParsingException: Not enough data available", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.NightConfigBug);
                }

                if (_logMc.Contains("Cannot find launch target fmlclient, unable to launch", StringComparison.Ordinal) ||
                    (_logMc.Contains("Invalid paths argument, contained no existing paths", StringComparison.Ordinal) &&
                     _logMc.Contains("libraries\\net\\minecraftforge\\fmlcore", StringComparison.Ordinal)))
                {
                    AppendReason(CrashReason.IncompleteForgeInstall);
                }

                if (_logMc.Contains("Invalid module name: '' is not a Java identifier", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.SpecialCharsInModName);
                }

                if (_logMc.Contains("has been compiled by a more recent version of the Java Runtime (class file version 55.0), this version of the Java Runtime only recognizes class file versions up to", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.RuntimeException: java.lang.NoSuchMethodException: no such method: sun.misc.Unsafe.defineAnonymousClass(Class,byte[],Object[])Class/invokeVirtual", StringComparison.Ordinal) ||
                    _logMc.Contains("java.lang.IllegalArgumentException: The requested compatibility level JAVA_11 could not be set. Level is not supported by the active JRE or ASM version", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ModNeedsJava11);
                }

                if (_logMc.Contains("Invalid maximum heap size", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.Java32BitInsufficientMemory);
                }

                if (_logMc.Contains("Could not reserve enough space", StringComparison.Ordinal))
                {
                    if (_logMc.Contains("for 1048576KB object heap", StringComparison.Ordinal))
                    {
                        AppendReason(CrashReason.Java32BitInsufficientMemory);
                    }
                    else
                    {
                        AppendReason(CrashReason.OutOfMemory);
                    }
                }

                if (_logMc.Contains("Caught exception from ", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ConfirmedModCrash, TryAnalyzeModName(MatchSingle(_logMc, "(?<=Caught exception from )[^\r\n]+")?.Trim()));
                }

                if (_logMc.Contains("DuplicateModsFoundException", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.DuplicateMods, MatchMany(_logMc, @"(?<=\n\t[\w]+ : [A-Z]:[^\n]+(/|\\))[^/\\\n]+?.jar", RegexOptions.IgnoreCase));
                }

                if (_logMc.Contains("Found a duplicate mod", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.DuplicateMods, MatchMany(MatchSingle(_logMc, "Found a duplicate mod[^\n]+") ?? string.Empty, @"[^\\/]+.jar", RegexOptions.IgnoreCase));
                }

                if (_logMc.Contains("Found duplicate mods", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.DuplicateMods, MatchMany(_logMc, "(?<=Mod ID: ')[\\w-]+?(?=' from mod files:)"));
                }

                if (_logMc.Contains("ModResolutionException: Duplicate", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.DuplicateMods, MatchMany(MatchSingle(_logMc, "ModResolutionException: Duplicate[^\n]+") ?? string.Empty, @"[^\\/]+.jar", RegexOptions.IgnoreCase));
                }

                if (_logMc.Contains("Incompatible mods found!", StringComparison.Ordinal))
                {
                    AppendReason(
                        CrashReason.IncompatibleMods,
                        MatchSingle(_logMc, "(?<=Incompatible mods found!\\s*)[\\s\\S]+?(?=\\n\\tat |\\z)")?.Trim());
                }

                if (_logMc.Contains("Missing or unsupported mandatory dependencies:", StringComparison.Ordinal))
                {
                    AppendReason(
                        CrashReason.MissingDependencyOrWrongMcVersion,
                        MatchMany(
                                _logMc,
                                "(?<=Missing or unsupported mandatory dependencies:)([\\n\\r]+\\t(.*))+",
                                RegexOptions.IgnoreCase)
                            .SelectMany(section => SplitLines(section)
                                .Select(line => line.Trim('\r', '\n', '\t', ' '))
                                .Where(line => !string.IsNullOrWhiteSpace(line)))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList());
                }
            }

            if (_logHs is not null)
            {
                if (_logHs.Contains("The system is out of physical RAM or swap space", StringComparison.Ordinal) ||
                    _logHs.Contains("Out of Memory Error", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.OutOfMemory);
                }

                if (_logHs.Contains("EXCEPTION_ACCESS_VIOLATION", StringComparison.Ordinal))
                {
                    if (_logHs.Contains("# C  [ig", StringComparison.Ordinal))
                    {
                        AppendReason(CrashReason.IntelDriverAccessViolation);
                    }

                    if (_logHs.Contains("# C  [atio", StringComparison.Ordinal))
                    {
                        AppendReason(CrashReason.AmdDriverAccessViolation);
                    }

                    if (_logHs.Contains("# C  [nvoglv", StringComparison.Ordinal))
                    {
                        AppendReason(CrashReason.NvidiaDriverAccessViolation);
                    }
                }
            }

            if (_logCrash is not null)
            {
                if (_logCrash.Contains("maximum id range exceeded", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ExceededIdLimit);
                }

                if (_logCrash.Contains("java.lang.OutOfMemoryError", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.OutOfMemory);
                }

                if (_logCrash.Contains("Pixel format not accelerated", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.PixelFormatUnsupported);
                }

                if (_logCrash.Contains("Manually triggered debug crash", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ManualDebugCrash);
                }

                if (_logCrash.Contains("has mods that were not found", StringComparison.Ordinal) &&
                    Regex.IsMatch(_logCrash, @"The Mod File [^\n]+optifine\\OptiFine[^\n]+ has mods that were not found"))
                {
                    AppendReason(CrashReason.OptiFineForgeIncompatible);
                }

                if (_logCrash.Contains("-- MOD ", StringComparison.Ordinal))
                {
                    var logCrashMod = Between(_logCrash, "-- MOD ", "Failure message:");
                    if (logCrashMod.Contains(".jar", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendReason(CrashReason.ConfirmedModCrash, TryAnalyzeModName(MatchSingle(logCrashMod, "(?<=Mod File: ).+")?.Trim()));
                    }
                    else
                    {
                        AppendReason(CrashReason.ModLoaderFailure, MatchSingle(_logCrash, "(?<=Failure message: )[\\w\\W]+?(?=\\tMod)")?.Replace('\t', ' ').Trim());
                    }
                }

                if (_logCrash.Contains("Multiple entries with same key: ", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ConfirmedModCrash, TryAnalyzeModName(MatchSingle(_logCrash, "(?<=Multiple entries with same key: )[^=]+")?.Trim()));
                }

                if (_logCrash.Contains("LoaderExceptionModCrash: Caught exception from ", StringComparison.Ordinal))
                {
                    AppendReason(CrashReason.ConfirmedModCrash, TryAnalyzeModName(MatchSingle(_logCrash, "(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+")?.Trim()));
                }

                if (_logCrash.Contains("Failed loading config file ", StringComparison.Ordinal))
                {
                    var modName = TryAnalyzeModName(MatchSingle(_logCrash, "(?<=Failed loading config file .+ for modid )[^\n]+")?.Trim()).FirstOrDefault();
                    var configPath = MatchSingle(_logCrash, "(?<=Failed loading config file ).+(?= of type)")?.Trim();
                    AppendReason(CrashReason.ModConfigCrash, new[] { modName, configPath }.Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToList());
                }
            }
        }

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
                return "Sorry, your game encountered a problem...\r\nIf you need help, send the crash report file to the other person instead of a photo or screenshot of this window.";
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
                        results.Add("Minecraft ran out of memory and could not continue.\nThis is usually caused by low system memory, too little memory allocated to the game, or a pack that is too demanding.\n\nTry Memory Optimization in More -> Toolbox, then start the game again.\nIf that does not help, increase the memory allocated to the game and remove demanding resource packs, mods, or shaders.\nIf it still fails, close other software before launching, or... try a different computer?\\h");
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
                        results.Add("* In fact, there is nothing wrong with your game. This crash was triggered intentionally.\n* Do you not have something more important to do?");
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
                combined += "\r\nIf you need help, send the crash report file to the other person instead of a photo or screenshot of this window.";
            }

            return combined;
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

    private sealed record AnalyzedFile(
        string Path,
        string[] Lines);

    private enum AnalyzeFileType
    {
        HsErr,
        MinecraftLog,
        ExtraLogFile,
        ExtraReportFile,
        CrashReport
    }

    private enum CrashReason
    {
        ExtractedModJar,
        MissingMixinBootstrap,
        OutOfMemory,
        UsingJdk,
        UnsupportedOpenGl,
        UsingOpenJ9,
        JavaTooHigh,
        IncompatibleJava,
        SpecialCharsInModName,
        PixelFormatUnsupported,
        VeryShortOutput,
        IntelDriverAccessViolation,
        AmdDriverAccessViolation,
        NvidiaDriverAccessViolation,
        ManualDebugCrash,
        OpenGl1282FromShadersOrResources,
        FileValidationFailed,
        ConfirmedModCrash,
        SuspectedModCrash,
        ModConfigCrash,
        ModMixinFailure,
        ModLoaderFailure,
        ModInitializationFailure,
        StackKeyword,
        StackModName,
        OptiFineWorldLoadFailure,
        ProblematicBlock,
        ProblematicEntity,
        TooLargeResourcePackOrWeakGpu,
        NoAnalysisFiles,
        Java32BitInsufficientMemory,
        DuplicateMods,
        IncompatibleMods,
        OptiFineForgeIncompatible,
        FabricError,
        FabricSolution,
        ForgeError,
        LegacyForgeHighJavaIncompatible,
        MultipleForgeInJson,
        ExceededIdLimit,
        NightConfigBug,
        ShadersModWithOptiFine,
        IncompleteForgeInstall,
        ModNeedsJava11,
        MissingDependencyOrWrongMcVersion
    }
}

public sealed record MinecraftCrashAnalysisRequest(
    IReadOnlyList<string> SourceFilePaths,
    string? CurrentLauncherLogFilePath);

public sealed record MinecraftCrashAnalysisResult(
    string ResultText,
    bool HasKnownReason,
    bool HasDirectFile,
    string? DirectFilePath,
    bool HasModLoaderVersionMismatch = false);
