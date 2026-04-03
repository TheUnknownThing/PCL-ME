Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.IO

Public Module ModJavaTransferShell

    Public Function DownloadRuntimeIndex(indexRequestPlan As MinecraftJavaRuntimeRequestUrlPlan) As String
        If indexRequestPlan Is Nothing Then Throw New ArgumentNullException(NameOf(indexRequestPlan))

        Return NetGetCodeByLoader(
            DlVersionListOrder(indexRequestPlan.OfficialUrls, indexRequestPlan.MirrorUrls),
            IsJson:=True)
    End Function

    Public Function DownloadRuntimeManifest(manifestPlan As MinecraftJavaRuntimeManifestRequestPlan) As String
        If manifestPlan Is Nothing Then Throw New ArgumentNullException(NameOf(manifestPlan))

        McLaunchLog(manifestPlan.LogMessage)
        Return NetGetCodeByRequestRetry(
            DlSourceOrder(manifestPlan.RequestUrls.OfficialUrls, manifestPlan.RequestUrls.MirrorUrls).First(),
            IsJson:=True)
    End Function

    Public Function DetectExistingRelativePaths(runtimePlan As MinecraftJavaRuntimeDownloadWorkflowPlan) As List(Of String)
        If runtimePlan Is Nothing Then Throw New ArgumentNullException(NameOf(runtimePlan))

        Dim existingRelativePaths As New List(Of String)(runtimePlan.Files.Count)
        For Each filePlan In runtimePlan.Files
            Dim checker As New FileChecker(ActualSize:=filePlan.Size, Hash:=filePlan.Sha1)
            If checker.Check(filePlan.TargetPath) Is Nothing Then
                existingRelativePaths.Add(filePlan.RelativePath)
            End If
        Next

        Return existingRelativePaths
    End Function

    Public Function BuildDownloadFiles(transferPlan As MinecraftJavaRuntimeDownloadTransferPlan) As List(Of NetFile)
        If transferPlan Is Nothing Then Throw New ArgumentNullException(NameOf(transferPlan))

        Dim results As New List(Of NetFile)(transferPlan.FilesToDownload.Count)
        For Each filePlan In transferPlan.FilesToDownload
            Dim checker As New FileChecker(ActualSize:=filePlan.Size, Hash:=filePlan.Sha1)
            results.Add(New NetFile(DlSourceOrder(filePlan.RequestUrls.OfficialUrls, filePlan.RequestUrls.MirrorUrls), filePlan.TargetPath, checker))
        Next

        Log(transferPlan.LogMessage)
        Return results
    End Function

End Module
