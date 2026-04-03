Public Module ModMainWindowDragControlShell

    Public Sub TickDragControl(stopDrag As Action)
        If DragControl Is Nothing Then Return
        If Mouse.LeftButton <> MouseButtonState.Pressed Then
            stopDrag?.Invoke()
        End If
    End Sub

    Public Sub ContinueDragControl(stopDrag As Action)
        If DragControl Is Nothing Then Return
        If Mouse.LeftButton = MouseButtonState.Pressed Then
            DragControl.DragDoing()
        Else
            stopDrag?.Invoke()
        End If
    End Sub

    Public Sub StopDragControl()
        RunInUi(Sub()
                    If DragControl Is Nothing Then Return
                    Dim Control = DragControl
                    DragControl = Nothing
                    Control.DragStop()
                End Sub)
    End Sub

End Module
