Imports System.Windows.Interop
Imports System.Windows.Media
Imports PCL.Core.Logging
Imports PCL.Core.Utils.OS

Public Module ModMainWindowChromeShell

    Public Sub InitializeCustomWindow(form As FormMain, sizeWndProc As HwndSourceHook)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))

        If Setup.Get("SystemDisableHardwareAcceleration") Then
            Dim hwndSource As HwndSource = TryCast(PresentationSource.FromVisual(form), HwndSource)
            If hwndSource IsNot Nothing Then
                hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly
            End If
        End If

        Dim hwnd As IntPtr = New WindowInteropHelper(form).Handle
        Dim source As HwndSource = HwndSource.FromHwnd(hwnd)
        If source IsNot Nothing Then
            source.CompositionTarget.BackgroundColor = Colors.Transparent
            source.AddHook(sizeWndProc)
        End If

        Try
            WindowInterop.ExtendFrameIntoClientArea(hwnd, -1)
        Catch ex As Exception
            LogWrapper.Error("DWM 窗口框架应用失败: " & ex.Message)
        End Try
    End Sub

End Module
