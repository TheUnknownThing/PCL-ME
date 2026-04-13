Imports System.Windows
Imports System.Windows.Input

Public Module ModMainWindowInputShell

    Public Sub HandleKeyDown(form As FormMain, e As KeyEventArgs, triggerPageBack As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If e Is Nothing Then Throw New ArgumentNullException(NameOf(e))

        If e.IsRepeat Then Return

        If form.PanMsg.Children.Count > 0 Then
            If e.Key = Key.Enter Then
                CType(form.PanMsg.Children(0), Object).Btn1_Click()
                Return
            ElseIf e.Key = Key.Escape Then
                Dim Msg As Object = form.PanMsg.Children(0)
                If TypeOf Msg IsNot MyMsgInput AndAlso TypeOf Msg IsNot MyMsgSelect AndAlso Msg.Btn3.Visibility = Visibility.Visible Then
                    Msg.Btn3_Click()
                ElseIf Msg.Btn2.Visibility = Visibility.Visible Then
                    Msg.Btn2_Click()
                Else
                    Msg.Btn1_Click()
                End If
                Return
            End If
        End If

        If e.Key = Key.Escape Then triggerPageBack?.Invoke()

        If e.Key = Key.F11 AndAlso form.PageCurrent = FormMain.PageType.InstanceSelect Then
            FrmSelectRight.ShowHidden = Not FrmSelectRight.ShowHidden
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Return
        End If

        If e.Key = Key.F12 Then
            PageSetupUI.HiddenForceShow = Not PageSetupUI.HiddenForceShow
            If PageSetupUI.HiddenForceShow Then
                Hint("功能隐藏设置已暂时关闭！", HintType.Finish)
            Else
                Hint("功能隐藏设置已重新开启！", HintType.Finish)
            End If
            PageSetupUI.HiddenRefresh()
            Return
        End If

        If e.Key = Key.F5 Then
            If TypeOf form.PageLeft Is IRefreshable Then CType(form.PageLeft, IRefreshable).Refresh()
            If TypeOf form.PageRight Is IRefreshable Then CType(form.PageRight, IRefreshable).Refresh()
            Return
        End If

        If e.Key = Key.Enter AndAlso form.PageCurrent = FormMain.PageType.Launch Then
            If IsAprilEnabled AndAlso Not IsAprilGiveup Then
                Hint("木大！")
            Else
                FrmLaunchLeft.LaunchButtonClick()
            End If
        End If

        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then e.Handled = True
    End Sub

    Public Sub HandleMouseDown(form As FormMain, e As MouseButtonEventArgs, triggerPageBack As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If e Is Nothing Then Throw New ArgumentNullException(NameOf(e))

        If form.PanMsg.Children.Count > 0 OrElse WaitingMyMsgBox.Any Then Return
        If e.ChangedButton = MouseButton.XButton1 OrElse e.ChangedButton = MouseButton.XButton2 Then triggerPageBack?.Invoke()
    End Sub

End Module
