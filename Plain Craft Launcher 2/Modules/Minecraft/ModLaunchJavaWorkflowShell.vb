Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchJavaWorkflowShell

    Public Function ResolveLaunchJava(instance As McInstance,
                                      task As LoaderTask(Of Integer, Integer),
                                      resolveSelection As Func(Of LoaderTask(Of Integer, Integer), MinecraftLaunchJavaWorkflowPlan, McInstance, JavaEntry),
                                      logMessage As Action(Of String)) As JavaEntry
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))
        If task Is Nothing Then Throw New ArgumentNullException(NameOf(task))
        If resolveSelection Is Nothing Then Throw New ArgumentNullException(NameOf(resolveSelection))

        Dim recommendedCode As Integer =
            If(instance.JsonObject?("javaVersion")?("majorVersion")?.ToObject(Of Integer),
               If(instance.JsonVersion?("java_version")?.ToObject(Of Integer), 0))
        Dim recommendedComponent As String =
            If(instance.JsonObject?("javaVersion")?("component")?.ToString,
               instance.JsonVersion?("java_component")?.ToString)
        If recommendedComponent = "" Then recommendedComponent = Nothing

        Dim jsonRequiredMajorVersion As Integer? = Nothing
        If instance.JsonObject("javaVersion") IsNot Nothing Then
            jsonRequiredMajorVersion = CInt(Val(instance.JsonObject("javaVersion")("majorVersion")))
        End If

        Dim javaWorkflow = MinecraftLaunchJavaWorkflowService.BuildPlan(
            New MinecraftLaunchJavaWorkflowRequest(
                instance.Info.Valid,
                instance.ReleaseTime,
                If(instance.Info.Valid, instance.Info.Vanilla, Nothing),
                instance.Info.HasOptiFine,
                instance.Info.HasForge,
                If(instance.Info.HasForge, instance.Info.Forge, Nothing),
                instance.Info.HasCleanroom,
                instance.Info.HasFabric,
                instance.Info.HasLiteLoader,
                instance.Info.HasLabyMod,
                jsonRequiredMajorVersion,
                recommendedCode,
                recommendedComponent))
        If javaWorkflow.RecommendedVersionLogMessage IsNot Nothing Then logMessage?.Invoke(javaWorkflow.RecommendedVersionLogMessage)

        Return resolveSelection(task, javaWorkflow, instance)
    End Function

End Module
