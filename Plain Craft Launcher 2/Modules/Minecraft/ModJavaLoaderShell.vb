Imports System.Threading
Imports PCL.Core.IO
Imports PCL.Core.Minecraft.Launch

Public Module ModJavaLoaderShell

    Private TrackedRuntimeDirectory As String = Nothing
    Private ReadOnly IgnoreHash As IReadOnlyList(Of String) = MinecraftJavaRuntimeDownloadSessionService.GetDefaultIgnoredSha1Hashes()

    Public Function CreateDownloadLoader() As LoaderCombo(Of String)
        Dim javaDownloadLoader As New LoaderDownload("下载 Java 文件", New List(Of NetFile)) With {.ProgressWeight = 10}
        Dim loader = New LoaderCombo(Of String)($"下载 Java", {
            New LoaderTask(Of String, List(Of NetFile))("获取 Java 下载信息", AddressOf LoadDownloadFiles) With {.ProgressWeight = 2},
            javaDownloadLoader
        })
        AddHandler javaDownloadLoader.OnStateChangedThread, AddressOf HandleDownloadStateChanged
        javaDownloadLoader.HasOnStateChangedThread = True
        Return loader
    End Function

    Public Sub ExecuteJavaDownload(downloadTarget As String, task As LoaderTask(Of Integer, Integer))
        If String.IsNullOrWhiteSpace(downloadTarget) Then Throw New ArgumentException("缺少 Java 下载目标。", NameOf(downloadTarget))
        If task Is Nothing Then Throw New ArgumentNullException(NameOf(task))

        Dim javaLoader = CreateDownloadLoader()
        Try
            javaLoader.Start(downloadTarget, IsForceRestart:=True)
            Do While javaLoader.State = LoadState.Loading AndAlso Not task.IsAborted
                task.Progress = javaLoader.Progress
                Thread.Sleep(10)
            Loop
        Finally
            javaLoader.Abort() '确保取消时中止 Java 下载
        End Try
    End Sub

    Private Sub LoadDownloadFiles(loader As LoaderTask(Of String, List(Of NetFile)))
        Log("[Java] 开始获取 Java 下载信息")
        Dim downloadFiles = ModJavaTransferShell.ResolveDownloadFiles(
            loader.Input,
            IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"),
            IgnoreHash,
            Is32BitSystem)
        TrackedRuntimeDirectory = downloadFiles.RuntimeBaseDirectory
        loader.Output = downloadFiles.Files
    End Sub

    Private Sub HandleDownloadStateChanged(raw As LoaderBase, newState As LoadState, oldState As LoadState)
        Dim sessionState = ModJavaDownloadSessionShell.ResolveSessionState(newState)
        If sessionState Is Nothing Then Exit Sub

        Dim transitionPlan = MinecraftJavaRuntimeDownloadSessionService.ResolveStateTransition(sessionState.Value, TrackedRuntimeDirectory)
        ModJavaDownloadSessionShell.ApplyStateTransition(transitionPlan, TrackedRuntimeDirectory)
    End Sub

End Module
