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
    private FrontendDeferredSettingsWriter? _deferredSettingsWriter;

    public void FlushPendingSettings() => _deferredSettingsWriter?.Flush();

    public void PersistLocalValueDeferred<T>(string key, T value)
    {
        (_deferredSettingsWriter ??= new()).SetDeferred(RuntimePaths.OpenLocalConfigProvider(), key, value);
    }

    public void PersistSharedValueDeferred<T>(string key, T value)
    {
        (_deferredSettingsWriter ??= new()).SetDeferred(RuntimePaths.OpenSharedConfigProvider(), key, value);
    }

    public void PersistInstanceValueDeferred<T>(string instanceDirectory, string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);
        if (!FrontendRuntimePaths.IsRecognizedInstanceDirectory(instanceDirectory)) return;
        var path = Path.Combine(instanceDirectory, "PCL", "config.v1.yml");
        (_deferredSettingsWriter ??= new()).SetDeferred(path,
            () => FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory), key, value);
    }

    private void SyncSettingsProvider(CommonFileProvider provider)
    {
        if (_deferredSettingsWriter is { } writer) writer.Sync(provider);
        else provider.Sync();
    }

    public static void ApplyStoredAnimationPreferences(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var debugAnimationSpeed = 9d;
        if (File.Exists(runtimePaths.SharedConfigPath))
        {
            var provider = runtimePaths.OpenSharedConfigProvider();
            if (provider.Exists("SystemDebugAnim"))
            {
                debugAnimationSpeed = provider.Get<int>("SystemDebugAnim");
            }
        }

        ApplyAnimationPreferences(debugAnimationSpeed);
    }

    public static void ApplyAnimationPreferences(double debugAnimationSpeed)
    {
        MotionDurations.ApplyRuntimePreferences(debugAnimationSpeed);
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
        SyncSettingsProvider(provider);
    }

    public void PersistSharedValue<T>(string key, T value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        provider.Set(key, value);
        SyncSettingsProvider(provider);
    }

    public void PersistProtectedSharedValue(string key, string value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        provider.Set(key, ProtectSharedValue(value));
        SyncSettingsProvider(provider);
    }

    public void RemoveLocalValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = RuntimePaths.OpenLocalConfigProvider();
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        SyncSettingsProvider(provider);
    }

    public void RemoveSharedValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        SyncSettingsProvider(provider);
    }

    public void PersistInstanceValue<T>(string instanceDirectory, string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);
        if (!FrontendRuntimePaths.IsRecognizedInstanceDirectory(instanceDirectory))
        {
            return;
        }

        if (_deferredSettingsWriter is { } writer)
        {
            writer.UpdateAndSync(Path.Combine(instanceDirectory, "PCL", "config.v1.yml"),
                () => FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory), provider => provider.Set(key, value));
        }
        else
        {
            var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
            provider.Set(key, value);
            provider.Sync();
        }
    }

    public void RemoveInstanceValues(string instanceDirectory, IEnumerable<string> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);
        if (!FrontendRuntimePaths.IsRecognizedInstanceDirectory(instanceDirectory))
        {
            return;
        }

        if (_deferredSettingsWriter is { } writer)
        {
            writer.UpdateAndSync(Path.Combine(instanceDirectory, "PCL", "config.v1.yml"),
                () => FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory), provider =>
                {
                    foreach (var key in keys) provider.Remove(key);
                });
        }
        else
        {
            var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
            foreach (var key in keys) provider.Remove(key);
            provider.Sync();
        }
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
