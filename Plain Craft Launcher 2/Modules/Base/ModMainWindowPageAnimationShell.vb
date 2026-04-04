Imports System.Windows

Public Module ModMainWindowPageAnimationShell

    Public Sub AnimatePageChange(
        form As FormMain,
        targetLeft As MyPageLeft,
        targetRight As MyPageRight,
        refreshLeftLayout As Action,
        refreshBackButton As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If targetLeft Is Nothing Then Throw New ArgumentNullException(NameOf(targetLeft))
        If targetRight Is Nothing Then Throw New ArgumentNullException(NameOf(targetRight))

        AniStop("FrmMain LeftChange")
        AniStop("PageLeft PageChange")
        AniControlEnabled += 1

        If targetLeft.Parent IsNot Nothing Then targetLeft.SetValue(ContentPresenter.ContentProperty, Nothing)
        If targetRight.Parent IsNot Nothing Then targetRight.SetValue(ContentPresenter.ContentProperty, Nothing)
        form.PageLeft = targetLeft
        form.PageRight = targetRight

        CType(form.PanMainLeft.Child, MyPageLeft).TriggerHideAnimation()
        CType(form.PanMainRight.Child, MyPageRight).PageOnExit()
        AniControlEnabled -= 1

        AniStart({
            AaCode(
            Sub()
                AniControlEnabled += 1
                form.PanMainLeft.Child = form.PageLeft
                form.PageLeft.Opacity = 0
                form.PanMainLeft.Background = Nothing
                AniControlEnabled -= 1
                refreshLeftLayout?.Invoke()
            End Sub, 110),
            AaCode(
            Sub()
                form.PageLeft.Opacity = 1
                form.PageLeft.TriggerShowAnimation()
            End Sub, 30, True)
        }, "FrmMain PageChangeLeft")
        AniStart({
            AaCode(
            Sub()
                AniControlEnabled += 1
                CType(form.PanMainRight.Child, MyPageRight).PageOnForceExit()
                form.PanMainRight.Child = form.PageRight
                form.PageRight.Opacity = 0
                form.PanMainRight.Background = Nothing
                AniControlEnabled -= 1
                refreshBackButton?.Invoke()
            End Sub, 110),
            AaCode(
            Sub()
                form.PageRight.Opacity = 1
                form.PageRight.PageOnEnter()
            End Sub, 30, True)
        }, "FrmMain PageChangeRight")
    End Sub

    Public Sub ExitSubPage(form As FormMain, pageStack As List(Of FormMain.PageStackData))
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If pageStack Is Nothing Then Throw New ArgumentNullException(NameOf(pageStack))

        If Not pageStack.Any Then Return

        form.PanTitleMain.Visibility = Visibility.Visible
        form.PanTitleMain.IsHitTestVisible = True
        form.PanTitleInner.IsHitTestVisible = False
        AniStart({
            AaOpacity(form.PanTitleInner, -form.PanTitleInner.Opacity, 150),
            AaX(form.PanTitleInner, -18 - form.PanTitleInner.Margin.Left, 150,, New AniEaseInFluent),
            AaOpacity(form.PanTitleMain, 1 - form.PanTitleMain.Opacity, 150, 200),
            AaX(form.PanTitleMain, -form.PanTitleMain.Margin.Left, 350, 200, New AniEaseOutBack(AniEasePower.Weak)),
            AaCode(Sub() form.PanTitleInner.Visibility = Visibility.Collapsed,, True)
        }, "FrmMain Titlebar FirstLayer")
        pageStack.Clear()
    End Sub

End Module
