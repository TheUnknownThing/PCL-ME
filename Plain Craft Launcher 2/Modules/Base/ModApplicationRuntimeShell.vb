Imports System.IO
Imports PCL.Core.Utils

Public Module ModApplicationRuntimeShell

    Public Sub PrepareRuntime()
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        PresentationTraceSources.DataBindingSource.Listeners.Add(New BindingErrorTraceListener())
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error
        SecretOnApplicationStart()
    End Sub

    Public Sub HandleInitializationFailure(ex As Exception)
        Dim filePath As String = Nothing
        Try
            filePath = ExePathWithName
        Catch
        End Try
        MsgBox(ex.ToString() & vbCrLf & "PCL 所在路径：" & If(String.IsNullOrEmpty(filePath), "获取失败", filePath), MsgBoxStyle.Critical, "PCL 初始化错误")
        FormMain.EndProgramForce(ProcessReturnValues.Exception)
    End Sub

    Public Class BindingErrorTraceListener
        Inherits TraceListener

        Public Overrides Sub Write(message As String)
            Log($"警告，检测到 Binding 失败：{message}")
        End Sub

        Public Overrides Sub WriteLine(message As String)
            Log($"警告，检测到 Binding 失败：{message}")
        End Sub
    End Class

End Module
