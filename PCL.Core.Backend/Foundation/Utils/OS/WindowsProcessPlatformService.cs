using System;
using System.Diagnostics;
using System.Management;
using System.Security;
using System.Security.Principal;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

[SupportedOSPlatform("windows")]
internal sealed class WindowsProcessPlatformService : IProcessPlatformService
{
    public bool IsAdmin() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public string? GetCommandLine(int processId)
    {
        var query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}";
        using var searcher = new ManagementObjectSearcher(query);
        using var results = searcher.Get();
        foreach (var result in results)
        {
            return result["CommandLine"]?.ToString();
        }

        return null;
    }

    public Process? StartAsAdmin(string path, string? arguments)
    {
        var psi = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        if (arguments != null)
        {
            psi.Arguments = arguments;
        }

        return Process.Start(psi);
    }

    public void SetGpuPreference(string executable, bool wantHighPerformance)
    {
        const string gpuPreferenceRegKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string gpuPreferenceRegValueHigh = "GpuPreference=2;";
        const string gpuPreferenceRegValueDefault = "GpuPreference=0;";

        try
        {
            var isCurrentHighPerformance = GetCurrentGpuPreference(
                executable,
                gpuPreferenceRegKey,
                gpuPreferenceRegValueHigh);

            LogWrapper.Info("System", $"Current GPU preference for {executable} is high performance: {isCurrentHighPerformance}");

            if (isCurrentHighPerformance == wantHighPerformance)
            {
                LogWrapper.Info("System", $"GPU preference for {executable} already matches the requested state.");
                return;
            }

            SetGpuPreferenceValue(
                executable,
                wantHighPerformance,
                gpuPreferenceRegKey,
                gpuPreferenceRegValueHigh,
                gpuPreferenceRegValueDefault);
        }
        catch (UnauthorizedAccessException ex)
        {
            const string errorMessage = "Insufficient permission to access the registry. Run the program as administrator or check user permissions.";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new UnauthorizedAccessException(errorMessage, ex);
        }
        catch (SecurityException ex)
        {
            const string errorMessage = "Security policy does not allow registry access. Contact the system administrator to review security settings.";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new SecurityException(errorMessage, ex);
        }
        catch (Exception ex)
        {
            var errorMessage = $"An unexpected error occurred while setting GPU preference: {ex.Message}";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    private static bool GetCurrentGpuPreference(string executable, string regKey, string highPerfValue)
    {
        try
        {
            using var readOnlyKey = Registry.CurrentUser.OpenSubKey(regKey, false);
            if (readOnlyKey == null)
            {
                LogWrapper.Info("System", "GPU preference registry key does not exist and will be created if needed.");
                return false;
            }

            var currentValue = readOnlyKey.GetValue(executable)?.ToString();
            return string.Equals(currentValue, highPerfValue, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "System", $"An error occurred while reading the current GPU preference: {ex.Message}");
            return false;
        }
    }

    private static void SetGpuPreferenceValue(
        string executable,
        bool wantHighPerformance,
        string regKey,
        string highPerfValue,
        string defaultValue)
    {
        RegistryKey? writeKey = null;
        try
        {
            writeKey = Registry.CurrentUser.OpenSubKey(regKey, true);
            if (writeKey == null)
            {
                LogWrapper.Info("System", "Creating GPU preference registry key.");
                writeKey = Registry.CurrentUser.CreateSubKey(regKey);

                if (writeKey == null)
                {
                    throw new InvalidOperationException($"Unable to create registry key: {regKey}");
                }
            }

            var valueToSet = wantHighPerformance ? highPerfValue : defaultValue;
            writeKey.SetValue(executable, valueToSet, RegistryValueKind.String);
            LogWrapper.Info("System", $"Successfully set GPU preference for {executable}: {(wantHighPerformance ? "high performance" : "default")}");
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var errorMessage = $"An error occurred while writing to the registry: {ex.Message}";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }
        finally
        {
            writeKey?.Dispose();
        }
    }
}
