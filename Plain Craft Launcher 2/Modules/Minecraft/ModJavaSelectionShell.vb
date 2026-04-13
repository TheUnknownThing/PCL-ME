Imports System.Threading
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch

Public Module ModJavaSelectionShell

    Public Function ResolveLaunchJavaSelectionShell(task As LoaderTask(Of Integer, Integer),
                                                    javaWorkflow As MinecraftLaunchJavaWorkflowPlan,
                                                    relatedInstance As McInstance) As JavaEntry
        If task Is Nothing Then Throw New ArgumentNullException(NameOf(task))
        If javaWorkflow Is Nothing Then Throw New ArgumentNullException(NameOf(javaWorkflow))
        If relatedInstance Is Nothing Then Throw New ArgumentNullException(NameOf(relatedInstance))

        SyncLock ModJava.JavaLock
            ModLaunch.McLaunchLog(javaWorkflow.RequirementLogMessage)

            Dim selectedJava = ModJava.JavaSelect("$$", javaWorkflow.MinimumVersion, javaWorkflow.MaximumVersion, relatedInstance)
            If task.IsAborted Then Return Nothing

            Dim initialSelection = MinecraftLaunchJavaSelectionWorkflowService.ResolveInitialSelection(
                javaWorkflow,
                selectedJava?.ToString())
            If initialSelection.LogMessage IsNot Nothing Then ModLaunch.McLaunchLog(initialSelection.LogMessage)
            If initialSelection.ActionKind = MinecraftLaunchJavaSelectionActionKind.UseSelectedJava Then
                Return selectedJava
            End If

            If task.IsAborted Then Return Nothing '中断加载会导致 JavaSelect 异常地返回空值，误判找不到 Java
            Dim javaDecision = ModLaunchPromptShell.RunJavaPrompt(initialSelection.Prompt)
            Dim promptOutcome = MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(initialSelection.Prompt, javaDecision.Decision)
            If promptOutcome.ActionKind <> MinecraftLaunchJavaPromptActionKind.DownloadAndRetrySelection Then Throw New Exception("$$")

            ExecuteJavaDownload(promptOutcome.DownloadTarget, task)

            selectedJava = ModJava.JavaSelect("$$", javaWorkflow.MinimumVersion, javaWorkflow.MaximumVersion, relatedInstance)
            If task.IsAborted Then Return Nothing

            Dim postDownloadSelection = MinecraftLaunchJavaSelectionWorkflowService.ResolvePostDownloadSelection(
                javaWorkflow,
                selectedJava?.ToString())
            If postDownloadSelection.ActionKind = MinecraftLaunchJavaPostDownloadActionKind.UseSelectedJava Then
                If postDownloadSelection.LogMessage IsNot Nothing Then ModLaunch.McLaunchLog(postDownloadSelection.LogMessage)
                Return selectedJava
            End If

            ModJavaPromptShell.ShowJavaSelectionFailureHint(postDownloadSelection.HintMessage)
            Throw New Exception("$$")
        End SyncLock
    End Function

    Private Sub ExecuteJavaDownload(downloadTarget As String, task As LoaderTask(Of Integer, Integer))
        ModJavaLoaderShell.ExecuteJavaDownload(downloadTarget, task)
    End Sub

End Module
