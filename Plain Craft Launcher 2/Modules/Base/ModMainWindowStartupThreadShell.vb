Imports System.Threading
Imports PCL.Core.App.Essentials

Public Module ModMainWindowStartupThreadShell

    Public Sub StartConsentPromptThread(consent As LauncherStartupConsentResult, onExitLauncher As Action)
        If consent Is Nothing Then Throw New ArgumentNullException(NameOf(consent))

        RunInNewThread(
            Sub()
                Try
                    For Each prompt In consent.Prompts
                        If Not ModStartupPromptShell.RunStartupPrompt(prompt, onExitLauncher) Then Exit For
                    Next
                Catch ex As Exception
                    Log(ex, "初始弹窗提示运行失败", LogLevel.Feedback)
                End Try
            End Sub, "Start MsgBox", ThreadPriority.Lowest)
    End Sub

    Public Sub StartLoaderInitializationThread(onApplyMilestone As Action, onClearTaskTemp As Action)
        RunInNewThread(
            Sub()
                Try
                    DlClientListMojangLoader.Start(1) 'PCL 会同时根据这里的加载结果决定是否使用官方源进行下载
                    onApplyMilestone?.Invoke()
                    ServerLoader.Start(1)
                    If onClearTaskTemp IsNot Nothing Then
                        RunInNewThread(Sub() onClearTaskTemp(), "TryClearTaskTemp", ThreadPriority.BelowNormal)
                    End If
                Catch ex As Exception
                    Log(ex, "初始化加载池运行失败", LogLevel.Feedback)
                End Try
            End Sub, "Start Loader", ThreadPriority.BelowNormal)
    End Sub

End Module
