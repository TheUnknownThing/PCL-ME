Imports System.Windows
Imports PCL.Core.Logging

Public Module ModMainWindowExtraButtonShell

    Public Sub HandleUpdateRestart()
        UpdateRestart(True, True)
    End Sub

    Public Function ShouldShowUpdateRestart() As Boolean
        Return IsUpdateWaitingRestart
    End Function

    Public Sub HandleMusicPause()
        MusicControlPause()
    End Sub

    Public Sub HandleMusicNext()
        MusicControlNext()
    End Sub

    Public Function ShouldShowDownloadButton(pageCurrent As FormMain.PageType) As Boolean
        Return HasDownloadingTask() AndAlso pageCurrent <> FormMain.PageType.TaskManager
    End Function

    Public Sub ApplyAprilGiveup(refreshButton As Action)
        If Not IsAprilEnabled OrElse IsAprilGiveup Then Return

        Hint("=D", HintType.Finish)
        IsAprilGiveup = True
        FrmLaunchLeft.AprilScaleTrans.ScaleX = 1
        FrmLaunchLeft.AprilScaleTrans.ScaleY = 1
        refreshButton?.Invoke()
    End Sub

    Public Function ShouldShowAprilButton(pageCurrent As FormMain.PageType) As Boolean
        Return IsAprilEnabled AndAlso Not IsAprilGiveup AndAlso pageCurrent = FormMain.PageType.Launch
    End Function

    Public Sub ShutdownRunningMinecraft()
        Try
            If McLaunchLoaderReal IsNot Nothing Then McLaunchLoaderReal.Abort()
            For Each Watcher In McWatcherList
                Watcher.Kill()
            Next
            Hint("已关闭运行中的 Minecraft！", HintType.Finish)
        Catch ex As Exception
            Log(ex, "强制关闭所有 Minecraft 失败", LogLevel.Feedback)
        End Try
    End Sub

    Public Function ShouldShowShutdownButton() As Boolean
        Return HasRunningMinecraft
    End Function

    Public Function ShouldShowLogButton(pageCurrent As FormMain.PageType) As Boolean
        If FrmLogLeft Is Nothing OrElse FrmLogRight Is Nothing OrElse pageCurrent = FormMain.PageType.GameLog Then Return False
        Return FrmLogLeft.ShownLogs.Count > 0
    End Function

    Public Sub BackToTop(form As FormMain)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))

        Dim realScroll = GetBackToTopScroll(form)
        If realScroll IsNot Nothing Then
            realScroll.PerformVerticalOffsetDelta(-realScroll.VerticalOffset)
        Else
            Log("[UI] 无法返回顶部，未找到合适的 RealScroll", LogLevel.Hint)
        End If
    End Sub

    Public Function ShouldShowBackToTop(form As FormMain, buttonShown As Boolean) As Boolean
        Dim realScroll = GetBackToTopScroll(form)
        Return realScroll IsNot Nothing AndAlso realScroll.Visibility = Visibility.Visible AndAlso realScroll.VerticalOffset > form.Height + If(buttonShown, 0, 700)
    End Function

    Public Function GetBackToTopScroll(form As FormMain) As MyScrollViewer
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If form.PanMainRight.Child Is Nothing OrElse TypeOf form.PanMainRight.Child IsNot MyPageRight Then Return Nothing
        Return CType(form.PanMainRight.Child, MyPageRight).PanScroll
    End Function

End Module
