Imports System.Windows

Public Module ModMainWindowPageTitleShell

    Public Sub ApplyTitleTransition(
        form As FormMain,
        stack As FormMain.PageStackData,
        pageName As String,
        pageCurrent As FormMain.PageStackData,
        pageStack As List(Of FormMain.PageStackData),
        refreshPageName As Action(Of FormMain.PageStackData),
        exitSubPage As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If stack Is Nothing Then Throw New ArgumentNullException(NameOf(stack))
        If pageStack Is Nothing Then Throw New ArgumentNullException(NameOf(pageStack))

        If pageName = "" Then
            exitSubPage?.Invoke()
            Return
        End If

        If pageStack.Any Then
            AniStart({
                AaOpacity(form.LabTitleInner, -form.LabTitleInner.Opacity, 130),
                AaCode(Sub() form.LabTitleInner.Text = pageName,, True),
                AaOpacity(form.LabTitleInner, 1, 150, 30)
            }, "FrmMain Titlebar SubLayer")
            If pageStack.Contains(stack) Then
                Do While pageStack.Contains(stack)
                    pageStack.RemoveAt(0)
                Loop
            Else
                pageStack.Insert(0, pageCurrent)
            End If
        Else
            form.PanTitleInner.Visibility = Visibility.Visible
            form.PanTitleMain.IsHitTestVisible = False
            form.PanTitleInner.IsHitTestVisible = True
            refreshPageName?.Invoke(stack)
            AniStart({
                AaOpacity(form.PanTitleMain, -form.PanTitleMain.Opacity, 150),
                AaX(form.PanTitleMain, 12 - form.PanTitleMain.Margin.Left, 150,, New AniEaseInFluent(AniEasePower.Weak)),
                AaOpacity(form.PanTitleInner, 1 - form.PanTitleInner.Opacity, 150, 200),
                AaX(form.PanTitleInner, -form.PanTitleInner.Margin.Left, 350, 200, New AniEaseOutBack),
                AaCode(Sub() form.PanTitleMain.Visibility = Visibility.Collapsed,, True)
            }, "FrmMain Titlebar FirstLayer")
            pageStack.Insert(0, pageCurrent)
        End If
    End Sub

End Module
