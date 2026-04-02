using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.Java.Scanner;

public class DefaultPathsScanner(IJavaRuntimeEnvironment? runtime = null) : IJavaScanner
{
    private const int MaxSearchDepth = 8;
    private readonly IJavaRuntimeEnvironment _runtime = runtime ?? SystemJavaRuntimeEnvironment.Current;

    public void Scan(ICollection<string> results)
    {
        try
        {
            var searchRoots = _GetSearchRoots();
            LogWrapper.Info($"[Java] 对下列目录进行广度关键词搜索:{Environment.NewLine}{string.Join(Environment.NewLine, searchRoots)}");

            foreach (var root in searchRoots)
            {
                _BfsSearch(root, results);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "默认路径扫描失败");
        }
    }

    private HashSet<string> _GetSearchRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            _runtime.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            _runtime.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            _runtime.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Path.Combine(_runtime.ExecutableDirectory, "PCL")
        };

        if (_runtime.IsWindows)
        {
            var keyFolders = new[] { "Program Files", "Program Files (x86)" };
            foreach (var drive in _runtime.GetFixedDriveRoots())
            {
                foreach (var folder in keyFolders)
                {
                    roots.Add(Path.Combine(drive, folder));
                }

                // 根目录关键词搜索
                try
                {
                    var rootDirs = Directory.EnumerateDirectories(drive)
                        .Where(dir => JavaConsts.MostPossibleKeywords.Any(k =>
                            Path.GetFileName(dir).Contains(k, StringComparison.OrdinalIgnoreCase)));

                    foreach (var dir in rootDirs)
                        roots.Add(dir);
                }
                catch (UnauthorizedAccessException) { /* 忽略无权限目录 */ }
                catch (IOException) { /* 忽略IO错误 */ }
            }
        }
        else if (_runtime.IsMacOS)
        {
            roots.Add("/Library/Java/JavaVirtualMachines");
            roots.Add(Path.Combine(_runtime.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Java", "JavaVirtualMachines"));
            roots.Add("/opt");
        }
        else
        {
            roots.Add("/usr/lib/jvm");
            roots.Add("/usr/java");
            roots.Add("/opt");
        }

        return roots;
    }

    private void _BfsSearch(string rootPath, ICollection<string> results)
    {
        if (!Directory.Exists(rootPath)) return;

        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootPath, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > MaxSearchDepth || !Directory.Exists(current)) continue;

            try
            {
                // 深度0时只遍历含关键词的目录
                var dirsToScan = depth == 0
                    ? Directory.EnumerateDirectories(current)
                        .Where(dir => _ShouldScanDirectory(dir))
                    : Directory.EnumerateDirectories(current);

                foreach (var dir in dirsToScan)
                {
                    var javaExe = _runtime.JavaExecutableNames
                        .Select(executableName => Path.Combine(dir, executableName))
                        .FirstOrDefault(File.Exists);
                    if (javaExe != null)
                    {
                        results.Add(javaExe);
                    }
                    else
                    {
                        queue.Enqueue((dir, depth + 1));
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                LogWrapper.Debug($"跳过目录 {current}: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Java", $"搜索目录 {current} 时出错");
            }
        }
    }

    private static bool _ShouldScanDirectory(string path)
    {
        var name = Path.GetFileName(path);
        if (JavaConsts.ExcludeFolderNames.Any(ex => name.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            return false;

        return JavaConsts.AllKeyworkds.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
