using System;

namespace PCL.Core.IO;

internal static class DirectoryPermissionServiceProvider
{
    public static IDirectoryPermissionService Current { get; } =
        OperatingSystem.IsWindows() ? new WindowsAclDirectoryPermissionService() : new PortableDirectoryPermissionService();
}
