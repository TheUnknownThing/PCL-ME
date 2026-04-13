Imports System.Windows
Imports System.Windows.Media

Public Module ModMainWindowShutdownShell

    Public Function ConfirmShutdown(sendWarning As Boolean) As Boolean
        If Not sendWarning OrElse Not HasDownloadingTask() Then Return True

        If MyMsgBox("还有下载任务尚未完成，是否确定退出？", "提示", "确定", "取消") <> 1 Then Return False

        RunInNewThread(
        Sub()
            Log("[System] 正在强行停止任务")
            For Each Task As LoaderBase In LoaderTaskbar.ToList()
                Task.Abort()
            Next
        End Sub, "强行停止下载任务")
        Return True
    End Function

    Public Sub RunShutdown(form As FormMain, onForceShutdown As Action)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))

        RunInUiWait(
        Sub()
            form.VideoBack.Stop()
            form.VideoBack.Source = Nothing
            form.VideoBack.Close()
            form.IsHitTestVisible = False

            If form.RenderTransform Is Nothing Then
                Dim TransformPos As New TranslateTransform(0, 0)
                Dim TransformRotate As New RotateTransform(0)
                Dim TransformScale As New ScaleTransform(1, 1)
                TransformScale.CenterX = form.Width / 2
                TransformScale.CenterY = form.Height / 2
                form.RenderTransform = New TransformGroup() With {.Children = New TransformCollection({TransformRotate, TransformPos, TransformScale})}

                AniStart({
                    AaOpacity(form, -form.Opacity, 140, 40, New AniEaseOutFluent(AniEasePower.Weak)),
                    AaDouble(
                    Sub(i)
                        TransformScale.ScaleX += i
                        TransformScale.ScaleY += i
                    End Sub, 0.88 - TransformScale.ScaleX, 180),
                    AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 180, 0, New AniEaseOutFluent(AniEasePower.Weak)),
                    AaDouble(Sub(i) TransformRotate.Angle += i, 0.6 - TransformRotate.Angle, 180, 0, New AniEaseInoutFluent(AniEasePower.Weak)),
                    AaCode(
                    Sub()
                        form.IsHitTestVisible = False
                        form.Visibility = Visibility.Collapsed
                        form.ShowInTaskbar = False
                    End Sub, 210),
                    AaCode(Sub() onForceShutdown?.Invoke(), 230)
                }, "Form Close")
            Else
                onForceShutdown?.Invoke()
            End If

            Log("[System] 收到关闭指令")
        End Sub)
    End Sub

End Module
