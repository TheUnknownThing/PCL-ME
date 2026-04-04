Imports PCL.Core.Utils

Public Partial Class CrashAnalyzer

    Private TempFolder As String

    Public Sub New(UUID As Integer)
        TempFolder = RequestTaskTempFolder()
        Directory.CreateDirectory(TempFolder & "Temp\")
        Directory.CreateDirectory(TempFolder & "Report\")
        Log("[Crash] 崩溃分析暂存文件夹：" & TempFolder)
    End Sub

    Public Sub Collect(VersionPathIndie As String, Optional LatestLog As IList(Of String) = Nothing)
        AnalyzeRawFiles = ModCrashCollectionShell.CollectAnalyzeRawFiles(VersionPathIndie, LatestLog, TempFolder)
    End Sub

    Public Sub Import(FilePath As String)
        AnalyzeRawFiles = ModCrashCollectionShell.ImportAnalyzeRawFiles(FilePath, TempFolder)
    End Sub

End Class
