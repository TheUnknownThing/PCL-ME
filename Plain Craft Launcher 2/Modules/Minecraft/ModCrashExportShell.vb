Imports PCL.Core.Minecraft
Imports PCL.Core.UI
Imports PCL.Core.Logging
Imports PCL.Core.Utils.OS

Public Module ModCrashExportShell

    Public Function TryExportReport(exportPlan As MinecraftCrashExportPlan) As Boolean
        If exportPlan Is Nothing Then Throw New ArgumentNullException(NameOf(exportPlan))

        Dim fileAddress As String = Nothing
        Dim saveDialogPlan = MinecraftCrashResponseWorkflowService.BuildExportSaveDialogPlan(exportPlan.SuggestedArchiveName)
        RunInUiWait(Sub() fileAddress = SystemDialogs.SelectSaveFile(saveDialogPlan.Title, saveDialogPlan.DefaultFileName, saveDialogPlan.Filter))
        If String.IsNullOrEmpty(fileAddress) Then Return False

        MinecraftCrashExportArchiveService.CreateArchive(
            New MinecraftCrashExportArchiveRequest(
                fileAddress,
                exportPlan.ExportRequest))
        Dim completionPlan = MinecraftCrashResponseWorkflowService.BuildExportCompletionPlan(fileAddress)
        Hint(completionPlan.HintMessage, HintType.Finish)
        OpenExplorer(completionPlan.RevealInShellPath)
        Return True
    End Function

    Public Function TryExportCurrentReport(tempFolder As String,
                                           outputFiles As IReadOnlyList(Of String),
                                           extraFiles As IReadOnlyList(Of String)) As Boolean
        Try
            FeedbackInfo()
            Dim currentLauncherLogPath As String = Nothing
            If LogWrapper.CurrentLogger.CurrentLogFiles.Any Then currentLauncherLogPath = LogWrapper.CurrentLogger.CurrentLogFiles.Last()
            Dim exportPlan = MinecraftCrashExportWorkflowService.CreatePlan(
                New MinecraftCrashExportPlanRequest(
                    Date.Now,
                    tempFolder & "Report\",
                    VersionBaseName,
                    UniqueAddress,
                    outputFiles,
                    extraFiles,
                    currentLauncherLogPath,
                    SystemEnvironmentInfo.GetSnapshot(),
                    McLoginLoader.Output.AccessToken,
                    McLoginLoader.Output.Uuid,
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
            Return TryExportReport(exportPlan)
        Catch ex As Exception
            Log(ex, "导出错误报告失败", LogLevel.Feedback)
            Return False
        End Try
    End Function

End Module
