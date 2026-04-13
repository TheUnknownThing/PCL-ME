using System;

namespace PCL.Core.App.Configuration.Storage;

public sealed record ConfigStorageAccessFailureContext(
    ConfigStorage Storage,
    StorageAction Action,
    object? Key,
    object? Value,
    bool HasOutput,
    object? Argument,
    string Message,
    Exception Exception);

public sealed record ConfigStorageSaveFailureContext(
    ConfigStorage Storage,
    string FilePath,
    string Message,
    Exception Exception);

public static class ConfigStorageHooks
{
    public static bool EnableTrace { get; set; }
    public static Func<ConfigStorageAccessFailureContext, bool>? AccessFailureHandler { get; set; }
    public static Action<ConfigStorageSaveFailureContext>? SaveFailureHandler { get; set; }
}
