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
    public static MinecraftCrashAnalysisResult Analyze(MinecraftCrashAnalysisRequest request, Func<string, string>? localize = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourceFilePaths);

        var analyzer = new Analyzer(request, localize);
        analyzer.Collect();
        analyzer.Prepare();
        analyzer.Analyze();
        return analyzer.BuildResult();
    }

    private sealed partial class Analyzer
    {
        private readonly MinecraftCrashAnalysisRequest _request;
        private readonly Func<string, string>? _localize;
        private readonly List<AnalyzedFile> _rawFiles = [];
        private readonly Dictionary<CrashReason, List<string>> _reasons = [];

        private string? _logMc;
        private string? _logMcDebug;
        private string? _logHs;
        private string? _logCrash;
        private string _logAll = string.Empty;
        private string? _directFilePath;


        public Analyzer(MinecraftCrashAnalysisRequest request, Func<string, string>? localize)
        {
            _request = request;
            _localize = localize;
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

    }
}
