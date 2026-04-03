Imports PCL.Core.Minecraft.Launch

Public Module ModJavaDownloadSessionShell

    Public Function ResolveSessionState(state As LoadState) As MinecraftJavaRuntimeDownloadSessionState?
        Select Case state
            Case LoadState.Finished
                Return MinecraftJavaRuntimeDownloadSessionState.Finished
            Case LoadState.Failed
                Return MinecraftJavaRuntimeDownloadSessionState.Failed
            Case LoadState.Aborted
                Return MinecraftJavaRuntimeDownloadSessionState.Aborted
            Case Else
                Return Nothing
        End Select
    End Function

    Public Sub ApplyStateTransition(transitionPlan As MinecraftJavaRuntimeDownloadStateTransitionPlan, ByRef trackedRuntimeDirectory As String)
        If transitionPlan Is Nothing Then Throw New ArgumentNullException(NameOf(transitionPlan))

        If transitionPlan.CleanupLogMessage IsNot Nothing Then
            Log(transitionPlan.CleanupLogMessage, LogLevel.Debug)
        End If
        If transitionPlan.CleanupDirectoryPath IsNot Nothing Then
            DeleteDirectory(transitionPlan.CleanupDirectoryPath)
        End If
        If transitionPlan.ShouldRefreshJavaInventory Then
            ModJava.Javas.ScanJavaAsync().GetAwaiter().GetResult()
        End If
        If transitionPlan.ShouldClearTrackedRuntimeDirectory Then
            trackedRuntimeDirectory = Nothing
        End If
    End Sub

End Module
