Public Module ModMainWindowSidebarShell

    Public Sub ResizeLeftSidebar(form As FormMain, newWidth As Double)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))

        Dim Delta As Double = newWidth - form.RectLeftBackground.Width
        If Math.Abs(Delta) > 0.1 AndAlso AniControlEnabled = 0 Then
            If form.PanMain.Opacity < 0.1 Then form.PanMainLeft.IsHitTestVisible = False
            If newWidth > 0 Then
                AniStart({
                    AaWidth(form.RectLeftBackground, newWidth - form.RectLeftBackground.Width, 180,, New AniEaseOutFluent(AniEasePower.ExtraStrong)),
                    AaOpacity(form.RectLeftShadow, 1 - form.RectLeftShadow.Opacity, 180),
                    AaCode(Sub() form.PanMainLeft.IsHitTestVisible = True, 150)
                }, "FrmMain LeftChange", True)
            Else
                AniStart({
                    AaWidth(form.RectLeftBackground, -form.RectLeftBackground.Width, 180,, New AniEaseOutFluent),
                    AaOpacity(form.RectLeftShadow, -form.RectLeftShadow.Opacity, 180),
                    AaCode(Sub() form.PanMainLeft.IsHitTestVisible = True, 150)
                }, "FrmMain LeftChange", True)
            End If
        Else
            form.RectLeftBackground.Width = newWidth
            form.PanMainLeft.IsHitTestVisible = True
            AniStop("FrmMain LeftChange")
        End If
    End Sub

End Module
