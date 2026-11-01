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
                or "rawoutput.log")
            {
                _directFilePath ??= file.Path;
                return AnalyzeFileType.MinecraftLog;
            }

            if (PathsEqual(file.Path, _request.CurrentLauncherLogFilePath) ||
                matchName is "pcl launcher log.txt"
                    or "launcher log.txt"
                    or "pcl.log")
            {
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
                         "PCL.log"
                     })
            {
                if (!filesByName.TryGetValue(fileName, out var currentLog))
                {
                    continue;
                }

                logMcBuilder.Append(GetHeadTailLines(currentLog.Lines, 0, 500));

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

    }
}
