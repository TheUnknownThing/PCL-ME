using System;

namespace PCL.Core.Logging;

public static class LogRuntimeHooks
{
    public static Action<string, string>? FatalDialogPresenter { get; set; }

    public static void ShowFatalDialog(string message, string caption)
    {
        if (FatalDialogPresenter is not null)
        {
            FatalDialogPresenter(message, caption);
            return;
        }

        Console.Error.WriteLine(caption);
        Console.Error.WriteLine(message);
    }
}
