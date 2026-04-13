using System;
using PCL.Core.App;
using PCL.Core.Utils.OS;

namespace PCL.Core.UI.Theme;

public static class SystemThemeHelper {
    /// <summary>
    /// 检查系统是否处于深色模式。
    /// </summary>
    /// <returns>如果系统使用深色模式，则返回 true；否则返回 false（包括注册表不可访问的情况）。</returns>
    public static bool IsSystemInDarkMode() => SystemTheme.IsSystemInDarkMode();

    [Obsolete("Use ThemeService.IsDarkMode instead")]
    public static bool IsDarkMode() => ThemeService.IsDarkMode;
}
