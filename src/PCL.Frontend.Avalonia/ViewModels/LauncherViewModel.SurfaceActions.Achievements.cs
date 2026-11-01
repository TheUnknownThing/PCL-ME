using System.Net.Http;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private void SaveOfficialSkin()
    {
        _ = SaveOfficialSkinAsync();
    }

    private async Task PreviewAchievementAsync()
    {
        var url = GetAchievementUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            AddFailureActivity("Achievement preview failed", "Enter valid achievement text first.");
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            AchievementPreviewImage = new Bitmap(stream);
            ShowAchievementPreview = true;
            AddActivity("Achievement preview", $"Loaded the achievement image for {AchievementTitle.Trim()}.\n{url}");
        }
        catch (Exception ex)
        {
            ShowAchievementPreview = false;
            AchievementPreviewImage = null;
            AddFailureActivity("Achievement preview failed", ex.Message);
        }
    }

    private async Task SelectHeadSkinAsync()
    {
        string? sourcePath;
        try
        {
            sourcePath = await _launcherActionService.PickOpenFileAsync(
                LT("shell.tools.test.head.pick_skin_title"),
                LT("shell.tools.test.head.pick_skin_filter"),
                "*.png",
                "*.jpg",
                "*.jpeg");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.head.pick_skin_failure"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity(
                LT("shell.tools.test.head.pick_skin_activity"),
                LT("shell.tools.test.head.pick_skin_cancelled"));
            return;
        }

        SelectedHeadSkinPath = sourcePath;
        AddActivity(LT("shell.tools.test.head.pick_skin_activity"), SelectedHeadSkinPath);
    }

    private void RefreshHeadPreviewFromSelection(bool addActivity)
    {
        if (!HasSelectedHeadSkin || !File.Exists(SelectedHeadSkinPath))
        {
            HeadPreviewImage = null;
            return;
        }

        try
        {
            HeadPreviewImage = GenerateHeadPreviewBitmap(SelectedHeadSkinPath, SelectedHeadSizeIndex);
            if (addActivity)
            {
                AddActivity("Head preview", $"Generated a {HeadSizeOptions[SelectedHeadSizeIndex]} head preview.");
            }
        }
        catch (Exception ex)
        {
            HeadPreviewImage = null;
            if (addActivity)
            {
                AddFailureActivity("Head preview failed", ex.Message);
            }
        }
    }

    private Task SaveHeadAsync()
    {
        if (!HasSelectedHeadSkin || !File.Exists(SelectedHeadSkinPath))
        {
            AddActivity(
                LT("shell.tools.test.head.save_activity"),
                LT("shell.tools.test.head.no_skin_selected"));
            return Task.CompletedTask;
        }

        var previewImage = HeadPreviewImage;
        if (previewImage is null)
        {
            AddActivity(
                LT("shell.tools.test.head.save_activity"),
                LT("shell.tools.test.head.output_missing"));
            return Task.CompletedTask;
        }

        try
        {
            var outputDirectory = Path.Combine(_launcherActionService.RuntimePaths.FrontendArtifactDirectory, "heads");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(
                outputDirectory,
                $"{SanitizeFileSegment(Path.GetFileNameWithoutExtension(SelectedHeadSkinPath))}-{HeadSizeOptions[SelectedHeadSizeIndex]}.png");
            previewImage.Save(outputPath);
            OpenInstanceTarget(
                LT("shell.tools.test.head.save_activity"),
                outputPath,
                LT("shell.tools.test.head.output_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.head.save_failure"), ex.Message);
        }

        return Task.CompletedTask;
    }

    private static RenderTargetBitmap GenerateHeadPreviewBitmap(string skinPath, int selectedHeadSizeIndex)
    {
        using var skinBitmap = new Bitmap(skinPath);
        var width = skinBitmap.PixelSize.Width;
        var height = skinBitmap.PixelSize.Height;
        if (width < 64 || height < 32 || width % 64 != 0)
        {
            throw new InvalidOperationException("Invalid skin dimensions. Width must be a multiple of 64 and height must be at least 32 pixels.");
        }

        var scale = Math.Max(1, width / 64);
        var headSize = selectedHeadSizeIndex switch
        {
            0 => 64,
            1 => 96,
            _ => 128
        };
        var headBitmap = new RenderTargetBitmap(new PixelSize(headSize, headSize));
        using var context = headBitmap.CreateDrawingContext();
        using (context.PushRenderOptions(new RenderOptions
               {
                   BitmapInterpolationMode = BitmapInterpolationMode.None
               }))
        {
            context.DrawImage(
                skinBitmap,
                new Rect(scale * 8, scale * 8, scale * 8, scale * 8),
                new Rect(0, 0, headSize, headSize));
            if (width >= 64 && height >= 32)
            {
                context.DrawImage(
                    skinBitmap,
                    new Rect(scale * 40, scale * 8, scale * 8, scale * 8),
                    new Rect(0, 0, headSize, headSize));
            }
        }

        return headBitmap;
    }

    private async Task SaveOfficialSkinAsync()
    {
        if (string.IsNullOrWhiteSpace(OfficialSkinPlayerName))
        {
            AddFailureActivity(
                LT("shell.tools.test.official_skin.save_failure"),
                LT("shell.tools.test.official_skin.missing_player_name"));
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            var profileJson = await client.GetStringAsync($"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(OfficialSkinPlayerName.Trim())}");
            using var profileDocument = JsonDocument.Parse(profileJson);
            var uuid = profileDocument.RootElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(uuid))
            {
                AddFailureActivity(
                    LT("shell.tools.test.official_skin.save_failure"),
                    LT("shell.tools.test.official_skin.player_not_found"));
                return;
            }

            var sessionJson = await client.GetStringAsync($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
            using var sessionDocument = JsonDocument.Parse(sessionJson);
            var texturePayload = sessionDocument.RootElement
                .GetProperty("properties")
                .EnumerateArray()
                .FirstOrDefault(item => item.TryGetProperty("name", out var nameElement)
                    && string.Equals(nameElement.GetString(), "textures", StringComparison.Ordinal));
            if (!texturePayload.TryGetProperty("value", out var valueElement))
            {
                AddFailureActivity(
                    LT("shell.tools.test.official_skin.save_failure"),
                    LT("shell.tools.test.official_skin.missing_skin_payload"));
                return;
            }

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(valueElement.GetString() ?? string.Empty));
            using var textureDocument = JsonDocument.Parse(decoded);
            var textureUrl = textureDocument.RootElement
                .GetProperty("textures")
                .GetProperty("SKIN")
                .GetProperty("url")
                .GetString();
            if (string.IsNullOrWhiteSpace(textureUrl))
            {
                AddFailureActivity(
                    LT("shell.tools.test.official_skin.save_failure"),
                    LT("shell.tools.test.official_skin.missing_skin_url"));
                return;
            }

            var outputDirectory = Path.Combine(_launcherActionService.RuntimePaths.FrontendArtifactDirectory, "skins");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"{SanitizeFileSegment(OfficialSkinPlayerName.Trim())}.png");
            var bytes = await client.GetByteArrayAsync(textureUrl);
            await File.WriteAllBytesAsync(outputPath, bytes);
            OpenInstanceTarget(
                LT("shell.tools.test.official_skin.save_activity"),
                outputPath,
                LT("shell.tools.test.official_skin.output_missing"));
        }
        catch (HttpRequestException ex)
        {
            AddFailureActivity(LT("shell.tools.test.official_skin.save_failure"), ex.Message);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.official_skin.save_failure"), ex.Message);
        }
    }

    private async Task SaveAchievementAsync()
    {
        var url = GetAchievementUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            AddFailureActivity(
                LT("shell.tools.test.achievement.save_failure"),
                LT("shell.tools.test.achievement.invalid_content"));
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            var outputDirectory = Path.Combine(_launcherActionService.RuntimePaths.FrontendArtifactDirectory, "achievements");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"{SanitizeFileSegment(AchievementTitle)}.png");
            await File.WriteAllBytesAsync(outputPath, bytes);
            OpenInstanceTarget(
                LT("shell.tools.test.achievement.save_activity"),
                outputPath,
                LT("shell.tools.test.achievement.output_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.achievement.save_failure"), ex.Message);
        }
    }

    private string GetAchievementUrl()
    {
        var block = AchievementBlockId.Trim();
        var title = AchievementTitle.Trim().Replace(" ", "..", StringComparison.Ordinal);
        var firstLine = AchievementFirstLine.Trim().Replace(" ", "..", StringComparison.Ordinal);
        var secondLine = AchievementSecondLine.Trim().Replace(" ", "..", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(block) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        var url = $"https://minecraft-api.com/api/achivements/{Uri.EscapeDataString(block)}/{Uri.EscapeDataString(title)}/{Uri.EscapeDataString(firstLine)}";
        if (!string.IsNullOrWhiteSpace(secondLine))
        {
            url += $"/{Uri.EscapeDataString(secondLine)}";
        }

        return url;
    }
}
