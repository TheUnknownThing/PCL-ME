using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Dialogs;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private async Task SelectDownloadFolderAsync()
    {
        string? selectedFolder;
        try
        {
            selectedFolder = await _launcherActionService.PickFolderAsync(LT("shell.tools.test.custom_download.pick_folder_title"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.custom_download.pick_folder_failure"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            AddActivity(
                LT("shell.tools.test.custom_download.pick_folder_activity"),
                LT("shell.tools.test.custom_download.pick_folder_cancelled"));
            return;
        }

        ToolDownloadFolder = selectedFolder;
        AddActivity(LT("shell.tools.test.custom_download.pick_folder_activity"), ToolDownloadFolder);
    }

    private Task StartCustomDownloadAsync()
    {
        if (!Uri.TryCreate(ToolDownloadUrl, UriKind.Absolute, out var uri))
        {
            AddFailureActivity(
                LT("shell.tools.test.custom_download.start_failure"),
                LT("shell.tools.test.custom_download.invalid_address"));
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(ToolDownloadFolder))
        {
            AddFailureActivity(
                LT("shell.tools.test.custom_download.start_failure"),
                LT("shell.tools.test.custom_download.missing_folder"));
            return Task.CompletedTask;
        }

        var fileName = string.IsNullOrWhiteSpace(ToolDownloadName)
            ? Path.GetFileName(uri.LocalPath)
            : ToolDownloadName.Trim();
        fileName = string.IsNullOrWhiteSpace(fileName) ? "download.bin" : SanitizeFileSegment(fileName);

        var targetDirectory = Path.GetFullPath(ToolDownloadFolder);
        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, fileName);

        try
        {
            var userAgent = string.IsNullOrWhiteSpace(ToolDownloadUserAgent)
                ? null
                : ToolDownloadUserAgent.Trim();
            TaskCenter.Register(new FrontendManagedFileDownloadTask(
                $"Custom download: {fileName}",
                uri.ToString(),
                targetPath,
                ResolveDownloadRequestTimeout(),
                _launcherActionService.GetDownloadTransferOptions(),
                onStarted: filePath => AvaloniaHintBus.Show($"Starting download of {Path.GetFileName(filePath)}", AvaloniaHintTheme.Info),
                onCompleted: filePath => AvaloniaHintBus.Show($"{Path.GetFileName(filePath)} downloaded", AvaloniaHintTheme.Success),
                onFailed: message => AvaloniaHintBus.Show(message, AvaloniaHintTheme.Error),
                userAgent: userAgent));

            AddActivity(LT("shell.tools.test.custom_download.start_activity"), $"{uri} -> {targetPath}");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.custom_download.start_failure"), ex.Message);
        }

        return Task.CompletedTask;
    }

    private void OpenCustomDownloadFolder()
    {
        var folder = string.IsNullOrWhiteSpace(ToolDownloadFolder)
            ? Path.Combine(_launcherActionService.RuntimePaths.FrontendArtifactDirectory, "tool-downloads")
            : Path.GetFullPath(ToolDownloadFolder);
        Directory.CreateDirectory(folder);
        if (_launcherActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity(LT("shell.tools.test.custom_download.open_folder_activity"), folder);
        }
        else
        {
            AddFailureActivity(LT("shell.tools.test.custom_download.open_folder_failure"), error ?? folder);
        }
    }
}
