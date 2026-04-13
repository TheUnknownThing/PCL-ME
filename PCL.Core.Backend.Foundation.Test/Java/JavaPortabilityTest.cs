using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Runtime;
using PCL.Core.Minecraft.Java.Scanner;
using PCL.Core.Utils;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.Test;

[TestClass]
public class JavaPortabilityTest
{
    [TestMethod]
    public void CommandJavaParser_ParsesPortableMetadata()
    {
        var root = CreateTempDirectory();
        try
        {
            var binDir = Path.Combine(root, "jdk", "bin");
            Directory.CreateDirectory(binDir);
            var javaPath = Path.Combine(binDir, "java");
            var javacPath = Path.Combine(binDir, "javac");
            File.WriteAllText(javaPath, string.Empty);
            File.WriteAllText(javacPath, string.Empty);

            var parser = new CommandJavaParser(
                new FakeJavaRuntimeEnvironment(isWindows: false, executableDirectory: root),
                new FakeCommandRunner(new CommandResult(
                    0,
                    string.Empty,
                    """
                    Property settings:
                        java.version = 21.0.2
                        java.vendor = Eclipse Adoptium
                        os.arch = aarch64
                    """)));

            var installation = parser.Parse(javaPath);

            Assert.IsNotNull(installation);
            Assert.AreEqual(new Version(21, 0, 2), installation.Version);
            Assert.AreEqual(JavaBrandType.EclipseTemurin, installation.Brand);
            Assert.AreEqual("java", installation.JavaExecutableName);
            Assert.AreEqual("javac", installation.CompilerExecutableName);
            Assert.AreEqual(MachineType.ARM64, installation.Architecture);
            Assert.IsTrue(installation.Is64Bit);
            Assert.IsFalse(installation.IsJre);
            Assert.IsNull(installation.JavawExePath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void PathEnvironmentScanner_UsesRuntimeSeparatorAndExecutableNames()
    {
        var root = "pcl-java-path-test-" + Guid.NewGuid().ToString("N");
        try
        {
            var pathA = Path.Combine(root, "path-a");
            var pathB = Path.Combine(root, "path-b");
            Directory.CreateDirectory(pathA);
            Directory.CreateDirectory(pathB);
            var javaPath = Path.Combine(pathB, "java");
            File.WriteAllText(javaPath, string.Empty);

            var runtime = new FakeJavaRuntimeEnvironment(
                isWindows: false,
                executableDirectory: root,
                variables: new Dictionary<string, string>
                {
                    ["PATH"] = $"{pathA}:{pathB}"
                });
            var scanner = new PathEnvironmentScanner(runtime);
            var results = new List<string>();

            scanner.Scan(results);

            CollectionAssert.Contains(results, javaPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void WhereCommandScanner_UsesWhichOnNonWindows()
    {
        var root = CreateTempDirectory();
        try
        {
            var javaPath = Path.Combine(root, "java");
            File.WriteAllText(javaPath, string.Empty);
            var runner = new InspectingCommandRunner(new CommandResult(0, javaPath + Environment.NewLine, string.Empty));
            var runtime = new FakeJavaRuntimeEnvironment(isWindows: false, executableDirectory: root);
            var scanner = new WhereCommandScanner(runtime, runner);
            var results = new List<string>();

            scanner.Scan(results);

            Assert.AreEqual("which", runner.FileName);
            Assert.AreEqual("-a java", runner.Arguments);
            CollectionAssert.Contains(results, javaPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task JavaManager_UsesStorageAndEvaluator()
    {
        var root = CreateTempDirectory();
        var storedJavaPath = Path.Combine(root, "stored", "java");
        var scannedJavaPath = Path.Combine(root, "scan", "java");

        try
        {
        var storage = new FakeJavaStorage(
        [
            new JavaStorageItem
            {
                Path = storedJavaPath,
                IsEnable = false,
                Source = JavaSource.ManualAdded
            }
        ]);
        var parser = new FakeJavaParser(
            new JavaInstallation(Path.GetDirectoryName(storedJavaPath)!, new Version(17, 0, 1), JavaBrandType.OpenJDK, MachineType.AMD64, true, false, Path.GetFileName(storedJavaPath), null, "javac"),
            new JavaInstallation(Path.GetDirectoryName(scannedJavaPath)!, new Version(21, 0, 2), JavaBrandType.EclipseTemurin, MachineType.AMD64, true, false, Path.GetFileName(scannedJavaPath), null, "javac"));
        var manager = new JavaManager(
            parser,
            storage,
            new FixedEvaluator(true),
            new FixedScanner(scannedJavaPath));

        manager.ReadConfig();
        await manager.ScanJavaAsync(force: true);

        Assert.AreEqual(2, manager.GetSortedJavaList().Count);
        Assert.IsNotNull(storage.SavedItems);
        Assert.AreEqual(2, storage.SavedItems.Length);
        Assert.IsTrue(manager.Exist(scannedJavaPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-java-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeJavaRuntimeEnvironment : IJavaRuntimeEnvironment
    {
        private readonly IReadOnlyDictionary<string, string>? _variables;

        public FakeJavaRuntimeEnvironment(bool isWindows, string executableDirectory, IReadOnlyDictionary<string, string>? variables = null)
        {
            IsWindows = isWindows;
            ExecutableDirectory = executableDirectory;
            _variables = variables;
        }

        public string ExecutableDirectory { get; }
        public bool IsWindows { get; }
        public bool IsLinux => !IsWindows;
        public bool IsMacOS => false;
        public bool Is64BitOperatingSystem => true;
        public char PathListSeparator => IsWindows ? ';' : ':';
        public string JavaCommandName => IsWindows ? "java.exe" : "java";
        public string CommandLookupToolName => IsWindows ? "where" : "which";
        public string JavacExecutableName => IsWindows ? "javac.exe" : "javac";
        public string? JavaWindowExecutableName => IsWindows ? "javaw.exe" : null;
        public IReadOnlyList<string> JavaExecutableNames => IsWindows ? ["java.exe", "java"] : ["java"];

        public string GetFolderPath(Special folder) => ExecutableDirectory;

        public string? GetEnvironmentVariable(string key)
        {
            if (_variables == null) return null;
            return _variables.TryGetValue(key, out var value) ? value : null;
        }

        public IEnumerable<string> GetFixedDriveRoots() => [];
    }

    private sealed class FakeCommandRunner(CommandResult result) : ICommandRunner
    {
        public CommandResult Run(string fileName, string arguments) => result;
    }

    private sealed class InspectingCommandRunner(CommandResult result) : ICommandRunner
    {
        public string? FileName { get; private set; }
        public string? Arguments { get; private set; }

        public CommandResult Run(string fileName, string arguments)
        {
            FileName = fileName;
            Arguments = arguments;
            return result;
        }
    }

    private sealed class FakeJavaStorage(params JavaStorageItem[] items) : IJavaStorage
    {
        public JavaStorageItem[] SavedItems { get; private set; } = [];

        public JavaStorageItem[] Load() => items;

        public void Save(JavaStorageItem[] itemsToSave)
        {
            SavedItems = itemsToSave;
        }
    }

    private sealed class FixedEvaluator(bool shouldEnable) : IJavaInstallationEvaluator
    {
        public bool ShouldEnableByDefault(JavaInstallation installation) => shouldEnable;
    }

    private sealed class FixedScanner(params string[] paths) : IJavaScanner
    {
        public void Scan(ICollection<string> results)
        {
            foreach (var path in paths) results.Add(path);
        }
    }

    private sealed class FakeJavaParser(JavaInstallation storedInstallation, JavaInstallation scannedInstallation) : IJavaParser
    {
        public JavaInstallation? Parse(string javaExePath)
        {
            return javaExePath switch
            {
                var path when string.Equals(path, storedInstallation.JavaExePath, StringComparison.OrdinalIgnoreCase) => storedInstallation,
                var path when string.Equals(path, scannedInstallation.JavaExePath, StringComparison.OrdinalIgnoreCase) => scannedInstallation,
                _ => null
            };
        }
    }
}
