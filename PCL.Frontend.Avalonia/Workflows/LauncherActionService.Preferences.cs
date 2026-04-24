using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.I18n;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.Processes;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class LauncherActionService
{
    public static void ApplyStoredAnimationPreferences(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var animationFpsLimit = 59;
        var debugAnimationSpeed = 9d;
        if (File.Exists(runtimePaths.SharedConfigPath))
        {
            var provider = runtimePaths.OpenSharedConfigProvider();
            if (provider.Exists("UiAniFPS"))
            {
                animationFpsLimit = provider.Get<int>("UiAniFPS");
            }

            if (provider.Exists("SystemDebugAnim"))
            {
                debugAnimationSpeed = provider.Get<int>("SystemDebugAnim");
            }
        }

        ApplyAnimationPreferences(animationFpsLimit, debugAnimationSpeed);
    }

    public static void ApplyAnimationPreferences(int animationFpsLimit, double debugAnimationSpeed)
    {
        MotionDurations.ApplyRuntimePreferences(animationFpsLimit, debugAnimationSpeed);
    }

    public void ApplyAppearance(
        int darkModeIndex,
        int lightColorIndex,
        int darkColorIndex,
        string? lightCustomColorHex,
        string? darkCustomColorHex,
        string? globalFontConfigValue,
        string? motdFontConfigValue)
    {
        FrontendAppearanceService.ApplyAppearance(
            Application.Current,
            new FrontendAppearanceSelection(
                darkModeIndex,
                lightColorIndex,
                darkColorIndex,
                lightCustomColorHex,
                darkCustomColorHex,
                globalFontConfigValue,
                motdFontConfigValue));
    }

    public void AcceptLauncherEula()
    {
        PersistSharedValue("SystemEula", true);
    }

    public void PersistLocalValue<T>(string key, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = RuntimePaths.OpenLocalConfigProvider();
        provider.Set(key, value);
        provider.Sync();
    }

    public void PersistSharedValue<T>(string key, T value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        provider.Set(key, value);
        provider.Sync();
    }

    public void PersistProtectedSharedValue(string key, string value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        provider.Set(key, ProtectSharedValue(value));
        provider.Sync();
    }

    public void RemoveLocalValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = RuntimePaths.OpenLocalConfigProvider();
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void RemoveSharedValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void PersistInstanceValue<T>(string instanceDirectory, string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);
        if (!FrontendRuntimePaths.IsRecognizedInstanceDirectory(instanceDirectory))
        {
            return;
        }

        var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
        provider.Set(key, value);
        provider.Sync();
    }

    public void RemoveInstanceValues(string instanceDirectory, IEnumerable<string> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);
        if (!FrontendRuntimePaths.IsRecognizedInstanceDirectory(instanceDirectory))
        {
            return;
        }

        var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void DisableNonAsciiGamePathWarning()
    {
        PersistSharedValue("HintDisableGamePathCheckTip", true);
    }

    public FrontendDownloadTransferOptions GetDownloadTransferOptions()
    {
        return FrontendDownloadSettingsService.ResolveTransferOptions(RuntimePaths);
    }

    private T ReadLocalValue<T>(string key, T fallback)
    {
        var provider = RuntimePaths.OpenLocalConfigProvider();
        return provider.Exists(key)
            ? provider.Get<T>(key)
            : fallback;
    }

    private T ReadSharedValue<T>(string key, T fallback)
    {
        var provider = RuntimePaths.OpenSharedConfigProvider();
        return provider.Exists(key)
            ? provider.Get<T>(key)
            : fallback;
    }

    private string ProtectSharedValue(string plainText)
    {
        var encryptionKey = ResolveSharedEncryptionKey();
        return LauncherDataProtectionService.Protect(plainText, encryptionKey);
    }

    private FrontendDownloadProvider GetDownloadProvider()
    {
        return FrontendDownloadProvider.FromPreference(ReadSharedValue("ToolDownloadSource", 1));
    }

    private byte[] ResolveSharedEncryptionKey()
    {
        return LauncherSharedEncryptionKeyService.ResolveOrCreate(
            RuntimePaths.SharedConfigDirectory,
            Environment.GetEnvironmentVariable("PCL_ENCRYPTION_KEY"));
    }
}
