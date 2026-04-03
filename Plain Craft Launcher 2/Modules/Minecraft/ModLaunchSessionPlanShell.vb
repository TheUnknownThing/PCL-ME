Imports PCL.Core.App
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchSessionPlanShell

    Public Function BuildSessionStartPlan(instance As McInstance,
                                          selectedJava As JavaEntry,
                                          launchArgument As String,
                                          watcherWorkflowRequest As MinecraftLaunchWatcherWorkflowRequest,
                                          replaceArgumentTokens As Func(Of String, Boolean, String)) As MinecraftLaunchSessionStartWorkflowPlan
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))
        If selectedJava Is Nothing Then Throw New ArgumentNullException(NameOf(selectedJava))
        If replaceArgumentTokens Is Nothing Then Throw New ArgumentNullException(NameOf(replaceArgumentTokens))

        Dim customCommandGlobal As String = Setup.Get("LaunchAdvanceRun")
        If customCommandGlobal <> "" Then customCommandGlobal = replaceArgumentTokens(customCommandGlobal, True)
        Dim customCommandVersion As String = Setup.Get("VersionAdvanceRun", instance:=instance)
        If customCommandVersion <> "" Then customCommandVersion = replaceArgumentTokens(customCommandVersion, True)

        Return MinecraftLaunchSessionWorkflowService.BuildStartPlan(
            New MinecraftLaunchSessionStartWorkflowRequest(
                New MinecraftLaunchCustomCommandWorkflowRequest(
                    New MinecraftLaunchCustomCommandRequest(
                        selectedJava.Installation.MajorVersion,
                        instance.Name,
                        ShortenPath(instance.PathIndie),
                        selectedJava.Installation.JavaExePath,
                        launchArgument,
                        customCommandGlobal,
                        Setup.Get("LaunchAdvanceRunWait"),
                        customCommandVersion,
                        Setup.Get("VersionAdvanceRunWait", instance:=instance)),
                    ShortenPath(McFolderSelected)),
                New MinecraftLaunchProcessRequest(
                    Setup.Get("LaunchAdvanceNoJavaw"),
                    selectedJava.Installation.JavaExePath,
                    selectedJava.Installation.JavawExePath,
                    ShortenPath(selectedJava.Installation.JavaFolder),
                    Environment.GetEnvironmentVariable("Path"),
                    ShortenPath(McFolderSelected),
                    ShortenPath(instance.PathIndie),
                    launchArgument,
                    Setup.Get("LaunchArgumentPriority")),
                watcherWorkflowRequest))
    End Function

    Public Function TryWriteLaunchScript(sessionPlan As MinecraftLaunchSessionStartWorkflowPlan,
                                         saveBatchPath As String,
                                         setAbortHint As Action(Of String),
                                         abortLaunch As Action) As Boolean
        If sessionPlan Is Nothing Then Throw New ArgumentNullException(NameOf(sessionPlan))

        WriteFile(
            If(saveBatchPath, ExePath & "PCL\LatestLaunch.bat"),
            FilterAccessToken(sessionPlan.CustomCommandPlan.BatchScriptContent, "F"),
            Encoding:=If(sessionPlan.CustomCommandPlan.UseUtf8Encoding, Encoding.UTF8, Encoding.Default))

        If saveBatchPath Is Nothing Then Return False

        Dim scriptExportPlan = MinecraftLaunchShellService.BuildScriptExportPlan(saveBatchPath)
        ModLaunchSessionShell.CompleteScriptExport(
            scriptExportPlan,
            AddressOf ModLaunch.McLaunchLog,
            setAbortHint,
            abortLaunch)
        Return True
    End Function

End Module
