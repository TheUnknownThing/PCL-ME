Imports System.Runtime.InteropServices
Imports System.Windows
Imports PCL.Core.UI.Theme
Imports PCL.Core.Utils.OS

Public Module ModMainWindowWindowShell

    Public Function HandleWindowMessage(
        hwnd As IntPtr,
        msg As Integer,
        lParam As IntPtr,
        isWindowLoadFinished As Boolean,
        showWindowToTop As Action,
        setSystemTimeChanged As Action(Of Boolean)) As Boolean
        If msg = 30 Then
            Dim NowDate = Date.Now
            If NowDate.Date = ApplicationOpenTime.Date Then
                Log("[System] 系统时间微调为：" & NowDate.ToLongDateString & " " & NowDate.ToLongTimeString)
                setSystemTimeChanged?.Invoke(False)
            Else
                Log("[System] 系统时间修改为：" & NowDate.ToLongDateString & " " & NowDate.ToLongTimeString)
                setSystemTimeChanged?.Invoke(True)
            End If
        ElseIf msg = 400 * 16 + 2 Then
            Log("[System] 收到置顶信息：" & hwnd.ToInt64)
            If Not isWindowLoadFinished Then
                Log("[System] 窗口尚未加载完成，忽略置顶请求")
                Return False
            End If
            showWindowToTop?.Invoke()
            Return True
        ElseIf msg = 26 Then
            If Marshal.PtrToStringAuto(lParam) = "ImmersiveColorSet" Then
                Log($"[System] 系统主题更改，深色模式：{SystemTheme.IsSystemInDarkMode()}")
                If Setup.Get("UiDarkMode") = 2 And IsDarkMode <> SystemTheme.IsSystemInDarkMode() Then
                    ThemeService.RefreshColorMode()
                End If
            End If
        End If

        Return False
    End Function

    Public Sub ApplyHiddenState(form As FormMain, value As Boolean, restoreWindow As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))

        If value Then
            form.Left -= 10000
            form.ShowInTaskbar = False
            form.Visibility = Visibility.Hidden
            Log("[System] 窗口已隐藏，位置：(" & form.Left & "," & form.Top & ")")
        Else
            If form.Left < -2000 Then form.Left += 10000
            restoreWindow?.Invoke()
        End If
    End Sub

    Public Sub ShowWindowToTop(form As FormMain, handle As IntPtr, clearHiddenState As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))

        RunInUi(
        Sub()
            form.Visibility = Visibility.Visible
            form.ShowInTaskbar = True
            form.WindowState = WindowState.Normal
            clearHiddenState?.Invoke()
            form.Topmost = True
            form.Topmost = False
            SetForegroundWindow(handle)
            form.Focus()
            Log($"[System] 窗口已置顶，位置：({form.Left}, {form.Top}), {form.Width} x {form.Height}")
        End Sub)
    End Sub

End Module
