Imports System.IO
Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.Utils.OS

Public Module ModLaunchPrerunShell

    Public Sub ApplyGpuPreference(javaExePath As String, wantHighPerformance As Boolean, logMessage As Action(Of String))
        Try
            ProcessInterop.SetGpuPreference(javaExePath, wantHighPerformance)
        Catch ex As Exception
            Dim failurePlan = MinecraftLaunchPrerunWorkflowService.BuildGpuPreferenceFailurePlan(
                New MinecraftLaunchGpuPreferenceFailureRequest(
                    javaExePath,
                    wantHighPerformance,
                    ProcessInterop.IsAdmin()))
            If failurePlan.ActionKind = MinecraftLaunchGpuPreferenceFailureActionKind.LogDirectFailure Then
                Log(ex, "直接调整显卡设置失败")
            Else
                Log(ex, failurePlan.RetryLogMessage)
                Try
                    If ProcessInterop.StartAsAdmin(failurePlan.AdminRetryArguments).ExitCode = ProcessReturnValues.TaskDone Then
                        logMessage?.Invoke("以管理员权限重启 PCL 并调整显卡设置成功")
                    Else
                        Throw New Exception("调整过程中出现异常")
                    End If
                Catch retryEx As Exception
                    Log(retryEx, failurePlan.RetryFailureHintMessage, LogLevel.Hint)
                End Try
            End If
        End Try
    End Sub

    Public Sub UpdateLauncherProfilesJson(plan As MinecraftLaunchLauncherProfilesPrerunPlan,
                                          mcFolder As String,
                                          logMessage As Action(Of String))
        If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))
        If Not plan.ShouldEnsureFileExists OrElse String.IsNullOrWhiteSpace(plan.Path) Then Exit Sub
        If Not plan.Workflow.ShouldWrite Then Exit Sub

        Try
            McFolderLauncherProfilesJsonCreate(mcFolder)
            WriteLauncherProfilesJson(plan.Path, plan.Workflow.InitialAttempt, logMessage)
        Catch ex As Exception
            Log(ex, plan.Workflow.RetryLogMessage)
            Try
                File.Delete(plan.Path)
                McFolderLauncherProfilesJsonCreate(mcFolder)
                WriteLauncherProfilesJson(plan.Path, plan.Workflow.RetryAttempt, logMessage)
            Catch retryEx As Exception
                Log(retryEx, plan.Workflow.FailureLogMessage, LogLevel.Feedback)
            End Try
        End Try
    End Sub

    Public Sub ApplyOptionsSync(plan As MinecraftLaunchOptionsPrerunPlan, logMessage As Action(Of String))
        If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))

        Try
            If plan.SyncPlan.TargetSelectionLogMessage IsNot Nothing Then logMessage?.Invoke(plan.SyncPlan.TargetSelectionLogMessage)
            For Each optionWrite In plan.SyncPlan.Writes
                WriteIni(plan.TargetFilePath, optionWrite.Key, optionWrite.Value)
            Next
            For Each message In plan.SyncPlan.LogMessages
                logMessage?.Invoke(message)
            Next
        Catch ex As Exception
            Log(ex, "更新 options.txt 失败", LogLevel.Hint)
        End Try
    End Sub

    Private Sub WriteLauncherProfilesJson(launcherProfilesPath As String,
                                          attempt As MinecraftLaunchLauncherProfilesWriteAttempt,
                                          logMessage As Action(Of String))
        If attempt Is Nothing Then Throw New ArgumentNullException(NameOf(attempt))

        WriteFile(launcherProfilesPath, attempt.UpdatedProfilesJson, Encoding:=Encoding.GetEncoding("GB18030"))
        logMessage?.Invoke(attempt.SuccessLogMessage)
    End Sub

End Module
