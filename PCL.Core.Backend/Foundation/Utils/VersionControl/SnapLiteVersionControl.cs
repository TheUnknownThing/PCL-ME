using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LiteDB;
using PCL.Core.IO.Storage;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Utils.VersionControl;

/*
一个基于文件结构只记录差异文件的文件版本控制系统，用于 MC 存档备份
自己乱搓的，效率肯定没有 Git 高（
*/

public class SnapLiteVersionControl : IVersionControl , IDisposable
{
    private readonly string _rootPath;
    private readonly LiteDatabase _database;
    private static readonly IHashProvider _HashProvider = SHA512Provider.Instance;
    private readonly HashStorage _storage;

    private const string ConfigFolderName = ".litesnap";
    private const string DatabaseName = "index.db";
    private const string DatabaseIndexTableName = "index";
    private const string ObjectsFolderName = "objects";
    //private const string TempFolderName = "temp";
    
    public SnapLiteVersionControl(string rootPath)
    {
        try
        {
            _rootPath = rootPath;
            var dbFile = Path.Combine(_rootPath, ConfigFolderName, DatabaseName);
            var objFolder = Path.Combine(_rootPath, ConfigFolderName, ObjectsFolderName);
            if (!Directory.Exists(objFolder))
                Directory.CreateDirectory(objFolder);
            _storage = new HashStorage(objFolder, _HashProvider, true);
            _database = new LiteDatabase($"Filename={dbFile}");
        }
        catch (Exception e)
        {
            LogWrapper.Error(e,$"[SnapLite] Failed to load SnapLite data at {_rootPath}");
            throw;
        }

    }

    private async Task<FileVersionObjects[]> _GetAllTrackedObjectsAsync()
    {
        List<FileVersionObjects> scannedPaths = [];
        Queue<string> scanQueue = new();
        scanQueue.Enqueue(_rootPath);
        string[] excludePath = [Path.Combine(_rootPath, ConfigFolderName)];
        while (scanQueue.Count != 0) // 找出所有文件和文件夹
        {
            var curDir = new DirectoryInfo(scanQueue.Dequeue());
            var filesInCurDir = curDir.EnumerateFiles().ToArray();
            var dirsInCurDir = curDir.EnumerateDirectories().ToArray();
            if (filesInCurDir.Length == 0 && dirsInCurDir.Length == 0) // 空文件夹直接加入
            {
                scannedPaths.Add(new FileVersionObjects()
                {
                    CreationTime = curDir.CreationTime,
                    Hash = string.Empty,
                    LastWriteTime = curDir.LastWriteTime,
                    Length = 0,
                    ObjectType = ObjectType.Directory,
                    Path = curDir.FullName.Replace(_rootPath, string.Empty).TrimStart(Path.DirectorySeparatorChar)
                });
                continue;
            }

            // 计算文件
            var fileComputesResult = await filesInCurDir.SelectAsync(async file =>
            {
                using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                return new FileVersionObjects()
                {
                    CreationTime = file.CreationTime,
                    Hash = (await _HashProvider.ComputeHashAsync(fs).ConfigureAwait(false)).ToHexString(),
                    LastWriteTime = file.LastWriteTime,
                    Length = fs.Length,
                    Path = file.FullName.Replace(_rootPath, string.Empty).TrimStart(Path.DirectorySeparatorChar),
                    ObjectType = ObjectType.File
                };
            }, 15).ConfigureAwait(false);

            scannedPaths.AddRange(fileComputesResult);

            // 剩余文件夹加入搜索队列中
            foreach (var directory in dirsInCurDir)
                if (!excludePath.Contains(directory.FullName))
                    scanQueue.Enqueue(directory.FullName);
        }
        return scannedPaths.ToArray();
    }

    public async Task<string> CreateNewVersion(string? name = null, string? desc = null)
    {
        try
        {
            var nodeId = Guid.NewGuid().ToString("N");

            // 获取当前的文件信息
            var allFiles = await _GetAllTrackedObjectsAsync().ConfigureAwait(false);
            LogWrapper.Info($"[SnapLite] Loaded all tracked files, total count: {allFiles.Length}");
            var newAddFiles = allFiles
                .Distinct(FileVersionObjectsComparer.Instance)
                .Where(x => x.ObjectType.Equals(ObjectType.Directory) ||
                            (x.ObjectType.Equals(ObjectType.File) && !_storage.Exists(x.Hash)))
                .ToList();
            // 存入数据库中
            LogWrapper.Info($"[SnapLite] Total new objects: {newAddFiles.Count}");
            var nodeObjects = _database.GetCollection<FileVersionObjects>(_GetNodeTableNameById(nodeId));
            nodeObjects.InsertBulk(allFiles);
            LogWrapper.Info($"[SnapLite] Records have been written to the database; starting file storage.");
            // 复制到 objects 文件夹中
            await newAddFiles
                .Where(x => x.ObjectType == ObjectType.File)
                .ForEachAsync(x => Task.Run(async () =>
                {
                    var filePath = Path.Combine(_rootPath, x.Path);
                    try
                    {
                        LogWrapper.Info($"[SnapLite] Storing {filePath} in the hash store");
                        await _storage.PutAsync(filePath, x.Hash).ConfigureAwait(false);
                        LogWrapper.Info($"[SnapLite] Finished storing {filePath}");
                    }
                    catch (Exception e)
                    {
                        LogWrapper.Error(e, $"[SnapLite] An error occurred while storing {filePath}");
                        throw;
                    }
                }), 12).ConfigureAwait(false);

            LogWrapper.Info($"[SnapLite] File storage task completed");
            // 创建最终记录
            var nodeList = _database.GetCollection<VersionData>(DatabaseIndexTableName);
            var currentNodeInfo = new VersionData()
            {
                Created = DateTime.Now,
                Desc = desc ?? "Backup made by SnapLite",
                Name = name ?? $"{DateTime.Now:yyyy/dd/MM-HH:mm:ss}",
                NodeId = nodeId,
                Version = 1
            };
            nodeList.Insert(currentNodeInfo);
            LogWrapper.Info($"[SnapLite] Database record update completed");
            return nodeId;
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, $"[SnapLite] Failed to create snapshot");
            throw;
        }
    }

    private static string _GetNodeTableNameById(string nodeId)
    {
        return $"node_{nodeId}";
    }

    public VersionData? GetVersion(string nodeId)
    {
        var nodeList = _database.GetCollection<VersionData>(DatabaseIndexTableName);
        return nodeList.FindOne(x => x.NodeId == nodeId);
    }

    public List<VersionData> GetVersions()
    {
        var nodeList = _database.GetCollection<VersionData>(DatabaseIndexTableName);
        return nodeList.Query().ToList();
    }

    public List<FileVersionObjects>? GetNodeObjects(string nodeId)
    {
        var objectList = _database.GetCollection<FileVersionObjects>(_GetNodeTableNameById(nodeId));
        return objectList?.Query().ToList();
    }

    public void DeleteVersion(string nodeId)
    {
        try
        {
            var nodeList = _database.GetCollection<VersionData>(DatabaseIndexTableName);
            nodeList.DeleteMany(x => x.NodeId == nodeId);
            _database.DropCollection(_GetNodeTableNameById(nodeId));
        }
        catch (Exception e)
        {
            LogWrapper.Error(e,$"[SnapLite] An error occurred while deleting {nodeId}");
            throw;
        }
    }

    public Stream? GetObjectContent(string objectId)
    {
        try
        {
            return _storage.Get(objectId);
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, $"[SnapLite] Failed to get a stream for {objectId}");
            throw;
        }
    }

    public async Task ApplyPastVersion(string nodeId)
    {
        LogWrapper.Info($"[SnapLite] Starting to apply snapshot data for {nodeId}");
        var applyObjects = GetNodeObjects(nodeId) ?? throw new NullReferenceException("Unable to load snapshot records.");
        var currentObjects = await _GetAllTrackedObjectsAsync().ConfigureAwait(false);
        LogWrapper.Info($"[SnapLite] Snapshot object count for {nodeId}: {applyObjects.Count}, current folder object count: {currentObjects.Length}");
        var curDict = currentObjects.ToDictionary(x => x.Path);

        List<FileVersionObjects> toDelete = [];
        List<FileVersionObjects> toAdd = [];
        List<FileVersionObjects> toUpdate = [];
        foreach (var applyObject in applyObjects)
        {
            if (curDict.TryGetValue(applyObject.Path, out var existingObject))
            {
                var isSameContent = existingObject.ObjectType == applyObject.ObjectType
                                    && existingObject.Length == applyObject.Length
                                    && existingObject.Hash == applyObject.Hash;
                var isSameMetadata = existingObject.CreationTime == applyObject.CreationTime
                                     && existingObject.LastWriteTime == applyObject.LastWriteTime;
                if (!isSameContent && !isSameMetadata) toAdd.Add(applyObject);
                if (isSameContent && !isSameMetadata) toUpdate.Add(applyObject);
            }
            else
            {
                toAdd.Add(applyObject);
            }
        }

        toDelete.AddRange(from currentObject in currentObjects
            let existsInApply = applyObjects.Any(x => x.Path == currentObject.Path)
            where !existsInApply
            select currentObject);
        LogWrapper.Info($"[SnapLite] Planned changes - delete: {toDelete.Count}, add: {toAdd.Count}, metadata updates: {toUpdate.Count}");

        // 先删除文件深度高的文件和文件夹 并且 遵循先删除文件 后 删除文件夹
        await toDelete
            .OrderByDescending(x => x.Path.Count(c => c == Path.DirectorySeparatorChar))
            .ThenBy(x => (int)(x.ObjectType))
            .ForEachAsync(deleteFile => Task.Run(() =>
        {
            try
            {
                if (deleteFile.ObjectType == ObjectType.File)
                {
                    var curFile = new FileInfo(Path.Combine(_rootPath, deleteFile.Path));
                    if (curFile.Exists) curFile.Delete();
                }
                else if (deleteFile.ObjectType == ObjectType.Directory)
                {
                    var curDir = new DirectoryInfo(Path.Combine(_rootPath, deleteFile.Path));
                    try
                    {
                        if (curDir.Exists) curDir.Delete(true); // 文件夹内有不属于这个版本的东西
                    }
                    catch (IOException e)
                    {
                        LogWrapper.Warn(e, "SnapLite", $"Directory {curDir} may have been modified externally; deletion failed because it contains content not tracked by this snapshot.");
                    }
                }
            }
            catch (Exception e)
            {
                LogWrapper.Error(e, $"[SnapLite] An error occurred while deleting {deleteFile.Path}; type: {deleteFile.ObjectType}, SHA512: {deleteFile.Hash}, size: {deleteFile.Length}");
                throw;
            }
        }), 25).ConfigureAwait(false);

        LogWrapper.Info($"[SnapLite] File deletion completed");
        
        // 先应用文件夹，再应用文件
        await toAdd
            .OrderByDescending(x => (int)(x.ObjectType))
            .ForEachAsync(addFile => Task.Run(async () =>
            {
                try
                {
                    switch (addFile.ObjectType)
                    {
                        case ObjectType.File:
                        {
                            var curFilePath = Path.Combine(_rootPath, addFile.Path);
                            var fileFolder = Path.GetDirectoryName(curFilePath);
                            if (fileFolder is null) throw new NullReferenceException($"Unable to get directory information for {curFilePath}.");
                            if (!Directory.Exists(fileFolder)) Directory.CreateDirectory(fileFolder);
                            var curFile = new FileInfo(curFilePath);
                            if (curFile.Exists) curFile.Delete();
                            using var ctx = GetObjectContent(addFile.Hash) ??
                                            throw new NullReferenceException("Failed to load stored file information.");
                            using (var fs = curFile.Create())
                            {
                                await ctx.CopyToAsync(fs).ConfigureAwait(false);
                            }

                            curFile.CreationTime = addFile.CreationTime;
                            curFile.LastWriteTime = addFile.LastWriteTime;
                            break;
                        }
                        case ObjectType.Directory:
                        {
                            var curDir = new DirectoryInfo(Path.Combine(_rootPath, addFile.Path));
                            if (!curDir.Exists) curDir.Create();
                            curDir.CreationTime = addFile.CreationTime;
                            curDir.LastWriteTime = addFile.LastWriteTime;
                            break;
                        }
                        default:
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
                catch (Exception e)
                {
                    LogWrapper.Error(e,
                        $"[SnapLite] An error occurred while adding or updating {addFile.Path}; type: {addFile.ObjectType}, SHA512: {addFile.Hash}, size: {addFile.Length}");
                    throw;
                }

            }), 12).ConfigureAwait(false);

        LogWrapper.Info($"[SnapLite] File addition completed");

        await toUpdate
            .ForEachAsync(updateObject => Task.Run(() =>
        {
            try
            {
                switch (updateObject.ObjectType)
                {
                    case ObjectType.File:
                    {
                        var curFile = new FileInfo(Path.Combine(_rootPath, updateObject.Path));
                        if (!curFile.Exists)
                        {
                            LogWrapper.Warn($"[SnapLite] File to update does not exist: {updateObject.Path}");
                            return;
                        }
                        curFile.LastWriteTime = updateObject.LastWriteTime;
                        curFile.CreationTime = updateObject.CreationTime;
                        break;
                    }
                    case ObjectType.Directory:
                    {
                        var curDir = new DirectoryInfo(Path.Combine(_rootPath, updateObject.Path));
                        if (!curDir.Exists)
                        {
                            LogWrapper.Warn($"[SnapLite] Folder to update does not exist: {updateObject.Path}");
                            return;
                        }
                        curDir.LastWriteTime = updateObject.LastWriteTime;
                        curDir.CreationTime = updateObject.CreationTime;
                        break;
                    }
                    default:
                    {
                        throw new NotSupportedException();
                    }
                }
            }
            catch (Exception e)
            {
                LogWrapper.Error(e, $"[SnapLite] An error occurred while updating file metadata");
                throw;
            }
        }), 20).ConfigureAwait(false);
        LogWrapper.Info("[SnapLite] File metadata update completed");
    }

    public async Task<bool> CheckVersion(string nodeId, bool deepCheck = false)
    {
        var fileObjects = GetNodeObjects(nodeId)?.Distinct(FileVersionObjectsComparer.Instance);
        if (fileObjects is null) return false;

        var checkTasks = fileObjects
            .SelectAsync(async x =>
        {
            var filePath = Path.Combine(_rootPath, ConfigFolderName, ObjectsFolderName, x.Hash);
            if (!deepCheck) return File.Exists(filePath);

            if (!File.Exists(filePath)) return false;
            using var ctx = GetObjectContent(x.Hash);
            if (ctx != null) return (await _HashProvider.ComputeHashAsync(ctx).ConfigureAwait(false)).ToHexString() == x.Hash;
            LogWrapper.Warn($"[SnapLite] Unable to open the file stream for object {x.Hash}");
            return false;
        }, deepCheck ? 12 : 25);
        return (await checkTasks.ConfigureAwait(false)).ToArray().All(x => x);
    }

    public async Task CleanUnrecordObjects()
    {
        var nodeList = _database.GetCollection<VersionData>(DatabaseIndexTableName).Query().ToArray();

        // 获取在记录的所有 objects
        List<string> objectsInRecord = [];
        foreach (var node in nodeList)
        {
            var nodeTable = _GetNodeTableNameById(node.NodeId);
            objectsInRecord.AddRange(_database.GetCollection<FileVersionObjects>(nodeTable).Query().ToEnumerable().Select(x => x.Hash));
        }
        objectsInRecord = [..objectsInRecord.Distinct()];
        
        // 获取目前存档的 objects
        var allObjects = Directory.EnumerateFiles(Path.Combine(_rootPath, ConfigFolderName, ObjectsFolderName))
            // ReSharper disable once ConvertClosureToMethodGroup // method group 写法会炸掉巨硬的史山 null check
            .Select(path => Path.GetFileName(path))
            .Where(x => !string.IsNullOrEmpty(x));

        // 计算出不需要继续存储的 objects
        var uselessObjects = allObjects.Except(objectsInRecord).ToArray();
        LogWrapper.Info($"[SnapLite] Found {uselessObjects.Length} objects that can be cleaned up");

        var deleteTask = uselessObjects.ForEachAsync(x => Task.Run(async () =>
        {
            try
            {
                await _storage.DeleteAsync(x).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                LogWrapper.Error(e, $"[SnapLite] Failed to delete file {x}");
                throw;
            }
        }), 20);
        await deleteTask.ConfigureAwait(false);
    }

    public async Task Export(string nodeId, string saveFilePath)
    {
        var fileObjects = GetNodeObjects(nodeId) ?? throw new NullReferenceException("Failed to load snapshot records.");
        if (File.Exists(saveFilePath))
            File.Delete(saveFilePath);
        await using var fs = new FileStream(saveFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        using var targetZip = new ZipArchive(fs, ZipArchiveMode.Update);
        fileObjects = fileObjects
            .OrderByDescending(x => (int)x.ObjectType) // 先文件夹后文件
            .ToList();
        foreach (var fileObject in fileObjects)
        {
            switch (fileObject.ObjectType)
            {
                case ObjectType.File:
                {
                    var entry = targetZip.CreateEntry(fileObject.Path);
                    entry.LastWriteTime = fileObject.LastWriteTime;
                    await using var writer = entry.Open();
                    await using var reader = GetObjectContent(fileObject.Hash) ?? throw new Exception("Unable to find the stored file.");
                    await reader.CopyToAsync(writer).ConfigureAwait(false);
                    break;
                }
                case ObjectType.Directory:
                {
                    var entry = targetZip.CreateEntry($"{fileObject.Path}{Path.DirectorySeparatorChar}");
                    entry.LastWriteTime = fileObject.LastWriteTime;
                    break;
                }
                default:
                {
                    throw new NotSupportedException();
                }
            }
        }
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _database.Dispose();
        GC.SuppressFinalize(this);
    }
}
