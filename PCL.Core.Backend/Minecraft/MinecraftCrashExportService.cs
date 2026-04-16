using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.Minecraft;

public static class MinecraftCrashExportService
{
    private const string LauncherLogReportName = "PCL Launcher Log.txt";
    private const string LaunchScriptReportName = "Launch Script.bat";
    private const string RawOutputReportName = "Pre-Crash Output.txt";
    private const string EnvironmentReportName = "Environment and Launch Info.txt";

    public static MinecraftCrashExportResult PrepareReportDirectory(MinecraftCrashExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Environment);

        ResetDirectory(request.ReportDirectory);

        var writtenFiles = new List<string>();
        foreach (var sourceFile in request.SourceFiles)
        {
            if (sourceFile is null || string.IsNullOrWhiteSpace(sourceFile.SourcePath) || !File.Exists(sourceFile.SourcePath))
            {
                continue;
            }

            var outputName = GetOutputFileName(sourceFile.SourcePath, request.CurrentLauncherLogFilePath);
            var outputPath = Path.Combine(request.ReportDirectory, outputName);
            var bytes = File.ReadAllBytes(sourceFile.SourcePath);
            var encoding = GetEncoding(outputName, bytes);
            var content = encoding.GetString(bytes);
            content = SanitizeContent(
                content,
                outputName == LaunchScriptReportName ? 'F' : '*',
                request.CurrentAccessToken,
                request.CurrentUserUuid,
                request.UserProfilePath);
            File.WriteAllText(outputPath, content, encoding);
            writtenFiles.Add(outputPath);
        }

        var launcherLogContent = ReadReportFile(request.ReportDirectory, LauncherLogReportName);
        var launchScriptContent = ReadReportFile(request.ReportDirectory, LaunchScriptReportName);
        var environmentReportPath = Path.Combine(request.ReportDirectory, EnvironmentReportName);
        var environmentReport = MinecraftCrashReportBuilder.BuildEnvironmentReport(
            new MinecraftCrashEnvironmentReportRequest(
                request.LauncherVersionName,
                request.UniqueAddress,
                launcherLogContent,
                launchScriptContent,
                request.Environment));
        File.WriteAllText(environmentReportPath, environmentReport, new UTF8Encoding(false));
        writtenFiles.Add(environmentReportPath);

        return new MinecraftCrashExportResult(request.ReportDirectory, writtenFiles);
    }

    private static void ResetDirectory(string reportDirectory)
    {
        if (Directory.Exists(reportDirectory))
        {
            Directory.Delete(reportDirectory, true);
        }

        Directory.CreateDirectory(reportDirectory);
    }

    private static string GetOutputFileName(string sourcePath, string? currentLauncherLogFilePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        if (PathsEqual(sourcePath, currentLauncherLogFilePath))
        {
            return LauncherLogReportName;
        }

        return fileName switch
        {
            "LatestLaunch.bat" => LaunchScriptReportName,
            "RawOutput.log" => RawOutputReportName,
            _ => fileName
        };
    }

    private static Encoding GetEncoding(string outputName, byte[] bytes)
    {
        if (outputName is RawOutputReportName or LauncherLogReportName)
        {
            return Encoding.UTF8;
        }

        return EncodingDetector.DetectEncoding(bytes);
    }

    private static string ReadReportFile(string reportDirectory, string fileName)
    {
        var path = Path.Combine(reportDirectory, fileName);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        var bytes = File.ReadAllBytes(path);
        return GetEncoding(fileName, bytes).GetString(bytes);
    }

    private static string SanitizeContent(
        string raw,
        char filterChar,
        string? currentAccessToken,
        string? currentUserUuid,
        string? userProfilePath)
    {
        if (raw.Contains("accessToken ", StringComparison.Ordinal))
        {
            raw = Regex.Replace(
                raw,
                "(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})",
                match => new string(filterChar, match.Length));
        }

        if (!string.IsNullOrEmpty(currentAccessToken) &&
            currentAccessToken.Length >= 10 &&
            !string.Equals(currentAccessToken, currentUserUuid, StringComparison.Ordinal) &&
            raw.Contains(currentAccessToken, StringComparison.Ordinal))
        {
            raw = raw.Replace(
                currentAccessToken,
                currentAccessToken[..5] + new string(filterChar, currentAccessToken.Length - 10) + currentAccessToken[^5..],
                StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(userProfilePath))
        {
            var userName = userProfilePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\', '/')
                .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault();
            if (!string.IsNullOrEmpty(userName))
            {
                var maskedProfile = userProfilePath.Replace(userName, new string(filterChar, userName.Length), StringComparison.Ordinal);
                raw = raw.Replace(userProfilePath, maskedProfile, StringComparison.Ordinal);
            }
        }

        return raw;
    }

    private static bool PathsEqual(string sourcePath, string? currentLauncherLogFilePath)
    {
        if (string.IsNullOrEmpty(currentLauncherLogFilePath))
        {
            return false;
        }

        var left = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var right = Path.GetFullPath(currentLauncherLogFilePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
