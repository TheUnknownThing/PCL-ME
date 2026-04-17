using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia.Threading;
using PCL.Core.App.Essentials;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Runtime;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string SetupJavaLogModule = "SetupJava";

    private void RefreshJavaSurface()
    {
        _ = RefreshJavaSurfaceAsync();
    }

    private async Task RefreshJavaSurfaceAsync()
    {
        AddActivity(
            LT("setup.java.activities.refresh"),
            LT("setup.java.activities.refresh_scanning"));

        try
        {
            await FrontendJavaInventoryService.RefreshPortableJavaScanCacheAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReloadSetupComposition(initializeAllSurfaces: false);
                RefreshLaunchState();
                AddActivity(
                    LT("setup.java.activities.refresh"),
                    LT("setup.java.activities.refresh_completed"));
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReloadSetupComposition(initializeAllSurfaces: false);
                AddFailureActivity(LT("setup.java.activities.refresh_failed"), ex.Message);
            });
        }
    }

    private async Task AddJavaRuntimeAsync()
    {
        string? selectedPath;
        try
        {
            selectedPath = await _shellActionService.PickOpenFileAsync(
                LT("setup.java.activities.add_pick_title"),
                LT(OperatingSystem.IsWindows()
                    ? "setup.java.activities.add_pick_filter_windows"
                    : "setup.java.activities.add_pick_filter_unix"),
                OperatingSystem.IsWindows() ? "*.exe" : "java",
                OperatingSystem.IsWindows() ? "java.exe" : "java.exe",
                OperatingSystem.IsWindows() ? "javaw.exe" : "javaw");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.java.activities.add_failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            AddActivity(LT("setup.java.activities.add"), LT("setup.java.activities.add_cancelled"));
            return;
        }

        var installation = ParseJavaInstallation(selectedPath);
        if (installation is null)
        {
            AddFailureActivity(
                LT("setup.java.activities.add_failed"),
                LT("setup.java.activities.add_unrecognized", ("path", selectedPath)));
            return;
        }

        var javaPath = Path.GetFullPath(installation.JavaExePath);
        var items = LoadStoredJavaItems();
        if (items.Any(item => string.Equals(item.Path, javaPath, StringComparison.OrdinalIgnoreCase)))
        {
            AddActivity(LT("setup.java.activities.add"), LT("setup.java.activities.add_exists", ("path", javaPath)));
            SelectJavaRuntime(javaPath);
            ReloadSetupComposition();
            return;
        }

        items.Add(new JavaStorageItem
        {
            Path = javaPath,
            IsEnable = true,
            Source = JavaSource.ManualAdded
        });
        SaveStoredJavaItems(items);
        SelectJavaRuntime(javaPath);
        ReloadSetupComposition();
        AddActivity(LT("setup.java.activities.add"), LT("setup.java.activities.add_completed", ("path", javaPath)));
    }

    private JavaRuntimeEntryViewModel CreateJavaRuntimeEntry(
        string key,
        string title,
        string folder,
        IReadOnlyList<string> tags,
        bool isEnabled)
    {
        return new JavaRuntimeEntryViewModel(
            key,
            title,
            folder,
            tags,
            isEnabled,
            new ActionCommand(() => SelectJavaRuntime(key)),
            new ActionCommand(() => OpenJavaRuntimeFolder(title, folder)),
            new ActionCommand(() => OpenJavaRuntimeDetail(key, title, folder, tags)),
            new ActionCommand(() =>
            {
                LogWrapper.Info(SetupJavaLogModule, $"Toggle button clicked for Java runtime: key='{key}', title='{title}'.");
                ToggleJavaEnabled(key);
            }),
            _i18n.T("setup.java.actions.enable"),
            _i18n.T("setup.java.actions.disable"));
    }

    private void OpenJavaRuntimeFolder(string title, string folder)
    {
        OpenInstanceTarget(
            LT("setup.java.activities.open_folder", ("title", title)),
            folder,
            LT("setup.java.activities.folder_missing"));
    }

    private void OpenJavaRuntimeDetail(string key, string title, string folder, IReadOnlyList<string> tags)
        => _ = OpenJavaRuntimeDetailAsync(key, title, folder, tags);

    private async Task OpenJavaRuntimeDetailAsync(string key, string title, string folder, IReadOnlyList<string> tags)
    {
        if (string.IsNullOrWhiteSpace(key) || !File.Exists(key))
        {
            AddActivity(
                LT("setup.java.activities.view_detail", ("title", title)),
                LT("setup.java.activities.unavailable"));
            return;
        }

        var installation = ParseJavaInstallation(key);
        var sourceLabel = ResolveJavaSourceLabel(key);
        var detail = installation is null
            ? string.Join(Environment.NewLine,
            [
                LT("setup.java.details.path", ("value", key)),
                LT("setup.java.details.folder", ("value", folder)),
                LT("setup.java.details.source", ("value", sourceLabel)),
                LT("setup.java.details.tags", ("value", string.Join(" / ", tags))),
                LT("setup.java.details.default_java", ("value", _selectedJavaRuntimeKey == key ? LT("setup.java.details.yes") : LT("setup.java.details.no"))),
                string.Empty,
                LT("setup.java.details.metadata_unavailable")
            ])
            : string.Join(Environment.NewLine,
            [
                LT("setup.java.details.type", ("value", installation.IsJre ? "JRE" : "JDK")),
                LT("setup.java.details.version", ("value", installation.Version)),
                LT("setup.java.details.major_version", ("value", installation.MajorVersion)),
                LT("setup.java.details.architecture", ("value", $"{installation.Architecture} ({(installation.Is64Bit ? "64 Bit" : "32 Bit")})")),
                LT("setup.java.details.brand", ("value", installation.Brand)),
                LT("setup.java.details.source", ("value", sourceLabel)),
                LT("setup.java.details.default_java", ("value", _selectedJavaRuntimeKey == key ? LT("setup.java.details.yes") : LT("setup.java.details.no"))),
                LT("setup.java.details.enabled", ("value", JavaRuntimeEntries.FirstOrDefault(item => item.Key == key)?.IsEnabled == true ? LT("setup.java.details.yes") : LT("setup.java.details.no"))),
                LT("setup.java.details.availability", ("value", installation.IsStillAvailable ? LT("setup.java.details.available") : LT("setup.java.details.unavailable"))),
                LT("setup.java.details.executable", ("value", installation.JavaExePath)),
                LT("setup.java.details.folder", ("value", installation.JavaFolder))
            ]);
        var result = await ShowToolboxConfirmationAsync(LT("setup.java.activities.view_detail", ("title", title)), detail);
        if (result is null)
        {
            return;
        }

        AddActivity(
            LT("setup.java.activities.view_detail", ("title", title)),
            LT("setup.java.activities.detail_shown"));
    }

    private static JavaInstallation? ParseJavaInstallation(string javaExecutablePath)
    {
        var parsers = new List<IJavaParser>
        {
            new CommandJavaParser(SystemJavaRuntimeEnvironment.Current, new ProcessCommandRunner())
        };
        if (TryCreatePeHeaderParser() is { } peHeaderParser)
        {
            parsers.Add(peHeaderParser);
        }

        var parser = new CompositeJavaParser([.. parsers]);
        return parser.Parse(javaExecutablePath);
    }

    private static IJavaParser? TryCreatePeHeaderParser()
    {
        const string typeName = "PCL.Core.Minecraft.Java.Parser.PeHeaderParser";

        try
        {
            var parserType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(candidate => candidate.GetType(typeName, throwOnError: false))
                .FirstOrDefault(candidate => candidate is not null);

            if (parserType is null)
            {
                parserType = TryLoadAssembly("PCL.Core.Backend")?.GetType(typeName, throwOnError: false)
                             ?? TryLoadAssembly("PCL.Core.Foundation")?.GetType(typeName, throwOnError: false)
                             ?? TryLoadAssembly("PCL.Core")?.GetType(typeName, throwOnError: false);
            }

            if (parserType is null || !typeof(IJavaParser).IsAssignableFrom(parserType))
            {
                return null;
            }

            return Activator.CreateInstance(parserType) as IJavaParser;
        }
        catch
        {
            return null;
        }
    }

    private static Assembly? TryLoadAssembly(string assemblyName)
    {
        var candidateRoots = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        };
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in candidateRoots.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            foreach (var directory in EnumerateParentDirectories(root))
            {
                var assemblyFileName = $"{assemblyName}.dll";
                var directPath = Path.Combine(directory, assemblyFileName);
                if (seenPaths.Add(directPath) && File.Exists(directPath))
                {
                    try
                    {
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(directPath);
                    }
                    catch
                    {
                    }
                }

                var binDirectory = Path.Combine(directory, assemblyName, "bin");
                if (!Directory.Exists(binDirectory))
                {
                    continue;
                }

                foreach (var buildPath in Directory.EnumerateFiles(binDirectory, assemblyFileName, SearchOption.AllDirectories)
                             .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                             .Take(8))
                {
                    if (!seenPaths.Add(buildPath))
                    {
                        continue;
                    }

                    try
                    {
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(buildPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateParentDirectories(string startPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private string ResolveJavaSourceLabel(string key)
    {
        var item = LoadStoredJavaItems().FirstOrDefault(candidate =>
            string.Equals(candidate.Path, key, StringComparison.OrdinalIgnoreCase));
        return item?.Source switch
        {
            JavaSource.AutoInstalled => LT("setup.java.tags.auto_installed"),
            JavaSource.ManualAdded => LT("setup.java.tags.manual_added"),
            _ => LT("setup.java.tags.auto_scanned")
        };
    }

    private void SelectJavaRuntime(string key)
    {
        _selectedJavaRuntimeKey = key;
        _shellActionService.PersistSharedValue("LaunchArgumentJavaSelect", key == "auto" ? string.Empty : key);
        SyncJavaSelection();
        _ = RefreshLaunchProfileCompositionAsync();
        RaisePropertyChanged(nameof(IsAutoJavaSelected));
        AddActivity(
            LT("setup.java.activities.select_default"),
            key == "auto" ? LT("setup.java.activities.auto_select") : key);
    }

    private void ToggleJavaEnabled(string key)
    {
        LogWrapper.Info(SetupJavaLogModule, $"ToggleJavaEnabled requested: key='{key}', selected='{_selectedJavaRuntimeKey}'.");
        var entry = JavaRuntimeEntries.FirstOrDefault(item => item.Key == key);
        if (entry is null)
        {
            LogWrapper.Warn(SetupJavaLogModule, $"ToggleJavaEnabled skipped: runtime entry not found for key='{key}'.");
            return;
        }

        LogWrapper.Info(SetupJavaLogModule, $"Current runtime state before toggle: key='{key}', enabled={entry.IsEnabled}, title='{entry.Title}'.");

        if (_selectedJavaRuntimeKey == key && entry.IsEnabled)
        {
            // Keep the launch selection valid, then disable the runtime.
            LogWrapper.Info(SetupJavaLogModule, $"Runtime '{key}' is currently selected and enabled. Auto-switching selection to 'auto' before disabling.");
            SelectJavaRuntime("auto");
        }

        entry.IsEnabled = !entry.IsEnabled;
        var items = LoadStoredJavaItems();
        var updated = items.FindIndex(item => string.Equals(item.Path, key, StringComparison.OrdinalIgnoreCase));
        LogWrapper.Info(SetupJavaLogModule, $"Persisting runtime toggle: key='{key}', nextEnabled={entry.IsEnabled}, storedItems={items.Count}, matchIndex={updated}.");
        if (updated >= 0)
        {
            items[updated] = new JavaStorageItem
            {
                Path = items[updated].Path,
                IsEnable = entry.IsEnabled,
                Source = items[updated].Source,
                Installation = items[updated].Installation
            };
        }
        else
        {
            items.Add(new JavaStorageItem
            {
                Path = key,
                IsEnable = entry.IsEnabled,
                Source = JavaSource.AutoScanned
            });
        }

        SaveStoredJavaItems(items);
        var persistedState = LoadStoredJavaItems()
            .FirstOrDefault(item => string.Equals(item.Path, key, StringComparison.OrdinalIgnoreCase))
            ?.IsEnable;
        LogWrapper.Info(SetupJavaLogModule, $"Toggle persisted for key='{key}': persistedEnabled={(persistedState is null ? "<missing>" : persistedState.Value)}.");
        ReloadSetupComposition(initializeAllSurfaces: false);
        AddActivity(
            entry.IsEnabled ? LT("setup.java.activities.enable") : LT("setup.java.activities.disable"),
            LT(
                "setup.java.activities.toggle_result",
                ("title", entry.Title),
                ("state", entry.IsEnabled ? LT("setup.java.activities.enabled_state") : LT("setup.java.activities.disabled_state"))));
    }

    private void SyncJavaSelection()
    {
        foreach (var entry in JavaRuntimeEntries)
        {
            entry.IsSelected = _selectedJavaRuntimeKey == entry.Key;
        }
    }

    private List<JavaStorageItem> LoadStoredJavaItems()
    {
        try
        {
            var provider = _shellActionService.RuntimePaths.OpenLocalConfigProvider();
            var rawJson = provider.Exists("LaunchArgumentJavaUser")
                ? provider.Get<string>("LaunchArgumentJavaUser")
                : "[]";
            var parsed = FrontendJavaInventoryService.ParseStorageItems(rawJson).ToList();
            LogWrapper.Trace(
                SetupJavaLogModule,
                $"Loaded Java storage items from '{_shellActionService.RuntimePaths.LocalConfigPath}': rawLength={rawJson.Length}, parsedCount={parsed.Count}.");
            return parsed;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, SetupJavaLogModule, "Failed to load Java storage items from local config.");
            return [];
        }
    }

    private void SaveStoredJavaItems(IReadOnlyList<JavaStorageItem> items)
    {
        var rawJson = JsonSerializer.Serialize(items);
        LogWrapper.Info(
            SetupJavaLogModule,
            $"Saving Java storage items to '{_shellActionService.RuntimePaths.LocalConfigPath}': count={items.Count}, rawLength={rawJson.Length}.");
        _shellActionService.PersistLocalValue("LaunchArgumentJavaUser", rawJson);
    }
}
