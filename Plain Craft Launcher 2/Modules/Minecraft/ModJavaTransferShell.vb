Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.IO

Public Module ModJavaTransferShell

    Public Structure JavaRuntimeDownloadFilesResult
        Public Sub New(runtimeBaseDirectory As String, files As List(Of NetFile))
            Me.RuntimeBaseDirectory = runtimeBaseDirectory
            Me.Files = files
        End Sub

        Public Property RuntimeBaseDirectory As String
        Public Property Files As List(Of NetFile)
    End Structure

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

    Public Function ResolveDownloadFiles(downloadTarget As String,
                                         minecraftRootPath As String,
                                         ignoredHashes As IReadOnlyList(Of String),
                                         is32BitSystem As Boolean) As JavaRuntimeDownloadFilesResult
        If String.IsNullOrWhiteSpace(downloadTarget) Then Throw New ArgumentException("缺少 Java 下载目标。", NameOf(downloadTarget))
        If String.IsNullOrWhiteSpace(minecraftRootPath) Then Throw New ArgumentException("缺少 Minecraft 根目录。", NameOf(minecraftRootPath))
        If ignoredHashes Is Nothing Then Throw New ArgumentNullException(NameOf(ignoredHashes))

        Dim indexRequestPlan = MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan()
        Dim indexFileStr = DownloadRuntimeIndex(indexRequestPlan)
        Dim manifestPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
            New MinecraftJavaRuntimeManifestRequestPlanRequest(
                indexFileStr,
                $"windows-x{If(is32BitSystem, "86", "64")}",
                downloadTarget,
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()))

        Dim manifestFileStr = DownloadRuntimeManifest(manifestPlan)
        Dim runtimeBaseDirectory = MinecraftJavaRuntimeDownloadSessionService.GetRuntimeBaseDirectory(
            minecraftRootPath,
            manifestPlan.Selection.ComponentKey)
        Dim runtimePlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            New MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                manifestFileStr,
                runtimeBaseDirectory,
                ignoredHashes,
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()))
        runtimeBaseDirectory = runtimePlan.DownloadPlan.RuntimeBaseDirectory

        Dim existingRelativePaths = DetectExistingRelativePaths(runtimePlan)
        Dim transferPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildTransferPlan(
            New MinecraftJavaRuntimeDownloadTransferPlanRequest(
                runtimePlan,
                existingRelativePaths))
        Return New JavaRuntimeDownloadFilesResult(runtimeBaseDirectory, BuildDownloadFiles(transferPlan))
    End Function

End Module
