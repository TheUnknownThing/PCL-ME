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
                return "很抱歉，你的游戏出现了一些问题……\r\n如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。";
            }

            var results = new List<string>();
            const string loaderIncompatiblePrefix = "Mod 加载器版本与 Mod 不兼容，请前往 实例设置 - 修改 更换加载器版本。\n\n详细信息：\n";

            foreach (var (reason, additional) in _reasons)
            {
                switch (reason)
                {
                    case CrashReason.ExtractedModJar:
                        results.Add("由于 Mod 文件被解压了，导致游戏无法继续运行。\n直接把整个 Mod 文件放进 Mod 文件夹中即可，若解压就会导致游戏出错。\n\n请删除 Mod 文件夹中已被解压的 Mod，然后再启动游戏。");
                        break;
                    case CrashReason.OutOfMemory:
                        results.Add("Minecraft 内存不足，导致其无法继续运行。\n这很可能是因为电脑内存不足、游戏分配的内存不足，或是配置要求过高。\n\n你可以尝试在 更多 → 百宝箱 中选择 内存优化，然后再启动游戏。\n如果还是不行，请在启动设置中增加为游戏分配的内存，并删除配置要求较高的材质、Mod、光影。\n如果依然不奏效，请在开始游戏前尽量关闭其他软件，或者……换台电脑？\\h");
                        break;
                    case CrashReason.UsingOpenJ9:
                        results.Add("游戏因为使用 OpenJ9 而崩溃了。\n请在启动设置的 Java 选择一项中改用非 OpenJ9 的 Java，然后再启动游戏。");
                        break;
                    case CrashReason.UsingJdk:
                        results.Add("游戏似乎因为使用 JDK，或 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用 JRE 8（Java 8），然后再启动游戏。\n如果你没有安装 JRE 8，你可以从网络中下载、安装一个。");
                        break;
                    case CrashReason.JavaTooHigh:
                        results.Add("游戏似乎因为你所使用的 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用较低版本的 Java，然后再启动游戏。\n如果没有，可以从网络中下载、安装一个。");
                        break;
                    case CrashReason.IncompatibleJava:
                        results.Add("游戏不兼容你当前使用的 Java。\n如果没有合适的 Java，可以从网络中下载、安装一个。");
                        break;
                    case CrashReason.SpecialCharsInModName:
                        results.Add("由于有 Mod 的名称包含特殊字符，导致游戏崩溃。\n请尝试修改 Mod 文件名，让它只包含英文字母、数字、减号、下划线等常见字符，然后再启动游戏。");
                        break;
                    case CrashReason.MissingMixinBootstrap:
                        results.Add("由于缺少 MixinBootstrap，导致游戏无法继续运行。\n请尝试重新安装对应版本的 Forge 或相关整合包。");
                        break;
                    case CrashReason.StackKeyword:
                        results.Add(additional.Count == 1
                            ? $"你的游戏遇到了一些问题，PCL 为此找到了一个可疑的关键词：{additional[0]}。\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\\h"
                            : $"你的游戏遇到了一些问题，PCL 为此找到了以下可疑的关键词：\n - {string.Join(", ", additional)}\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\\h");
                        break;
                    case CrashReason.StackModName:
                    case CrashReason.SuspectedModCrash:
                        results.Add(additional.Count == 1
                            ? $"PCL 怀疑名为 {additional[0]} 的 Mod 导致了游戏出错，但不能完全确定。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\\e\\h"
                            : $"PCL 怀疑以下 Mod 导致了游戏出错，但不能完全确定：\n - {string.Join("\n - ", additional)}\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\\e\\h");
                        break;
                    case CrashReason.ConfirmedModCrash:
                        results.Add(additional.Count == 1
                            ? $"名为 {additional[0]} 的 Mod 导致了游戏出错。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\\e\\h"
                            : $"以下 Mod 导致了游戏出错：\n - {string.Join("\n - ", additional)}\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\\e\\h");
                        break;
                    case CrashReason.ModMixinFailure:
                        if (additional.Count == 0)
                        {
                            results.Add("部分 Mod 注入失败，导致游戏出错。\n这一般代表着部分 Mod 与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试逐步禁用 Mod，然后观察游戏是否还会崩溃，以此定位导致崩溃的 Mod。\n\\e\\h");
                        }
                        else if (additional.Count == 1)
                        {
                            results.Add($"名为 {additional[0]} 的 Mod 注入失败，导致游戏出错。\n这一般代表着它与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\\e\\h");
                        }
                        else
                        {
                            results.Add($"以下 Mod 导致了游戏出错：\n - {string.Join("\n - ", additional)}\n这一般代表着它们与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\\e\\h");
                        }

                        break;
                    case CrashReason.ModConfigCrash:
                        results.Add(additional.Count >= 2
                            ? $"名为 {additional[0]} 的 Mod 导致了游戏出错：\n其配置文件 {additional[1]} 存在异常，无法读取。"
                            : $"名为 {additional.FirstOrDefault() ?? "未知"} 的 Mod 导致了游戏出错。\n\\e\\h");
                        break;
                    case CrashReason.ModInitializationFailure:
                        results.Add(additional.Count == 1
                            ? $"名为 {additional[0]} 的 Mod 初始化失败，导致游戏无法继续加载。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\\e\\h"
                            : $"以下 Mod 初始化失败，导致游戏出错：\n - {string.Join("\n - ", additional)}\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\\e\\h");
                        break;
                    case CrashReason.ProblematicBlock:
                        results.Add(additional.Count == 1
                            ? $"游戏似乎因为方块 {additional[0]} 出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是该方块导致出错，你或许需要使用一些方式删除此方块。\n - 若仍然出错，问题就可能来自其他原因……\\h"
                            : "游戏似乎因为世界中的某些方块出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是某些方块导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\\h");
                        break;
                    case CrashReason.DuplicateMods:
                        results.Add(additional.Count >= 2
                            ? $"你重复安装了多个相同的 Mod：\n - {string.Join("\n - ", additional)}\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。"
                            : "你可能重复安装了多个相同的 Mod，导致游戏出错。\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。\\e\\h");
                        break;
                    case CrashReason.ProblematicEntity:
                        results.Add(additional.Count == 1
                            ? $"游戏似乎因为实体 {additional[0]} 出现了问题。\n\n你可以创建一个新世界，并生成一个该实体，然后观察游戏的运行情况：\n - 若正常运行，则是该实体导致出错，你或许需要使用一些方式删除此实体。\n - 若仍然出错，问题就可能来自其他原因……\\h"
                            : "游戏似乎因为世界中的某些实体出现了问题。\n\n你可以创建一个新世界，并生成各种实体，观察游戏的运行情况：\n - 若正常运行，则是某些实体导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\\h");
                        break;
                    case CrashReason.OptiFineForgeIncompatible:
                        results.Add("由于 OptiFine 与当前版本的 Forge 不兼容，导致了游戏崩溃。\n\n请前往 OptiFine 官网（https://optifine.net/downloads）查看 OptiFine 所兼容的 Forge 版本，并严格按照对应版本重新安装游戏。");
                        break;
                    case CrashReason.ShadersModWithOptiFine:
                        results.Add("无需同时安装 OptiFine 和 Shaders Mod，OptiFine 已经集成了 Shaders Mod 的功能。\n在删除 Shaders Mod 后，游戏即可正常运行。");
                        break;
                    case CrashReason.LegacyForgeHighJavaIncompatible:
                        results.Add("由于低版本 Forge 与当前 Java 不兼容，导致了游戏崩溃。\n\n请尝试以下解决方案：\n - 更新 Forge 到 36.2.26 或更高版本\n - 换用版本低于 1.8.0.320 的 Java");
                        break;
                    case CrashReason.MultipleForgeInJson:
                        results.Add("可能由于其他启动器修改了 Forge 版本，当前实例的文件存在异常，导致了游戏崩溃。\n请尝试重新全新安装 Forge，而非使用其他启动器修改 Forge 版本。");
                        break;
                    case CrashReason.ManualDebugCrash:
                        results.Add("* 事实上，你的游戏没有任何问题，这是你自己触发的崩溃。\n* 你难道没有更重要的事要做吗？");
                        break;
                    case CrashReason.ModNeedsJava11:
                        results.Add("你所安装的部分 Mod 似乎需要使用 Java 11 启动。\n请在启动设置的 Java 选择一项中改用 Java 11，然后再启动游戏。\n如果你没有安装 Java 11，你可以从网络中下载、安装一个。");
                        break;
                    case CrashReason.VeryShortOutput:
                        results.Add($"程序返回了以下信息：\n{additional.FirstOrDefault() ?? string.Empty}\n\\h");
                        break;
                    case CrashReason.OptiFineWorldLoadFailure:
                        results.Add("你所使用的 OptiFine 可能导致了你的游戏出现问题。\n\n该问题只在特定 OptiFine 版本中出现，你可以尝试更换 OptiFine 的版本。\\h");
                        break;
                    case CrashReason.PixelFormatUnsupported:
                    case CrashReason.IntelDriverAccessViolation:
                    case CrashReason.AmdDriverAccessViolation:
                    case CrashReason.NvidiaDriverAccessViolation:
                    case CrashReason.UnsupportedOpenGl:
                        results.Add(_logAll.Contains("hd graphics ", StringComparison.OrdinalIgnoreCase)
                            ? "你的显卡驱动存在问题，或未使用独立显卡，导致游戏无法正常运行。\n\n如果你的电脑存在独立显卡，请使用独立显卡而非 Intel 核显启动 PCL 与 Minecraft。\n如果问题依然存在，请尝试升级你的显卡驱动到最新版本，或回退到出厂版本。\n如果还是不行，还可以尝试使用 8.0.51 或更低版本的 Java。\\h"
                            : "你的显卡驱动存在问题，导致游戏无法正常运行。\n\n请尝试升级你的显卡驱动到最新版本，或回退到出厂版本，然后再启动游戏。\n如果还是不行，可以尝试使用 8.0.51 或更低版本的 Java。\n如果问题依然存在，那么你可能需要换个更好的显卡……\\h");
                        break;
                    case CrashReason.TooLargeResourcePackOrWeakGpu:
                        results.Add("你所使用的材质分辨率过高，或显卡配置不足，导致游戏无法继续运行。\n\n如果你正在使用高清材质，请将它移除。\n如果你没有使用材质，那么你可能需要更新显卡驱动，或者换个更好的显卡……\\h");
                        break;
                    case CrashReason.NightConfigBug:
                        results.Add("由于 Night Config 存在问题，导致了游戏崩溃。\n你可以尝试安装 Night Config Fixes 模组，这或许能解决此问题。\\h");
                        break;
                    case CrashReason.OpenGl1282FromShadersOrResources:
                        results.Add("你所使用的光影或材质导致游戏出现了一些问题……\n\n请尝试删除你所添加的这些额外资源。\\h");
                        break;
                    case CrashReason.ExceededIdLimit:
                        results.Add("你所安装的 Mod 过多，超出了游戏的 ID 限制，导致了游戏崩溃。\n请尝试安装 JEID 等修复 Mod，或删除部分大型 Mod。");
                        break;
                    case CrashReason.FileValidationFailed:
                        results.Add("部分文件或内容校验失败，导致游戏出现了问题。\n\n请尝试删除游戏（包括 Mod）并重新下载，或尝试在重新下载时使用 VPN。\\h");
                        break;
                    case CrashReason.IncompleteForgeInstall:
                        results.Add("由于安装的 Forge 文件丢失，导致游戏无法正常运行。\n请前往实例设置重置该实例，然后再启动游戏。\n在打包游戏时删除 libraries 文件夹可能导致此错误。\\h");
                        break;
                    case CrashReason.FabricError:
                        results.Add(additional.Count == 1
                            ? $"Fabric 提供了以下错误信息：\n{additional[0]}\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。"
                            : "Fabric 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\\h");
                        break;
                    case CrashReason.IncompatibleMods:
                        if (additional.Count == 1)
                        {
                            var info = additional[0];
                            results.Add(RegexPatterns.IncompatibleModLoaderErrorHint.IsMatch(info)
                                ? loaderIncompatiblePrefix + info
                                : $"你所安装的 Mod 不兼容：\n{info}\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                        }
                        else
                        {
                            results.Add("你所安装的 Mod 不兼容，Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\\h");
                        }

                        break;
                    case CrashReason.ModLoaderFailure:
                        results.Add(additional.Count == 1
                            ? $"Mod 加载器提供了以下错误信息：\n{additional[0]}\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。"
                            : "Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\\h");
                        break;
                    case CrashReason.FabricSolution:
                        results.Add(additional.Count == 1
                            ? $"Fabric 提供了以下解决方案：\n{additional[0]}\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。"
                            : "Fabric 可能已经提供了解决方案，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\\h");
                        break;
                    case CrashReason.ForgeError:
                        results.Add(additional.Count == 1
                            ? $"Forge 提供了以下错误信息：\n{additional[0]}\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。"
                            : "Forge 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\\h");
                        break;
                    case CrashReason.NoAnalysisFiles:
                        results.Add("你的游戏出现了一些问题，但 PCL 未能找到相关记录文件，因此无法进行分析。\\h");
                        break;
                    case CrashReason.MissingDependencyOrWrongMcVersion:
                        results.Add(additional.Count > 0
                            ? $"Mod 缺少前置，或你安装的 Mod 与当前 Minecraft 版本不匹配：\n - {string.Join("\n - ", additional)}\n\n请根据上述信息检查依赖关系、Mod 版本与游戏版本。"
                            : "部分 Mod 缺少前置，或与当前 Minecraft 版本不匹配。\n请根据错误报告中的日志信息检查依赖关系、Mod 版本与游戏版本。");
                        break;
                    default:
                        results.Add($"PCL 获取到了没有详细信息的错误原因（{reason}），请向 PCL 作者提交反馈以获取详情。\\h");
                        break;
                }
            }

            var combined = string.Join("\n\n此外，", results)
                .Replace("\n", "\r\n", StringComparison.Ordinal)
                .Replace("\\h", string.Empty, StringComparison.Ordinal)
                .Replace("\\e", "\r\n你可以查看错误报告了解错误具体是如何发生的。", StringComparison.Ordinal)
                .Trim('\r', '\n');

            if (!results.Any(result => result.EndsWith("\\h", StringComparison.Ordinal)))
            {
                combined += "\r\n如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。";
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
