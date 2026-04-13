using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal static class FrontendLoggingBootstrap
{
    private static readonly object SyncRoot = new();
    private static Logger? _logger;
    private static bool _isInitialized;

    public static void Initialize(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        lock (SyncRoot)
        {
            if (_isInitialized)
            {
                return;
            }

            var logDirectory = Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log");
            _logger = new Logger(new LoggerConfiguration(logDirectory, FileNameFormat: "PCL-{0}"));
            LogWrapper.AttachLogger(_logger);
            LogWrapper.OnLog += OnLog;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _isInitialized = true;
        }

        LogWrapper.Info("Frontend", $"Logger initialized at {Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log")}");
    }

    public static async ValueTask DisposeAsync()
    {
        Logger? loggerToDispose;

        lock (SyncRoot)
        {
            if (!_isInitialized)
            {
                return;
            }

            loggerToDispose = _logger;
            _logger = null;
            _isInitialized = false;
            LogWrapper.OnLog -= OnLog;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        }

        if (loggerToDispose is null)
        {
            return;
        }

        loggerToDispose.Log($"[{DateTime.Now:HH:mm:ss.fff}] [INFO] [#{Environment.CurrentManagedThreadId}] [Frontend] Logger shutting down");
        await loggerToDispose.DisposeAsync().ConfigureAwait(false);
    }

    public static void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static void OnLog(LogLevel level, string message, string? module, Exception? exception)
    {
        var logger = _logger;
        if (logger is null)
        {
            return;
        }

        var threadName = Thread.CurrentThread.Name ?? $"#{Environment.CurrentManagedThreadId}";
        var modulePrefix = string.IsNullOrWhiteSpace(module) ? string.Empty : $"[{module}] ";
        var formatted = $"[{DateTime.Now:HH:mm:ss.fff}] [{level.PrintName()}] [{threadName}] {modulePrefix}{message}";
        if (exception is not null)
        {
            formatted = $"{formatted}\n{exception}";
        }

        logger.Log(formatted);
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
        {
            LogWrapper.Fatal(exception, "Frontend", $"Unhandled exception (terminating={args.IsTerminating})");
            return;
        }

        LogWrapper.Fatal("Frontend", $"Unhandled exception object: {args.ExceptionObject}");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        LogWrapper.Error(args.Exception, "Frontend", "Unobserved task exception");
        args.SetObserved();
    }
}
