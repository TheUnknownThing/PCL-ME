using System;
using System.IO;
using System.Security;
using Microsoft.Win32;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

internal sealed class WindowsRegistrySystemThemeSource : ISystemThemeSource
{
    private const string ThemeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeKey = "AppsUseLightTheme";

    public bool IsSystemInDarkMode()
    {
        try
        {
            using var registryKey = Registry.CurrentUser.OpenSubKey(ThemeRegistryPath);
            if (registryKey == null)
            {
                LogWrapper.Warn($"注册表键 {ThemeRegistryPath} 不存在");
                return false;
            }

            var value = registryKey.GetValue(AppsUseLightThemeKey) as int?;
            return value == 0;
        }
        catch (Exception ex) when (ex is SecurityException or IOException)
        {
            LogWrapper.Warn(ex, $"无法访问注册表键 {ThemeRegistryPath}");
            return false;
        }
    }
}
