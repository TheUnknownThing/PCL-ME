using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace PCL.Core.Utils.OS;

internal sealed partial class WindowsRegistryChangeMonitor : IRegistryChangeMonitor
{
    private const int REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;
    private const int KEY_NOTIFY = 0x0010;
    private const int KEY_QUERY_VALUE = 0x0001;
    private const int KEY_READ = KEY_QUERY_VALUE | KEY_NOTIFY;
    private const UIntPtr HKEY_CURRENT_USER = 0x80000001;

    [LibraryImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegOpenKeyEx(UIntPtr hKey, string subKey, uint options, int samDesired, out IntPtr phkResult);

    [LibraryImport("advapi32.dll", EntryPoint = "RegNotifyChangeKeyValue", SetLastError = true)]
    private static partial int RegNotifyChangeKeyValue(IntPtr hKey, [MarshalAs(UnmanagedType.Bool)] bool bWatchSubtree, int dwNotifyFilter, IntPtr hEvent, [MarshalAs(UnmanagedType.Bool)] bool fAsynchronous);

    [LibraryImport("advapi32.dll", EntryPoint = "RegCloseKey", SetLastError = true)]
    private static partial int RegCloseKey(IntPtr hKey);

    private readonly IntPtr _hKey;
    private readonly ManualResetEvent _stopEvent = new(false);
    private readonly ManualResetEvent _registryEvent = new(false);
    private readonly Thread _monitorThread;

    public event EventHandler? Changed;

    public WindowsRegistryChangeMonitor(string keyPath)
    {
        var result = RegOpenKeyEx(HKEY_CURRENT_USER, keyPath, 0, KEY_READ, out _hKey);
        if (result != 0) throw new Win32Exception(result);

        _monitorThread = new Thread(MonitorThread) { IsBackground = true };
        _monitorThread.Start();
    }

    private void MonitorThread()
    {
        try
        {
            RegisterForNotification();

            while (!_stopEvent.WaitOne(0))
            {
                var index = WaitHandle.WaitAny([_registryEvent, _stopEvent], TimeSpan.FromSeconds(1));
                if (index == 1) break;

                if (index == 0)
                {
                    _registryEvent.Reset();
                    Changed?.Invoke(this, EventArgs.Empty);
                    RegisterForNotification();
                }
            }
        }
        finally
        {
            _registryEvent.Dispose();
        }
    }

    private void RegisterForNotification()
    {
        var result = RegNotifyChangeKeyValue(
            _hKey,
            true,
            REG_NOTIFY_CHANGE_LAST_SET,
            _registryEvent.SafeWaitHandle.DangerousGetHandle(),
            true);

        if (result != 0)
        {
            _stopEvent.Set();
            throw new Win32Exception(result);
        }
    }

    public void Dispose()
    {
        _stopEvent.Set();

        if (_monitorThread is { IsAlive: true })
            _monitorThread.Join(1000);

        if (_hKey != IntPtr.Zero)
            _ = RegCloseKey(_hKey);

        _stopEvent.Dispose();
        GC.SuppressFinalize(this);
    }
}
