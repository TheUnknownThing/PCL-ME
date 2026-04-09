using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string LaunchAvatarFallbackImagePath = FrontendLauncherAssetLocator.GetPath("Images", "Heads", "PCL-Community.png");
    private const string LaunchAvatarHeadCacheVersion = "v3";
    private static readonly HttpClient LaunchAvatarHttpClient = new();
    private string _launchAvatarImagePath = LaunchAvatarFallbackImagePath;
    private int _launchAvatarRefreshVersion;

    private void ScheduleLaunchAvatarRefresh()
    {
        var profile = _launchComposition.SelectedProfile;
        var version = Interlocked.Increment(ref _launchAvatarRefreshVersion);
        SetLaunchAvatarImagePath(TryGetCachedLaunchAvatarImagePath(profile) ?? LaunchAvatarFallbackImagePath);
        _ = RefreshLaunchAvatarAsync(version, profile);
    }

    private async Task RefreshLaunchAvatarAsync(int version, FrontendLaunchProfileSummary profile)
    {
        string resolvedPath;
        try
        {
            resolvedPath = await EnsureLaunchAvatarImagePathAsync(profile).ConfigureAwait(false);
        }
        catch
        {
            resolvedPath = LaunchAvatarFallbackImagePath;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (version != _launchAvatarRefreshVersion)
            {
                return;
            }

            SetLaunchAvatarImagePath(resolvedPath);
        });
    }

    private void SetLaunchAvatarImagePath(string? path)
    {
        var nextPath = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? path
            : LaunchAvatarFallbackImagePath;
        if (string.Equals(_launchAvatarImagePath, nextPath, StringComparison.Ordinal))
        {
            return;
        }

        _launchAvatarImagePath = nextPath;
        RaisePropertyChanged(nameof(LaunchAvatarImage));
    }

    private string? TryGetCachedLaunchAvatarImagePath(FrontendLaunchProfileSummary profile)
    {
        var headId = ResolveLaunchAvatarSkinHeadId(profile);
        if (string.IsNullOrWhiteSpace(headId))
        {
            return null;
        }

        var cachePath = GetLaunchAvatarHeadCachePath(headId);
        return IsNonEmptyFile(cachePath) ? cachePath : null;
    }

    private async Task<string> EnsureLaunchAvatarImagePathAsync(FrontendLaunchProfileSummary profile)
    {
        var headId = ResolveLaunchAvatarSkinHeadId(profile);
        if (string.IsNullOrWhiteSpace(headId))
        {
            return LaunchAvatarFallbackImagePath;
        }

        var headCachePath = GetLaunchAvatarHeadCachePath(headId);
        if (IsNonEmptyFile(headCachePath))
        {
            return headCachePath;
        }

        var skinPath = await EnsureLaunchAvatarSkinSourceAsync(profile, headId).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(skinPath) || !File.Exists(skinPath))
        {
            return LaunchAvatarFallbackImagePath;
        }

        return TryWriteLaunchAvatarHeadCache(skinPath, headCachePath)
            ? headCachePath
            : LaunchAvatarFallbackImagePath;
    }

    private async Task<string> EnsureLaunchAvatarSkinSourceAsync(FrontendLaunchProfileSummary profile, string headId)
    {
        var fallbackSkinPath = ResolveLaunchAvatarFallbackSkinPath(profile);
        if (profile.Kind != MinecraftLaunchProfileKind.Microsoft)
        {
            return fallbackSkinPath;
        }

        var skinUrl = TryGetActiveMicrosoftSkinUrl(profile.RawJson);
        if (string.IsNullOrWhiteSpace(skinUrl))
        {
            return fallbackSkinPath;
        }

        var skinCachePath = GetLaunchAvatarSkinCachePath(headId);
        if (IsNonEmptyFile(skinCachePath))
        {
            return skinCachePath;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(skinCachePath)!);
            var bytes = await LaunchAvatarHttpClient.GetByteArrayAsync(skinUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(skinCachePath, bytes).ConfigureAwait(false);
            return skinCachePath;
        }
        catch
        {
            TryDeleteFile(skinCachePath);
            return fallbackSkinPath;
        }
    }

    private static bool TryWriteLaunchAvatarHeadCache(string skinPath, string cachePath)
    {
        try
        {
            using var skinBitmap = new Bitmap(skinPath);
            var width = skinBitmap.PixelSize.Width;
            var height = skinBitmap.PixelSize.Height;
            if (width < 32 || height < 32)
            {
                return false;
            }

            var scale = Math.Max(1, width / 64);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            using var headBitmap = new RenderTargetBitmap(new PixelSize(56, 56));
            using (var context = headBitmap.CreateDrawingContext())
            {
                using (context.PushRenderOptions(new RenderOptions
                       {
                           BitmapInterpolationMode = BitmapInterpolationMode.None
                       }))
                {
                    context.DrawImage(
                        skinBitmap,
                        new Rect(scale * 8, scale * 8, scale * 8, scale * 8),
                        new Rect(4, 4, 48, 48));

                    if (width >= 64 && height >= 32)
                    {
                        context.DrawImage(
                            skinBitmap,
                            new Rect(scale * 40, scale * 8, scale * 8, scale * 8),
                            new Rect(0, 0, 56, 56));
                    }
                }
            }

            headBitmap.Save(cachePath);
            return true;
        }
        catch
        {
            TryDeleteFile(cachePath);
            return false;
        }
    }

    private string GetLaunchAvatarHeadCachePath(string headId)
    {
        return Path.Combine(
            _shellActionService.RuntimePaths.TempDirectory,
            "Cache",
            "Skin",
            "Head",
            LaunchAvatarHeadCacheVersion,
            $"{SanitizeFileSegment(headId)}.png");
    }

    private string GetLaunchAvatarSkinCachePath(string headId)
    {
        return Path.Combine(
            _shellActionService.RuntimePaths.TempDirectory,
            "Cache",
            "Skin",
            $"{SanitizeFileSegment(headId)}.png");
    }

    private static string ResolveLaunchAvatarFallbackSkinPath(FrontendLaunchProfileSummary profile)
    {
        return FrontendLauncherAssetLocator.GetPath(
            "Images",
            "Skins",
            $"{ResolveLaunchAvatarDefaultSkinName(profile)}.png");
    }

    private static string? ResolveLaunchAvatarSkinHeadId(FrontendLaunchProfileSummary profile)
    {
        var microsoftHeadId = profile.Kind == MinecraftLaunchProfileKind.Microsoft
            ? TryResolveMicrosoftSkinHeadId(profile.RawJson)
            : null;
        if (!string.IsNullOrWhiteSpace(microsoftHeadId))
        {
            return microsoftHeadId;
        }

        if (!string.IsNullOrWhiteSpace(profile.SkinHeadId))
        {
            return profile.SkinHeadId;
        }

        return profile.Kind switch
        {
            MinecraftLaunchProfileKind.Legacy => ResolveLaunchAvatarDefaultSkinName(profile),
            MinecraftLaunchProfileKind.Microsoft when !string.IsNullOrWhiteSpace(profile.Uuid) => profile.Uuid,
            MinecraftLaunchProfileKind.Microsoft when !string.IsNullOrWhiteSpace(profile.UserName) => CreateOfflineUuid(profile.UserName),
            MinecraftLaunchProfileKind.Auth when !string.IsNullOrWhiteSpace(profile.Uuid) => profile.Uuid,
            MinecraftLaunchProfileKind.Auth when !string.IsNullOrWhiteSpace(profile.UserName) => CreateOfflineUuid(profile.UserName),
            _ => null
        };
    }

    private static string ResolveLaunchAvatarDefaultSkinName(FrontendLaunchProfileSummary profile)
    {
        var uuid = profile.Kind switch
        {
            MinecraftLaunchProfileKind.Microsoft when !string.IsNullOrWhiteSpace(profile.UserName) => CreateOfflineUuid(profile.UserName),
            _ when !string.IsNullOrWhiteSpace(profile.Uuid) => profile.Uuid!,
            _ when !string.IsNullOrWhiteSpace(profile.UserName) => CreateOfflineUuid(profile.UserName),
            _ => string.Empty
        };

        if (uuid.Length != 32)
        {
            return "Steve";
        }

        var a = int.Parse(uuid[7].ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var b = int.Parse(uuid[15].ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var c = int.Parse(uuid[23].ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var d = int.Parse(uuid[31].ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return ((a ^ b ^ c ^ d) & 1) == 1 ? "Alex" : "Steve";
    }

    private static string? TryResolveMicrosoftSkinHeadId(string? rawJson)
    {
        var skinUrl = TryGetActiveMicrosoftSkinUrl(rawJson);
        if (string.IsNullOrWhiteSpace(skinUrl))
        {
            return null;
        }

        if (Uri.TryCreate(skinUrl, UriKind.Absolute, out var uri))
        {
            var segment = uri.Segments.LastOrDefault()?.Trim('/');
            return string.IsNullOrWhiteSpace(segment) ? null : Uri.UnescapeDataString(segment);
        }

        var fileName = Path.GetFileNameWithoutExtension(skinUrl);
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    private static string? TryGetActiveMicrosoftSkinUrl(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(rawJson) is not JsonObject root ||
                root["skins"] is not JsonArray skins)
            {
                return null;
            }

            var activeSkin = skins
                .OfType<JsonObject>()
                .Where(skin => !string.IsNullOrWhiteSpace(skin["url"]?.ToString()))
                .OrderByDescending(skin => string.Equals(skin["state"]?.ToString(), "ACTIVE", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            return activeSkin?["url"]?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNonEmptyFile(string path)
    {
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cache cleanup failures.
        }
    }
}
