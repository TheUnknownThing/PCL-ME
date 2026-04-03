Imports System.Windows
Imports System.Windows.Media
Imports PCL.Core.UI
Imports PCL.Core.Utils

Public Module ModMainWindowLoadedShell

    Public Sub PrepareLoadedWindow(form As FormMain, addResizer As Action, removeResizer As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))

        Setup.Load("UiBackgroundOpacity")
        Setup.Load("UiBackgroundBlur")
        Setup.Load("UiLogoType")
        Setup.Load("UiHiddenPageDownload")
        Setup.Load("UiAutoPauseVideo")

        PageSetupUI.HiddenRefresh()
        PageSetupUI.BackgroundRefresh(False, True)
        MusicRefreshPlay(False, True)

        If Not Setup.Get("UiLockWindowSize") Then
            addResizer?.Invoke()
        Else
            removeResizer?.Invoke()
        End If

        If RandomUtils.NextInt(1, 1000) = 233 Then
            form.ShapeTitleLogo.Data = New GeometryConverter().ConvertFromString("M26,29 v-25 h6 a7,7 180 0 1 0,14 h-6 M83,6.5 a10,11.5 180 1 0 0,18 M48,2.5 v24.5 h13.5")
        End If

        ThemeRefresh()
    End Sub

End Module
