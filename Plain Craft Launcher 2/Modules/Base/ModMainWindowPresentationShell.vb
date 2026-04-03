Public Module ModMainWindowPresentationShell

    Public Sub PresentLoadedWindow(form As FormMain, attachWindowHook As Action, onPresentationCompleted As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))

        form.Topmost = False
        If FrmStart IsNot Nothing Then FrmStart.Close(New TimeSpan(0, 0, 0, 0, 400 / AniSpeed))

        form.IsSizeSaveable = True
        form.ShowWindowToTop()
        attachWindowHook?.Invoke()

        AniStart({
            AaCode(Sub() AniControlEnabled -= 1, 50),
            AaOpacity(form, Setup.Get("UiLauncherTransparent") / 1000 + 0.4, 250, 100),
            AaDouble(Sub(i) form.TransformPos.Y += i, -form.TransformPos.Y, 600, 100, New AniEaseOutBack(AniEasePower.Weak)),
            AaDouble(Sub(i) form.TransformRotate.Angle += i, -form.TransformRotate.Angle, 500, 100, New AniEaseOutBack(AniEasePower.Weak)),
            AaCode(Sub() onPresentationCompleted?.Invoke(), , True)
        }, "Form Show")

        AniStart()
        TimerMainStart()
    End Sub

End Module
