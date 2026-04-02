using System;
using System.Threading;
using PCL.Core.App.IoC;
using PCL.Core.UI;

namespace PCL.Core.App.Configuration.Storage;

internal static class ConfigStorageRuntimeHooks
{
    private static int _isInstalled;

    public static void Install()
    {
        if (Interlocked.Exchange(ref _isInstalled, 1) == 1) return;

        ConfigStorageHooks.EnableTrace = Array.Exists(Basics.CommandLineArguments, static arg => arg == "--trace-traffic");
        ConfigStorageHooks.AccessFailureHandler = failure =>
        {
            Lifecycle.ForceShutdown(-2);
            return true;
        };
        ConfigStorageHooks.SaveFailureHandler = failure =>
        {
            var hint = "保存配置文件时出现问题，若该问题能够稳定复现，请尽快提交反馈。" +
                       $"\n\n错误信息:\n{failure.Exception.GetType().FullName}: {failure.Exception.Message}";
            MsgBoxWrapper.Show(hint, "配置文件保存失败", MsgBoxTheme.Error);
        };
    }
}
