Imports PCL.Core.App.Essentials

Public Module ModUpdateLogShell

    Public Sub ShowUpdateLog(prompt As LauncherUpdateLogPrompt)
        If prompt Is Nothing Then Throw New ArgumentNullException(NameOf(prompt))

        If MyMsgBoxMarkdown(
            prompt.MarkdownContent,
            prompt.Title,
            prompt.ConfirmLabel,
            prompt.FullChangelogLabel) = 2 Then
            OpenWebsite(prompt.FullChangelogUrl)
        End If
    End Sub

End Module
