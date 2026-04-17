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

    }
}
