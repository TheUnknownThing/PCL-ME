using System.Collections.Generic;
using System.Runtime.InteropServices;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.OS;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class AvaloniaHostInputFactory
{
    public static StartupAvaloniaInputs CreateStartupInputs()
    {
        var executableDirectory = EnsureTrailingSeparator(Directory.GetCurrentDirectory());
        var tempDirectory = EnsureTrailingSeparator(Path.Combine(Path.GetTempPath(), "PCL", "Temp"));
        var appDataDirectory = EnsureTrailingSeparator(GetLauncherAppDataDirectory());

        return new StartupAvaloniaInputs(
            new LauncherStartupWorkflowRequest(
                CommandLineArguments: ["--memory"],
                ExecutableDirectory: executableDirectory,
                TempDirectory: tempDirectory,
                AppDataDirectory: appDataDirectory,
                IsBetaVersion: false,
                DetectedWindowsVersion: GetHostVersionForStartupChecks(),
                Is64BitOperatingSystem: Environment.Is64BitOperatingSystem,
                ShowStartupLogo: true),
            new LauncherStartupConsentRequest(
                LauncherStartupSpecialBuildKind.None,
                IsSpecialBuildHintDisabled: false,
                HasAcceptedEula: false,
                IsTelemetryDefault: true));
    }

    public static ShellAvaloniaInputs CreateShellInputs()
    {
        return new ShellAvaloniaInputs(
            CreateStartupInputs(),
            new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                HasRunningTasks: true,
                HasGameLogs: true));
    }

    public static LaunchAvaloniaInputs CreateLaunchInputs(string scenario)
    {
        var sample = AvaloniaSampleFactory.CreateDefaultLaunchInputs(scenario);
        var homeDirectory = GetUserHomeDirectory();
        var minecraftRoot = GetMinecraftRootDirectory(homeDirectory);
        var librariesDirectory = Path.Combine(minecraftRoot, "libraries");
        var assetsRoot = Path.Combine(minecraftRoot, "assets");
        var instanceDirectory = Path.Combine(minecraftRoot, "versions", "demo");
        var nativesDirectory = Path.Combine(instanceDirectory, "demo-natives");
        var javaHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java", "bin")
            : "/usr/bin";
        var javaExecutablePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(javaHome, "java.exe")
            : Path.Combine(javaHome, "java");
        var javawExecutablePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(javaHome, "javaw.exe")
            : javaExecutablePath;
        var pathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
        var libraryPaths = new[]
        {
            Path.Combine(librariesDirectory, "override.jar"),
            Path.Combine(librariesDirectory, "cleanroom.jar"),
            Path.Combine(librariesDirectory, "retrowrapper", "RetroWrapper.jar"),
            Path.Combine(librariesDirectory, "optifine.jar"),
            Path.Combine(librariesDirectory, "cleanroom.jar"),
            Path.Combine(librariesDirectory, "core.jar")
        };

        return sample with
        {
            JavaRuntimeInputs = BuildJavaRuntimeInputs(sample.JavaRuntimeInputs, scenario, minecraftRoot),
            ClasspathRequest = sample.ClasspathRequest with
            {
                Libraries =
                [
                    new MinecraftLaunchClasspathLibrary("com.cleanroommc:cleanroom:0.2.4-alpha", Path.Combine(librariesDirectory, "cleanroom.jar"), false),
                    new MinecraftLaunchClasspathLibrary("com.example:core", Path.Combine(librariesDirectory, "core.jar"), false),
                    new MinecraftLaunchClasspathLibrary("optifine:OptiFine", Path.Combine(librariesDirectory, "optifine.jar"), false)
                ],
                CustomHeadEntries = [Path.Combine(librariesDirectory, "override.jar")],
                RetroWrapperPath = Path.Combine(librariesDirectory, "retrowrapper", "RetroWrapper.jar"),
                ClasspathSeparator = pathSeparator
            },
            NativesDirectoryRequest = sample.NativesDirectoryRequest with
            {
                PreferredInstanceDirectory = nativesDirectory,
                AppDataNativesDirectory = Path.Combine(minecraftRoot, "bin", "natives"),
                FinalFallbackDirectory = Path.Combine(Path.GetTempPath(), "PCL", "natives")
            },
            ReplacementValueRequest = sample.ReplacementValueRequest with
            {
                ClasspathSeparator = pathSeparator,
                NativesDirectory = nativesDirectory,
                LibraryDirectory = librariesDirectory,
                LibrariesDirectory = librariesDirectory,
                GameDirectory = minecraftRoot,
                AssetsRoot = assetsRoot,
                GameAssetsDirectory = Path.Combine(assetsRoot, "virtual", "legacy"),
                Classpath = string.Join(pathSeparator, libraryPaths)
            },
            ArgumentPlanRequest = sample.ArgumentPlanRequest with
            {
                ReplacementValues = new Dictionary<string, string>(sample.ArgumentPlanRequest.ReplacementValues)
                {
                    ["${game_directory}"] = minecraftRoot
                }
            },
            PrerunWorkflowRequest = sample.PrerunWorkflowRequest with
            {
                LauncherProfilesPath = Path.Combine(minecraftRoot, "launcher_profiles.json"),
                PrimaryOptionsFilePath = Path.Combine(minecraftRoot, "options.txt"),
                YosbrOptionsFilePath = Path.Combine(minecraftRoot, "config", "yosbr", "options.txt")
            },
            SessionStartWorkflowRequest = sample.SessionStartWorkflowRequest with
            {
                CustomCommandWorkflow = sample.SessionStartWorkflowRequest.CustomCommandWorkflow with
                {
                    CommandRequest = sample.SessionStartWorkflowRequest.CustomCommandWorkflow.CommandRequest with
                    {
                        WorkingDirectory = minecraftRoot,
                        JavaExecutablePath = javaExecutablePath
                    },
                    ShellWorkingDirectory = minecraftRoot
                },
                ProcessRequest = sample.SessionStartWorkflowRequest.ProcessRequest with
                {
                    JavaExecutablePath = javaExecutablePath,
                    JavawExecutablePath = javawExecutablePath,
                    JavaFolder = javaHome,
                    CurrentPathEnvironmentValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
                    AppDataPath = minecraftRoot,
                    WorkingDirectory = minecraftRoot
                },
                WatcherWorkflowRequest = sample.SessionStartWorkflowRequest.WatcherWorkflowRequest with
                {
                    SessionLogRequest = sample.SessionStartWorkflowRequest.WatcherWorkflowRequest.SessionLogRequest with
                    {
                        MinecraftFolder = minecraftRoot,
                        InstanceFolder = instanceDirectory,
                        NativesFolder = nativesDirectory
                    },
                    WatcherRequest = sample.SessionStartWorkflowRequest.WatcherWorkflowRequest.WatcherRequest with
                    {
                        JavaFolder = javaHome
                    }
                }
            }
        };
    }

    public static CrashAvaloniaInputs CreateCrashInputs()
    {
        var sample = AvaloniaSampleFactory.CreateDefaultCrashInputs();
        var homeDirectory = GetUserHomeDirectory();
        var minecraftRoot = GetMinecraftRootDirectory(homeDirectory);
        var logDirectory = Path.Combine(minecraftRoot, "logs");
        var launcherAppDataDirectory = GetLauncherAppDataDirectory();
        var launcherLogDirectory = Path.Combine(launcherAppDataDirectory, "Log");
        var crashReportDirectory = Path.Combine(minecraftRoot, "crash-reports");
        var primaryFiles = new List<string>
        {
            Path.Combine(launcherAppDataDirectory, $"LatestLaunch{GetCommandScriptExtension()}"),
            Path.Combine(logDirectory, "RawOutput.log"),
            Path.Combine(logDirectory, "latest.log"),
            Path.Combine(logDirectory, "debug.log")
        };
        var additionalFiles = new List<string>();

        if (Directory.Exists(crashReportDirectory))
        {
            additionalFiles.AddRange(Directory.EnumerateFiles(crashReportDirectory));
        }

        if (Directory.Exists(minecraftRoot))
        {
            additionalFiles.AddRange(Directory.EnumerateFiles(minecraftRoot, "*.log"));
        }

        return sample with
        {
            ExportPlanRequest = sample.ExportPlanRequest with
            {
                ReportDirectory = Path.Combine(Path.GetTempPath(), "PCL", "CrashReport", DateTime.Now.ToString("yyyy-MM-dd")),
                SourceFilePaths = primaryFiles,
                AdditionalSourceFilePaths = additionalFiles,
                CurrentLauncherLogFilePath = Path.Combine(launcherLogDirectory, "PCL.log"),
                Environment = GetHostEnvironmentSnapshot(),
                UserProfilePath = homeDirectory
            }
        };
    }

    private static string GetUserHomeDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string GetLauncherAppDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL");
        }

        return Path.Combine(GetUserHomeDirectory(), ".config", "PCL");
    }

    private static string GetMinecraftRootDirectory(string homeDirectory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }

        return Path.Combine(homeDirectory, ".minecraft");
    }

    private static string GetCommandScriptExtension()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ".bat"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ".command"
                : ".sh";
    }

    private static Version GetHostVersionForStartupChecks()
    {
        var version = Environment.OSVersion.Version;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return version;
        }

        return new Version(Math.Max(version.Major, 10), Math.Max(version.Minor, 0), Math.Max(version.Build, 17763));
    }

    private static SystemEnvironmentSnapshot GetHostEnvironmentSnapshot()
    {
        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var totalPhysicalMemoryBytes = availableMemory > 0
            ? (ulong)availableMemory
            : 8UL * 1024UL * 1024UL * 1024UL;

        return new SystemEnvironmentSnapshot(
            RuntimeInformation.OSDescription,
            Environment.OSVersion.Version,
            RuntimeInformation.OSArchitecture,
            Environment.Is64BitOperatingSystem,
            totalPhysicalMemoryBytes,
            GetCpuName(),
            []);
    }

    private static string GetCpuName()
    {
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
               Environment.GetEnvironmentVariable("HOSTTYPE") ??
               RuntimeInformation.ProcessArchitecture.ToString();
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static JavaRuntimeAvaloniaInputs BuildJavaRuntimeInputs(JavaRuntimeAvaloniaInputs sample, string scenario, string minecraftRoot)
    {
        var platformKey = ResolveJavaRuntimePlatformKey();
        var componentKey = scenario == "legacy-forge" ? "jre-8u412" : "jre-legacy";
        var runtimeBaseDirectory = MinecraftJavaRuntimeDownloadSessionService.GetRuntimeBaseDirectory(minecraftRoot, componentKey);
        var downloadWorkflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                sample.ManifestJson,
                runtimeBaseDirectory,
                sample.IgnoredSha1Hashes,
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));
        var existingRelativePaths = downloadWorkflowPlan.Files
            .Where(file => File.Exists(file.TargetPath))
            .Select(file => file.RelativePath)
            .ToArray();

        return sample with
        {
            PlatformKey = platformKey,
            RuntimeBaseDirectory = runtimeBaseDirectory,
            IndexJson = sample.IndexJson.Replace("windows-x64", platformKey, StringComparison.Ordinal),
            ExistingRelativePaths = existingRelativePaths
        };
    }

    private static string ResolveJavaRuntimePlatformKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "windows-arm64",
                Architecture.X86 => "windows-x86",
                _ => "windows-x64"
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "mac-os-arm64"
                : "mac-os";
        }

        return RuntimeInformation.ProcessArchitecture == Architecture.X86
            ? "linux-i386"
            : "linux";
    }
}
