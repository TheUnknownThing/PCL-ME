using System.IO.Compression;
using System.Text.Json;
using PCL.Extensions.Runtime;

try
{
    if (args.Length == 3 && args[0] == "pack")
    {
        var source = Path.GetFullPath(args[1]);
        var target = Path.GetFullPath(args[2]);
        ExtensionPackage.ReadManifest(source, checkPlatform: false);
        if (target.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Write the archive outside the published extension directory.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            ZipFile.CreateFromDirectory(source, temporary, CompressionLevel.Optimal, includeBaseDirectory: false);
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Console.WriteLine(target);
    }
    else if (args.Length == 2 && args[0] == "validate")
    {
        var directory = args[1];
        string? extracted = null;
        try
        {
            if (File.Exists(directory))
            {
                extracted = Path.Combine(Path.GetTempPath(), "pcl-extension-validate-" + Guid.NewGuid().ToString("N"));
                ExtensionPackage.Extract(directory, extracted, checkPlatform: false);
                directory = extracted;
            }
            var manifest = ExtensionPackage.ReadManifest(directory, checkPlatform: false);
            Console.WriteLine(JsonSerializer.Serialize(manifest, ExtensionPackage.JsonOptions));
        }
        finally { if (extracted is not null && Directory.Exists(extracted)) Directory.Delete(extracted, true); }
    }
    else
    {
        Console.Error.WriteLine("Usage: pack <published-directory> <output.pclext> | validate <directory-or-package>");
        return 1;
    }
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
