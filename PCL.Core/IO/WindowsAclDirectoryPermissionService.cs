using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO;

internal sealed class WindowsAclDirectoryPermissionService : IDirectoryPermissionService
{
    public async Task<bool> HasWriteAccessAsync(string path, CancellationToken cancellationToken)
    {
        var directoryInfo = new DirectoryInfo(path);
        var security = await Task.Run(() => directoryInfo.GetAccessControl(), cancellationToken).ConfigureAwait(false);
        var rules = security.GetAccessRules(true, true, typeof(NTAccount));

        var currentUser = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(currentUser);

        var isDenied = false;
        var isAllowed = false;

        foreach (FileSystemAccessRule rule in rules)
        {
            if (!rule.FileSystemRights.HasFlag(FileSystemRights.Write))
                continue;

            if (!principal.IsInRole(rule.IdentityReference.Value))
                continue;

            if (rule.AccessControlType == AccessControlType.Deny)
            {
                isDenied = true;
                break;
            }

            if (rule.AccessControlType == AccessControlType.Allow)
            {
                isAllowed = true;
            }
        }

        if (isDenied || !isAllowed)
        {
            return false;
        }

        await Task.Run(() => Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Any(), cancellationToken).ConfigureAwait(false);
        return true;
    }
}
