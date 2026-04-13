using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO;

internal interface IDirectoryPermissionService
{
    Task<bool> HasWriteAccessAsync(string path, CancellationToken cancellationToken);
}
