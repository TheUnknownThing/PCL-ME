Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchResultShell

    Public Sub ShowCompletionNotification(instanceName As String,
                                          outcome As MinecraftLaunchOutcome,
                                          isScriptExport As Boolean,
                                          abortHint As String)
        Dim notification = MinecraftLaunchShellService.GetCompletionNotification(
            New MinecraftLaunchCompletionRequest(
                instanceName,
                outcome,
                isScriptExport,
                abortHint))
        Select Case notification.Kind
            Case MinecraftLaunchNotificationKind.Info
                Hint(notification.Message, HintType.Info)
            Case MinecraftLaunchNotificationKind.Finish
                Hint(notification.Message, HintType.Finish)
        End Select
    End Sub

    Public Sub ShowFailureMessage(message As String, isScriptExport As Boolean)
        If String.IsNullOrWhiteSpace(message) Then Return

        Dim failureDisplay = MinecraftLaunchShellService.GetFailureDisplay(isScriptExport)
        MyMsgBox(message, failureDisplay.DialogTitle)
    End Sub

    Public Sub LogUnhandledFailure(ex As Exception, isScriptExport As Boolean, logError As Action(Of String))
        If ex Is Nothing Then Throw New ArgumentNullException(NameOf(ex))

        logError?.Invoke("错误：" & ex.ToString())
        Dim failureDisplay = MinecraftLaunchShellService.GetFailureDisplay(isScriptExport)
        Log(ex,
            failureDisplay.LogTitle, LogLevel.Msgbox,
            failureDisplay.DialogTitle)
    End Sub

End Module
