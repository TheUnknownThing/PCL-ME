namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void OpenBackgroundFolder()
    {
        var folder = GetBackgroundFolderPath();
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity(LT("setup.ui.background.activities.open_folder"), folder);
        }
        else
        {
            AddFailureActivity(LT("setup.ui.background.activities.open_folder_failed"), error ?? folder);
        }
    }

    private void RefreshBackgroundAssets()
    {
        RefreshBackgroundContentState(selectNewAsset: true, addActivity: true);
    }

    private void ClearBackgroundAssets()
    {
        var folder = GetBackgroundFolderPath();
        var removedCount = DeleteDirectoryContents(folder, BackgroundCleanupExtensions);
        RefreshBackgroundContentState(selectNewAsset: false, addActivity: false);
        AddActivity(
            LT("setup.ui.background.activities.clear"),
            removedCount == 0
                ? LT("setup.ui.background.activities.clear_empty")
                : LT("setup.ui.background.activities.clear_count", ("count", removedCount)));
    }

    private void OpenMusicFolder()
    {
        var folder = GetMusicFolderPath();
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity(LT("setup.ui.music.activities.open_folder"), folder);
        }
        else
        {
            AddFailureActivity(LT("setup.ui.music.activities.open_folder_failed"), error ?? folder);
        }
    }

    private void RefreshMusicAssets()
    {
        var assets = EnumerateMediaFiles(GetMusicFolderPath(), MusicMediaExtensions).ToArray();
        AddActivity(
            LT("setup.ui.music.activities.refresh"),
            assets.Length == 0
                ? LT("setup.ui.music.activities.empty")
                : LT("setup.ui.music.activities.refreshed_count", ("count", assets.Length)));
    }

    private void ClearMusicAssets()
    {
        var folder = GetMusicFolderPath();
        var removedCount = DeleteDirectoryContents(folder, MusicMediaExtensions);
        AddActivity(
            LT("setup.ui.music.activities.clear"),
            removedCount == 0
                ? LT("setup.ui.music.activities.clear_empty")
                : LT("setup.ui.music.activities.clear_count", ("count", removedCount)));
    }

    private async Task ChangeLogoImageAsync()
    {
        string? sourcePath;

        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                LT("setup.ui.title_bar.activities.change_image_pick_title"),
                LT("setup.ui.title_bar.activities.change_image_pick_filter"),
                "*.png",
                "*.jpg",
                "*.jpeg",
                "*.gif",
                "*.webp");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.ui.title_bar.activities.change_image_failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity(
                LT("setup.ui.title_bar.activities.change_image"),
                LT("setup.ui.title_bar.activities.change_image_cancelled"));
            return;
        }

        var targetPath = GetLogoImagePath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, true);
        if (SelectedLogoTypeIndex != 3)
        {
            SelectedLogoTypeIndex = 3;
        }

        RefreshTitleBarLogoImage();
        AddActivity(LT("setup.ui.title_bar.activities.change_image"), $"{sourcePath} -> {targetPath}");
    }

    private void DeleteLogoImage()
    {
        var targetPath = GetLogoImagePath();
        if (!File.Exists(targetPath))
        {
            AddActivity(
                LT("setup.ui.title_bar.activities.clear_image"),
                LT("setup.ui.title_bar.activities.clear_image_empty"));
            return;
        }

        File.Delete(targetPath);
        if (SelectedLogoTypeIndex == 3)
        {
            SelectedLogoTypeIndex = 1;
        }

        RefreshTitleBarLogoImage();
        AddActivity(LT("setup.ui.title_bar.activities.clear_image"), targetPath);
    }

    private void RefreshHomepageContent()
    {
        RefreshLaunchHomepage(forceRefresh: true, addActivity: true);
    }

    private void GenerateHomepageTutorialFile()
    {
        var sourcePath = Path.Combine(LauncherRootDirectory, "Resources", "Custom.xml");
        if (!File.Exists(sourcePath))
        {
            AddFailureActivity(
                LT("setup.ui.homepage.activities.generate_tutorial_failed"),
                LT("setup.ui.homepage.activities.tutorial_template_missing", ("path", sourcePath)));
            return;
        }

        var targetPath = GetHomepageTutorialPath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, true);
        AddActivity(LT("setup.ui.homepage.activities.generate_tutorial"), targetPath);
    }

    private void ViewHomepageTutorial() => _ = ViewHomepageTutorialAsync();

    private async Task ViewHomepageTutorialAsync()
    {
        var result = await ShowToolboxConfirmationAsync(
            LT("setup.ui.homepage.activities.view_tutorial"),
            LT("setup.ui.homepage.activities.tutorial_content"));
        if (result is null)
        {
            return;
        }

        AddActivity(
            LT("setup.ui.homepage.activities.view_tutorial"),
            LT("setup.ui.homepage.activities.tutorial_shown"));
    }

    private static readonly string[] BackgroundCleanupExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".bmp",
        ".mp4",
        ".webm",
        ".avi",
        ".mkv",
        ".mov"
    ];

    private static readonly string[] MusicMediaExtensions =
    [
        ".mp3",
        ".flac",
        ".wav",
        ".ogg",
        ".m4a",
        ".aac"
    ];

    private string GetBackgroundFolderPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.DataDirectory, "Pictures");
    }

    private string GetMusicFolderPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.DataDirectory, "Musics");
    }

    private string GetLogoImagePath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.DataDirectory, "Logo.png");
    }

    private string GetHomepageTutorialPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.DataDirectory, "Custom.xaml");
    }

    private static IEnumerable<string> EnumerateMediaFiles(string folder, IEnumerable<string> allowedExtensions)
    {
        Directory.CreateDirectory(folder);
        var extensionSet = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => extensionSet.Contains(Path.GetExtension(path)));
    }

    private static int DeleteDirectoryContents(string folder, IEnumerable<string> allowedExtensions)
    {
        Directory.CreateDirectory(folder);
        var removedCount = 0;
        foreach (var file in EnumerateMediaFiles(folder, allowedExtensions))
        {
            try
            {
                File.Delete(file);
                removedCount++;
            }
            catch
            {
                // Ignore deletion failures for individual files and keep going.
            }
        }

        return removedCount;
    }
}
