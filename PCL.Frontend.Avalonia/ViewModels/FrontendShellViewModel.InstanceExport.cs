using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string InstanceExportConfigSeparator = "==============================================================";
    private const string InstanceExportConfigCacheKey = "CacheExportConfig";
    private string _instanceExportName = string.Empty;
    private string _instanceExportVersion = string.Empty;
    private bool _instanceExportIncludeResources;
    private bool _instanceExportModrinthMode;

    public ObservableCollection<ExportOptionGroupViewModel> InstanceExportOptionGroups { get; } = [];

    public string InstanceExportName
    {
        get => _instanceExportName;
        set => SetProperty(ref _instanceExportName, value);
    }

    public string InstanceExportVersion
    {
        get => _instanceExportVersion;
        set => SetProperty(ref _instanceExportVersion, value);
    }

    public bool InstanceExportIncludeResources
    {
        get => _instanceExportIncludeResources;
        set
        {
            if (SetProperty(ref _instanceExportIncludeResources, value))
            {
                RaisePropertyChanged(nameof(ShowInstanceExportIncludeWarning));
            }
        }
    }

    public bool InstanceExportModrinthMode
    {
        get => _instanceExportModrinthMode;
        set
        {
            if (SetProperty(ref _instanceExportModrinthMode, value))
            {
                RaisePropertyChanged(nameof(ShowInstanceExportOptiFineWarning));
            }
        }
    }

    public bool ShowInstanceExportIncludeWarning => InstanceExportIncludeResources;

    public bool ShowInstanceExportOptiFineWarning => InstanceExportModrinthMode;

    public bool HasInstanceExportOptionGroups => InstanceExportOptionGroups.Count > 0;

    private void InitializeInstanceExportSurface()
    {
        var exportState = _instanceComposition.Export;
        _instanceExportName = exportState.Name;
        _instanceExportVersion = exportState.Version;
        _instanceExportIncludeResources = exportState.IncludeResources;
        _instanceExportModrinthMode = exportState.ModrinthMode;

        ReplaceItems(
            InstanceExportOptionGroups,
            exportState.OptionGroups.Select(group => CreateExportOptionGroup(
                group.Key,
                group.Title,
                group.Description,
                group.IsChecked,
                group.Children.Select(child => CreateExportOption(child.Key, child.Title, child.Description, child.IsChecked)).ToArray())));
    }

    private void RefreshInstanceExportSurface()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceExport))
        {
            return;
        }

        RaisePropertyChanged(nameof(InstanceExportName));
        RaisePropertyChanged(nameof(InstanceExportVersion));
        RaisePropertyChanged(nameof(InstanceExportIncludeResources));
        RaisePropertyChanged(nameof(InstanceExportModrinthMode));
        RaisePropertyChanged(nameof(ShowInstanceExportIncludeWarning));
        RaisePropertyChanged(nameof(ShowInstanceExportOptiFineWarning));
        RaisePropertyChanged(nameof(HasInstanceExportOptionGroups));
    }

    private void ResetInstanceExportOptions()
    {
        ReloadInstanceComposition();
        RefreshInstanceExportSurface();
        AddActivity(
            T("instance.export.activities.reset"),
            T("instance.export.messages.reset_completed"));
    }

    private async Task ImportInstanceExportConfigAsync()
    {
        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                T("instance.export.dialogs.import_config.title"),
                T("instance.export.dialogs.import_config.filter_name"),
                "*.txt");
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", T("instance.export.activities.import_config"))), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity(T("instance.export.activities.import_config"), T("instance.export.messages.import_config_canceled"));
            return;
        }

        string content;
        try
        {
            content = File.ReadAllText(sourcePath);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", T("instance.export.activities.import_config"))), ex.Message);
            return;
        }

        ApplyInstanceExportConfig(content);
        AddActivity(T("instance.export.activities.import_config"), sourcePath);
    }

    private void SaveInstanceExportConfig() => _ = SaveInstanceExportConfigAsync();

    private async Task SaveInstanceExportConfigAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(T("instance.export.activities.save_config"), T("instance.export.messages.no_instance_selected"));
            return;
        }

        var lines = new List<string>
        {
            $"Name:{InstanceExportName}",
            $"Version:{InstanceExportVersion}",
            $"IncludeResources:{InstanceExportIncludeResources}",
            $"ModrinthMode:{InstanceExportModrinthMode}",
            InstanceExportConfigSeparator
        };

        foreach (var group in InstanceExportOptionGroups)
        {
            lines.Add($"Group:{group.Header.Key}|{group.Header.IsChecked}");
            foreach (var child in group.Children)
            {
                lines.Add($"Item:{group.Header.Key}|{child.Key}|{child.IsChecked}");
            }
        }

        string? suggestedStartFolder = null;
        try
        {
            var provider = _shellActionService.RuntimePaths.OpenSharedConfigProvider();
            if (provider.Exists(InstanceExportConfigCacheKey))
            {
                var cachedPath = provider.Get<string>(InstanceExportConfigCacheKey);
                suggestedStartFolder = Path.GetDirectoryName(cachedPath);
            }
        }
        catch
        {
            // Ignore invalid cached save locations and fall back to the system default picker directory.
        }

        string? outputPath;
        try
        {
            outputPath = await _shellActionService.PickSaveFileAsync(
                T("instance.export.dialogs.save_config.title"),
                T("instance.export.dialogs.save_config.default_name"),
                T("instance.export.dialogs.save_config.filter_name"),
                suggestedStartFolder,
                "*.txt");
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", T("instance.export.activities.save_config"))), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
            _shellActionService.PersistSharedValue(InstanceExportConfigCacheKey, outputPath);
            if (_shellActionService.TryRevealExternalTarget(outputPath, out var error))
            {
                AddActivity(T("instance.export.activities.save_config"), outputPath);
                return;
            }

            AddFailureActivity(T("common.activities.failed", ("title", T("instance.export.activities.save_config"))), error ?? outputPath);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", T("instance.export.activities.save_config"))), ex.Message);
        }
    }

    private void OpenInstanceExportGuide() => _ = OpenInstanceExportGuideAsync();

    private async Task OpenInstanceExportGuideAsync()
    {
        if (TryResolveHelpEntry("指南/整合包制作.json", out var guideEntry))
        {
            ShowHelpDetail(guideEntry, addActivity: true);
            return;
        }

        var lines = new[]
        {
            T("instance.export.guide.title"),
            string.Empty,
            T("instance.export.guide.step_1"),
            T("instance.export.guide.step_2"),
            T("instance.export.guide.step_3"),
            T("instance.export.guide.step_4"),
            T("instance.export.guide.step_5")
        };
        var result = await ShowToolboxConfirmationAsync(
            T("instance.export.guide.title"),
            string.Join(Environment.NewLine, lines.Skip(2)));
        if (result is null)
        {
            return;
        }

        AddActivity(T("instance.export.activities.guide"), T("instance.export.messages.guide_shown"));
    }

    private void StartInstanceExport()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(T("instance.export.activities.start"), T("instance.export.messages.no_instance_selected"));
            return;
        }

        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-exports");
        Directory.CreateDirectory(exportDirectory);
        var archiveName = $"{GetEffectiveInstanceExportName()} {GetEffectiveInstanceExportVersion()}{(InstanceExportModrinthMode ? ".mrpack" : ".zip")}";
        var archivePath = Path.Combine(exportDirectory, archiveName);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        var sources = CollectInstanceExportSources()
            .GroupBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var source in sources)
            {
                AddInstanceExportSourceToArchive(archive, source.SourcePath, source.ArchivePath);
            }
        }

        OpenInstanceTarget(T("instance.export.activities.start"), archivePath, T("instance.export.messages.archive_missing"));
    }

    private ExportOptionEntryViewModel CreateExportOption(string key, string title, string description, bool isChecked)
    {
        return new ExportOptionEntryViewModel(
            key,
            LocalizeExportTitle(title),
            LocalizeExportDescription(description),
            isChecked);
    }

    private ExportOptionGroupViewModel CreateExportOptionGroup(
        string key,
        string title,
        string description,
        bool isChecked,
        IReadOnlyList<ExportOptionEntryViewModel> children)
    {
        return new ExportOptionGroupViewModel(
            new ExportOptionEntryViewModel(
                key,
                LocalizeExportTitle(title),
                LocalizeExportDescription(description),
                isChecked),
            children);
    }

    private void ApplyInstanceExportConfig(string content)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)
                || string.Equals(line, InstanceExportConfigSeparator, StringComparison.Ordinal)
                || line.StartsWith('#'))
            {
                continue;
            }

            if (TryParseConfigAssignment(line, "Name", out var name))
            {
                InstanceExportName = name;
                continue;
            }

            if (TryParseConfigAssignment(line, "Version", out var version))
            {
                InstanceExportVersion = version;
                continue;
            }

            if (TryParseConfigAssignment(line, "IncludeResources", out var includeResources)
                && bool.TryParse(includeResources, out var includeResourcesValue))
            {
                InstanceExportIncludeResources = includeResourcesValue;
                continue;
            }

            if (TryParseConfigAssignment(line, "ModrinthMode", out var modrinthMode)
                && bool.TryParse(modrinthMode, out var modrinthModeValue))
            {
                InstanceExportModrinthMode = modrinthModeValue;
                continue;
            }

            if (line.StartsWith("Group:", StringComparison.Ordinal))
            {
                var groupParts = line["Group:".Length..].Split('|', 2, StringSplitOptions.TrimEntries);
                if (groupParts.Length == 2
                    && bool.TryParse(groupParts[1], out var isChecked)
                    && TryFindInstanceExportGroup(groupParts[0], out var group))
                {
                    group.Header.IsChecked = isChecked;
                }

                continue;
            }

            if (!line.StartsWith("Item:", StringComparison.Ordinal))
            {
                continue;
            }

            var itemParts = line["Item:".Length..].Split('|', 3, StringSplitOptions.TrimEntries);
            if (itemParts.Length == 3
                && bool.TryParse(itemParts[2], out var isCheckedValue)
                && TryFindInstanceExportGroup(itemParts[0], out var selectedGroup))
            {
                var child = selectedGroup.Children.FirstOrDefault(entry =>
                    string.Equals(entry.Key, itemParts[1], StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.Title, itemParts[1], StringComparison.OrdinalIgnoreCase));
                if (child is not null)
                {
                    child.IsChecked = isCheckedValue;
                }
            }
        }
    }

    private IEnumerable<(string SourcePath, string ArchivePath)> CollectInstanceExportSources()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            yield break;
        }

        var indieDirectory = _instanceComposition.Selection.IndieDirectory;
        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;

        foreach (var group in InstanceExportOptionGroups.Where(entry => entry.Header.IsChecked))
        {
            switch (group.Header.Key)
            {
                case "game":
                    foreach (var child in group.Children.Where(entry => entry.IsChecked))
                    {
                        var fileName = child.Key switch
                        {
                            "game_settings" => "options.txt",
                            "game_personal" => "optionsof.txt",
                            "optifine_settings" => "optionsof.txt",
                            _ => null
                        };
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            continue;
                        }

                        var sourcePath = Path.Combine(indieDirectory, fileName);
                        if (File.Exists(sourcePath))
                        {
                            yield return (sourcePath, Path.Combine("overrides", fileName));
                        }
                    }
                    break;
                case "mods":
                    foreach (var child in group.Children.Where(entry => entry.IsChecked))
                    {
                        switch (child.Key)
                        {
                            case "disabled_mods":
                                foreach (var entry in _instanceComposition.DisabledMods.Entries)
                                {
                                    yield return (entry.Path, BuildOverrideArchivePath(indieDirectory, entry.Path));
                                }
                                break;
                            case "important_data":
                            case "mod_settings":
                                var configDirectory = Path.Combine(indieDirectory, "config");
                                if (Directory.Exists(configDirectory))
                                {
                                    yield return (configDirectory, BuildOverrideArchivePath(indieDirectory, configDirectory));
                                }
                                break;
                        }
                    }
                    break;
                case "resource_packs":
                    foreach (var source in ResolveCheckedExportEntries(group, _instanceComposition.ResourcePacks.Entries))
                    {
                        yield return source;
                    }
                    break;
                case "shaders":
                    foreach (var source in ResolveCheckedExportEntries(group, _instanceComposition.Shaders.Entries))
                    {
                        yield return source;
                    }
                    break;
                case "screenshots":
                    foreach (var entry in _instanceComposition.Screenshot.Entries)
                    {
                        yield return (entry.Path, BuildOverrideArchivePath(indieDirectory, entry.Path));
                    }
                    break;
                case "schematics":
                    foreach (var source in ResolveCheckedExportEntries(group, _instanceComposition.Schematics.Entries))
                    {
                        yield return source;
                    }
                    break;
                case "replays":
                    foreach (var child in group.Children.Where(entry => entry.IsChecked))
                    {
                        if (File.Exists(child.Key))
                        {
                            yield return (child.Key, BuildOverrideArchivePath(indieDirectory, child.Key));
                        }
                    }
                    break;
                case "worlds":
                    foreach (var source in ResolveCheckedExportEntries(group, _instanceComposition.World.Entries))
                    {
                        yield return source;
                    }
                    break;
                case "servers":
                    var serversPath = Path.Combine(indieDirectory, "servers.dat");
                    if (File.Exists(serversPath))
                    {
                        yield return (serversPath, BuildOverrideArchivePath(indieDirectory, serversPath));
                    }
                    break;
                case "launcher":
                    if (group.Children.Any(child => child.IsChecked))
                    {
                        var pclDirectory = Path.Combine(instanceDirectory, "PCL");
                        if (Directory.Exists(pclDirectory))
                        {
                            yield return (pclDirectory, Path.Combine("overrides", "PCL"));
                        }
                    }
                    break;
            }
        }

        if (InstanceExportIncludeResources)
        {
            foreach (var entry in _instanceComposition.Mods.Entries.Concat(_instanceComposition.ResourcePacks.Entries).Concat(_instanceComposition.Shaders.Entries))
            {
                yield return (entry.Path, BuildOverrideArchivePath(indieDirectory, entry.Path));
            }
        }
    }

    private IEnumerable<(string SourcePath, string ArchivePath)> ResolveCheckedExportEntries(
        ExportOptionGroupViewModel group,
        IEnumerable<FrontendInstanceResourceEntry> entries)
    {
        foreach (var child in group.Children.Where(entry => entry.IsChecked))
        {
            var source = entries.FirstOrDefault(entry =>
                string.Equals(entry.Path, child.Key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Title, child.Title, StringComparison.OrdinalIgnoreCase));
            if (source is not null)
            {
                yield return (source.Path, BuildOverrideArchivePath(_instanceComposition.Selection.IndieDirectory, source.Path));
            }
        }
    }

    private IEnumerable<(string SourcePath, string ArchivePath)> ResolveCheckedExportEntries(
        ExportOptionGroupViewModel group,
        IEnumerable<FrontendInstanceDirectoryEntry> entries)
    {
        foreach (var child in group.Children.Where(entry => entry.IsChecked))
        {
            var source = entries.FirstOrDefault(entry =>
                string.Equals(entry.Path, child.Key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Title, child.Title, StringComparison.OrdinalIgnoreCase));
            if (source is not null)
            {
                yield return (source.Path, BuildOverrideArchivePath(_instanceComposition.Selection.IndieDirectory, source.Path));
            }
        }
    }

    private static string BuildOverrideArchivePath(string indieDirectory, string sourcePath)
    {
        return Path.Combine("overrides", Path.GetRelativePath(indieDirectory, sourcePath));
    }

    private static void AddInstanceExportSourceToArchive(ZipArchive archive, string sourcePath, string archivePath)
    {
        if (File.Exists(sourcePath))
        {
            archive.CreateEntryFromFile(sourcePath, NormalizeArchivePath(archivePath));
            return;
        }

        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            archive.CreateEntryFromFile(file, NormalizeArchivePath(Path.Combine(archivePath, relativePath)));
        }
    }

    private static string NormalizeArchivePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private bool TryFindInstanceExportGroup(string keyOrTitle, out ExportOptionGroupViewModel group)
    {
        group = InstanceExportOptionGroups.FirstOrDefault(entry =>
            string.Equals(entry.Header.Key, keyOrTitle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.Header.Title, keyOrTitle, StringComparison.OrdinalIgnoreCase))!;
        return group is not null;
    }

    private static bool TryParseConfigAssignment(string line, string key, out string value)
    {
        if (line.StartsWith($"{key}:", StringComparison.Ordinal))
        {
            value = line[(key.Length + 1)..].Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private string GetEffectiveInstanceExportName()
    {
        return string.IsNullOrWhiteSpace(InstanceExportName)
            ? _instanceComposition.Selection.InstanceName
            : InstanceExportName;
    }

    private string GetEffectiveInstanceExportVersion()
    {
        return string.IsNullOrWhiteSpace(InstanceExportVersion)
            ? "1.0.0"
            : InstanceExportVersion;
    }
}
