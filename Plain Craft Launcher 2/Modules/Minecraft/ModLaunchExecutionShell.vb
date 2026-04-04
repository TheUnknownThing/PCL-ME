Imports System.Threading
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.Utils.Processes

Public Module ModLaunchExecutionShell

    Public Sub ExecuteCustomCommand(shellPlan As MinecraftLaunchCustomCommandShellPlan,
                                    loader As LoaderTask(Of Integer, Integer),
                                    logMessage As Action(Of String))
        If shellPlan Is Nothing Then Throw New ArgumentNullException(NameOf(shellPlan))
        If loader Is Nothing Then Throw New ArgumentNullException(NameOf(loader))

        logMessage?.Invoke(shellPlan.StartLogMessage)
        Dim customProcess As Process = Nothing
        Try
            customProcess = SystemProcessManager.Current.Start(
                MinecraftLaunchProcessExecutionService.BuildCustomCommandStartRequest(shellPlan))
            If customProcess Is Nothing Then Throw New InvalidOperationException("自定义命令进程启动失败。")
            If shellPlan.WaitForExit Then
                Do Until customProcess.HasExited OrElse loader.IsAborted
                    Thread.Sleep(10)
                Loop
            End If
        Catch ex As Exception
            Log(ex, shellPlan.FailureLogMessage, LogLevel.Hint)
        Finally
            If customProcess IsNot Nothing AndAlso Not customProcess.HasExited AndAlso loader.IsAborted Then
                logMessage?.Invoke(shellPlan.AbortKillLogMessage) '#1183
                SystemProcessManager.Current.Kill(customProcess)
            End If
        End Try
    End Sub

    Public Function StartGameProcess(shellPlan As MinecraftLaunchProcessShellPlan,
                                     loader As LoaderTask(Of Integer, Process),
                                     logMessage As Action(Of String)) As Process
        If shellPlan Is Nothing Then Throw New ArgumentNullException(NameOf(shellPlan))
        If loader Is Nothing Then Throw New ArgumentNullException(NameOf(loader))

        Dim gameProcess = SystemProcessManager.Current.Start(
            MinecraftLaunchProcessExecutionService.BuildGameProcessStartRequest(shellPlan))
        If gameProcess Is Nothing Then Throw New InvalidOperationException("游戏进程启动失败。")

        logMessage?.Invoke(shellPlan.StartedLogMessage)
        If loader.IsAborted Then
            logMessage?.Invoke(shellPlan.AbortKillLogMessage) '#1631
            SystemProcessManager.Current.Kill(gameProcess)
            Return Nothing
        End If

        loader.Output = gameProcess
        If Not MinecraftLaunchProcessExecutionService.TryApplyPriority(gameProcess, shellPlan.PriorityKind) Then
            Log("设置进程优先级失败", LogLevel.Feedback)
        End If

        Return gameProcess
    End Function

    Public Function WaitForGameWindow(sessionPlan As MinecraftLaunchSessionStartWorkflowPlan,
                                      loader As LoaderTask(Of Process, Integer),
                                      instance As McInstance,
                                      resolveWindowTitle As Func(Of String, String),
                                      logMessage As Action(Of String)) As Watcher
        If sessionPlan Is Nothing Then Throw New ArgumentNullException(NameOf(sessionPlan))
        If loader Is Nothing Then Throw New ArgumentNullException(NameOf(loader))
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))
        If resolveWindowTitle Is Nothing Then Throw New ArgumentNullException(NameOf(resolveWindowTitle))

        For Each logLine In sessionPlan.WatcherWorkflowPlan.StartupSummaryLogLines
            logMessage?.Invoke(logLine)
        Next

        Dim windowTitle = resolveWindowTitle(sessionPlan.WatcherWorkflowPlan.RawWindowTitleTemplate)
        Dim watcher = New Watcher(
            loader,
            instance,
            windowTitle,
            sessionPlan.WatcherWorkflowPlan.JstackExecutablePath,
            sessionPlan.WatcherWorkflowPlan.ShouldAttachRealtimeLog)

        If sessionPlan.WatcherWorkflowPlan.ShouldAttachRealtimeLog Then
            ModLaunchSessionShell.AttachRealtimeLog(
                watcher,
                sessionPlan.WatcherWorkflowPlan.RealtimeLogAttachedMessage,
                logMessage)
        End If

        Do While watcher.State = Watcher.MinecraftState.Loading
            Thread.Sleep(100)
        Loop
        If watcher.State = Watcher.MinecraftState.Crashed Then
            Throw New Exception("$$")
        End If

        Return watcher
    End Function

End Module
