using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO;

internal sealed class PortableDirectoryPermissionService : IDirectoryPermissionService
{
    public async Task<bool> HasWriteAccessAsync(string path, CancellationToken cancellationToken)
    {
        var probeFileName = $".pcl-permission-check-{Guid.NewGuid():N}.tmp";
        var probePath = Path.Combine(path, probeFileName);

        try
        {
            await using (var stream = new FileStream(
                             probePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1,
                             FileOptions.DeleteOnClose | FileOptions.Asynchronous))
            {
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }
}
