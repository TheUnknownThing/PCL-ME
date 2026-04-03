Imports PCL.Core.Minecraft
Imports PCL.Core.UI

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

End Module
