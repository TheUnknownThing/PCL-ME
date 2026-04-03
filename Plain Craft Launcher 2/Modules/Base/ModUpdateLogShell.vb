Imports PCL.Core.App.Essentials

Public Module ModUpdateLogShell

    Public Sub ShowUpdateLogFromFile(changelogFile As String, versionBranchName As String, versionBaseName As String)
        If String.IsNullOrWhiteSpace(changelogFile) Then Throw New ArgumentException("缺少更新日志文件路径。", NameOf(changelogFile))

        RunInNewThread(
            Sub()
                Dim changelog As String = Nothing
                If File.Exists(changelogFile) Then
                    changelog = ReadFile(changelogFile)
                End If
                Dim prompt = LauncherUpdateLogService.BuildPrompt(New LauncherUpdateLogRequest(changelog, versionBranchName, versionBaseName))
                ShowUpdateLog(prompt)
            End Sub, "UpdateLog Output")
    End Sub

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
