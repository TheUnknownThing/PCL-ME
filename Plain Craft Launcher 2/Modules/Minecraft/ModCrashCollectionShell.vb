Imports PCL.Core.Utils

Public Module ModCrashCollectionShell

    Public Function CollectAnalyzeRawFiles(versionPathIndie As String,
                                           latestLog As IList(Of String),
                                           tempFolder As String) As List(Of KeyValuePair(Of String, String()))
        Log("[Crash] 步骤 1：收集日志文件")

        Dim analyzeRawFiles As New List(Of KeyValuePair(Of String, String()))
        Dim possibleLogs As New List(Of String)
        Try
            Dim dirInfo As New DirectoryInfo(versionPathIndie & "crash-reports\")
            If dirInfo.Exists Then
                For Each file In dirInfo.EnumerateFiles
                    possibleLogs.Add(file.FullName)
                Next
            End If
        Catch ex As Exception
            Log(ex, "收集 Minecraft 崩溃日志文件夹下的日志失败")
        End Try
        Try
            For Each file In New DirectoryInfo(versionPathIndie).Parent.Parent.EnumerateFiles
                If If(file.Extension, "") <> ".log" Then Continue For
                possibleLogs.Add(file.FullName)
            Next
        Catch ex As Exception
            Log(ex, "收集 Minecraft 主文件夹下的日志失败")
        End Try
        Try
            For Each file In New DirectoryInfo(versionPathIndie).EnumerateFiles
                If If(file.Extension, "") <> ".log" Then Continue For
                possibleLogs.Add(file.FullName)
            Next
        Catch ex As Exception
            Log(ex, "收集 Minecraft 隔离文件夹下的日志失败")
        End Try
        possibleLogs.Add(versionPathIndie & "logs\latest.log")
        Dim launchScript As String = ReadFile(ExePath & "PCL\LatestLaunch.bat")
        If launchScript.ContainsF("-Dlog4j2.formatMsgNoLookups=false") Then
            possibleLogs.Add(versionPathIndie & "logs\debug.log")
        End If
        possibleLogs = possibleLogs.Distinct.ToList()

        Dim rightLogs As New List(Of String)
        For Each logFile In possibleLogs
            Try
                Dim info As New FileInfo(logFile)
                If Not info.Exists Then Continue For
                Dim time = Math.Abs((info.LastWriteTime - Date.Now).TotalMinutes)
                If time < 3 AndAlso info.Length > 0 Then
                    rightLogs.Add(logFile)
                    Log("[Crash] 可能可用的日志文件：" & logFile & "（" & Math.Round(time, 1) & " 分钟）")
                End If
            Catch ex As Exception
                Log(ex, "确认崩溃日志时间失败（" & logFile & "）")
            End Try
        Next
        If Not rightLogs.Any() Then Log("[Crash] 未发现可能可用的日志文件")

        For Each filePath In rightLogs
            Try
                analyzeRawFiles.Add(New KeyValuePair(Of String, String())(filePath, ReadFile(filePath).Split(vbCrLf.ToCharArray)))
            Catch ex As Exception
                Log(ex, "读取可能的崩溃日志文件失败（" & filePath & "）")
            End Try
        Next

        If latestLog IsNot Nothing AndAlso latestLog.Any Then
            Dim rawOutput As String = Join(latestLog, vbCrLf)
            Log("[Crash] 以下为游戏输出的最后一段内容：" & vbCrLf & rawOutput)
            WriteFile(tempFolder & "RawOutput.log", rawOutput)
            analyzeRawFiles.Add(New KeyValuePair(Of String, String())(tempFolder & "RawOutput.log", latestLog.ToArray))
            latestLog.Clear()
        End If

        Log("[Crash] 步骤 1：收集日志文件完成，收集到 " & analyzeRawFiles.Count & " 个文件")
        Return analyzeRawFiles
    End Function

    Public Function ImportAnalyzeRawFiles(filePath As String, tempFolder As String) As List(Of KeyValuePair(Of String, String()))
        Log("[Crash] 步骤 1：自主导入日志文件")

        Dim analyzeRawFiles As New List(Of KeyValuePair(Of String, String()))
        Try
            Dim info As New FileInfo(filePath)
            If info.Exists AndAlso info.Length > 0 AndAlso Not filePath.EndsWithF(".jar", True) Then
                ExtractFile(filePath, tempFolder & "Temp\")
                Log("[Crash] 已解压导入的日志文件：" & filePath)
                GoTo Extracted
            End If
        Catch
        End Try

        CopyFile(filePath, tempFolder & "Temp\" & GetFileNameFromPath(filePath))
        Log("[Crash] 已复制导入的日志文件：" & filePath)
Extracted:

        For Each targetFile As FileInfo In New DirectoryInfo(tempFolder & "Temp\").EnumerateFiles.ToList()
            Try
                If Not targetFile.Exists OrElse targetFile.Length = 0 Then Continue For
                Dim ext As String = targetFile.Extension.ToLower
                If ext = ".log" OrElse ext = ".txt" Then
                    analyzeRawFiles.Add(New KeyValuePair(Of String, String())(targetFile.FullName, ReadFile(targetFile.FullName).Split(vbCrLf.ToCharArray)))
                Else
                    File.Delete(targetFile.FullName)
                End If
            Catch ex As Exception
                Log(ex, "导入单个日志文件失败")
            End Try
        Next

        Log("[Crash] 步骤 1：自主导入日志文件，收集到 " & analyzeRawFiles.Count & " 个文件")
        Return analyzeRawFiles
    End Function

End Module
