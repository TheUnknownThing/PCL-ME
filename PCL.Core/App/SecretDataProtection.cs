namespace PCL.Core.App;

public static class SecretDataProtection
{
    public static string Protect(string? data)
    {
        return LauncherDataProtectionRuntime.Protect(data);
    }

    public static string Unprotect(string? data)
    {
        return LauncherDataProtectionRuntime.Unprotect(data);
    }
}
