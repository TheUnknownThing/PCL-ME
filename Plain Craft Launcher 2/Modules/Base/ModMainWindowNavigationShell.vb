Public Module ModMainWindowNavigationShell

    Public Sub HandleTitleSelection(isChangingPage As Boolean, senderTag As Object, pageChangeActual As Action(Of FormMain.PageStackData))
        If isChangingPage Then Return
        pageChangeActual?.Invoke(Val(senderTag))
    End Sub

    Public Sub HandlePageBack(pageStack As List(Of FormMain.PageStackData), pageChangeActual As Action(Of FormMain.PageStackData), pageChangeHome As Action)
        If pageStack Is Nothing Then Throw New ArgumentNullException(NameOf(pageStack))

        If pageStack.Any() Then
            pageChangeActual?.Invoke(pageStack(0))
        Else
            pageChangeHome?.Invoke()
        End If
    End Sub

    Public Sub TriggerPageBack(pageCurrent As FormMain.PageType, pageCurrentSub As FormMain.PageSubType, pageBack As Action)
        If pageCurrent = FormMain.PageType.Download AndAlso pageCurrentSub = FormMain.PageSubType.DownloadInstall AndAlso FrmDownloadInstall.IsInSelectPage Then
            FrmDownloadInstall.ExitSelectPage()
        ElseIf pageCurrent = FormMain.PageType.InstanceSetup AndAlso pageCurrentSub = FormMain.PageSubType.VersionInstall AndAlso FrmInstanceInstall.IsInSelectPage Then
            FrmInstanceInstall.ExitSelectPage()
        Else
            pageBack?.Invoke()
        End If
    End Sub

End Module
