namespace PCL.Core.Utils.OS;

public class SystemTheme {

    /// <summary>
    /// 检查系统是否处于深色模式。
    /// </summary>
    /// <returns>如果系统使用深色模式，则返回 true；否则返回 false（包括注册表不可访问的情况）。</returns>
    public static bool IsSystemInDarkMode() => SystemThemeSourceProvider.Current.IsSystemInDarkMode();
}
