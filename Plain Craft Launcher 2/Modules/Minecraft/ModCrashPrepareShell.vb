Imports PCL.Core.Utils.Exts

Public Partial Class CrashAnalyzer

    Private AnalyzeRawFiles As New List(Of KeyValuePair(Of String, String()))

    Private Enum AnalyzeFileType
        HsErr
        MinecraftLog
        ExtraLogFile
        ExtraReportFile
        CrashReport
    End Enum

    Private DirectFile As KeyValuePair(Of String, String())? = Nothing
    Private OutputFiles As New List(Of String)

    Public Function Prepare() As Boolean
        Log("[Crash] 步骤 2：准备日志文本")

        DirectFile = Nothing
        Dim allFiles As New List(Of KeyValuePair(Of AnalyzeFileType, KeyValuePair(Of String, String())))
        For Each logFile In AnalyzeRawFiles
            Dim matchName As String = GetFileNameFromPath(logFile.Key).ToLower
            Dim targetType As AnalyzeFileType
            If matchName.StartsWithF("hs_err") Then
                targetType = AnalyzeFileType.HsErr
                DirectFile = logFile
            ElseIf matchName.StartsWithF("crash-") Then
                targetType = AnalyzeFileType.CrashReport
                DirectFile = logFile
            ElseIf matchName = "latest.log" OrElse matchName = "latest log.txt" OrElse
                   matchName = "debug.log" OrElse matchName = "debug log.txt" OrElse
                   matchName = "游戏崩溃前的输出.txt" OrElse matchName = "rawoutput.log" Then
                targetType = AnalyzeFileType.MinecraftLog
                If DirectFile Is Nothing Then DirectFile = logFile
            ElseIf matchName = "启动器日志.txt" OrElse matchName = "PCL2 启动器日志.txt" OrElse matchName = "PCL 启动器日志.txt" OrElse matchName = "log1.txt" OrElse matchName = "log-ce1.log" Then
                If logFile.Value.Any(Function(s) s.Contains("以下为游戏输出的最后一段内容")) Then
                    targetType = AnalyzeFileType.MinecraftLog
                    If DirectFile Is Nothing Then DirectFile = logFile
                Else
                    targetType = AnalyzeFileType.ExtraLogFile
                End If
            ElseIf matchName.EndsWithF(".log", True) Then
                targetType = AnalyzeFileType.ExtraLogFile
            ElseIf matchName.EndsWithF(".txt", True) Then
                targetType = AnalyzeFileType.ExtraReportFile
            Else
                Log("[Crash] " & matchName & " 分类为 Ignore")
                Continue For
            End If
            If logFile.Value.Any Then
                allFiles.Add(New KeyValuePair(Of AnalyzeFileType, KeyValuePair(Of String, String()))(targetType, logFile))
                Log("[Crash] " & matchName & " 分类为 " & GetStringFromEnum(targetType))
            Else
                Log("[Crash] " & matchName & " 由于内容为空跳过")
            End If
        Next

        If allFiles.Any() AndAlso allFiles.All(Function(p) p.Key = AnalyzeFileType.ExtraLogFile) Then
            Log("[Crash] 由于仅发现了额外日志，将它们视作 Minecraft 日志进行分析")
            allFiles = allFiles.Select(Function(p) New KeyValuePair(Of AnalyzeFileType, KeyValuePair(Of String, String()))(AnalyzeFileType.MinecraftLog, p.Value)).ToList
        End If

        For Each selectType In {AnalyzeFileType.MinecraftLog, AnalyzeFileType.HsErr, AnalyzeFileType.ExtraLogFile, AnalyzeFileType.CrashReport}
            Dim selectedFiles As New List(Of KeyValuePair(Of String, String()))
            For Each file In allFiles
                If selectType = file.Key Then selectedFiles.Add(file.Value)
            Next
            If Not selectedFiles.Any() Then Continue For
            Try
                Select Case selectType
                    Case AnalyzeFileType.HsErr, AnalyzeFileType.CrashReport
                        Dim datedFiles As New SortedList(Of Date, KeyValuePair(Of String, String()))()
                        For Each file In selectedFiles
                            Try
                                datedFiles.Add(New FileInfo(file.Key).LastWriteTime, file)
                            Catch ex As Exception
                                Log(ex, "获取日志文件修改时间失败")
                                datedFiles.Add(New Date(1900, 1, 1), file)
                            End Try
                        Next
                        Dim newestFile As KeyValuePair(Of String, String()) = datedFiles.Last.Value
                        OutputFiles.Add(newestFile.Key)
                        If selectType = AnalyzeFileType.HsErr Then
                            LogHs = GetHeadTailLines(newestFile.Value, 200, 100)
                            Log("[Crash] 输出报告：" & newestFile.Key & "，作为虚拟机错误信息")
                            Log("[Crash] 导入分析：" & newestFile.Key & "，作为虚拟机错误信息")
                        Else
                            LogCrash = GetHeadTailLines(newestFile.Value, 300, 700)
                            Log("[Crash] 输出报告：" & newestFile.Key & "，作为 Minecraft 崩溃报告")
                            Log("[Crash] 导入分析：" & newestFile.Key & "，作为 Minecraft 崩溃报告")
                        End If
                    Case AnalyzeFileType.MinecraftLog
                        LogMc = ""
                        LogMcDebug = ""
                        Dim fileNameDict As New Dictionary(Of String, KeyValuePair(Of String, String()))
                        For Each selectedFile In selectedFiles
                            fileNameDict(GetFileNameFromPath(selectedFile.Key).ToLower) = selectedFile
                            OutputFiles.Add(selectedFile.Key)
                            Log("[Crash] 输出报告：" & selectedFile.Key & "，作为 Minecraft 或启动器日志")
                        Next
                        For Each fileName As String In {"rawoutput.log", "启动器日志.txt", "log1.txt", "log-ce1.log", "游戏崩溃前的输出.txt", "PCL2 启动器日志.txt", "PCL 启动器日志.txt"}
                            If Not fileNameDict.ContainsKey(fileName) Then Continue For
                            Dim currentLog = fileNameDict(fileName)
                            Dim hasLauncherMark As Boolean = False
                            For Each line In currentLog.Value
                                If hasLauncherMark Then
                                    LogMc += line & vbLf
                                ElseIf line.Contains("以下为游戏输出的最后一段内容") Then
                                    hasLauncherMark = True
                                    Log("[Crash] 找到 PCL 输出的游戏实时日志头")
                                End If
                            Next
                            If Not hasLauncherMark Then LogMc += GetHeadTailLines(currentLog.Value, 0, 500)
                            LogMc = LogMc.TrimEnd(vbCrLf.ToCharArray)
                            Log("[Crash] 导入分析：" & currentLog.Key & "，作为启动器日志")
                            Exit For
                        Next
                        For Each fileName As String In {"latest.log", "latest log.txt", "debug.log", "debug log.txt"}
                            If Not fileNameDict.ContainsKey(fileName) Then Continue For
                            Dim currentLog = fileNameDict(fileName)
                            LogMc += GetHeadTailLines(currentLog.Value, 1500, 500)
                            Log("[Crash] 导入分析：" & currentLog.Key & "，作为 Minecraft 日志")
                            Exit For
                        Next
                        For Each fileName As String In {"debug.log", "debug log.txt"}
                            If Not fileNameDict.ContainsKey(fileName) Then Continue For
                            Dim currentLog = fileNameDict(fileName)
                            LogMcDebug += GetHeadTailLines(currentLog.Value, 1000, 0)
                            Log("[Crash] 导入分析：" & currentLog.Key & "，作为 Minecraft Debug 日志")
                            Exit For
                        Next
                        If LogMc = "" Then
                            If LogMcDebug <> "" Then
                                LogMc = LogMcDebug
                            ElseIf fileNameDict.Any() Then
                                Dim currentLog = fileNameDict.First.Value
                                LogMc += GetHeadTailLines(currentLog.Value, 1500, 500)
                                Log("[Crash] 导入分析：" & currentLog.Key & "，作为兜底日志")
                            Else
                                LogMc = Nothing
                                Throw New Exception("无法找到匹配的 Minecraft Log")
                            End If
                        End If
                        If LogMcDebug = "" Then LogMcDebug = Nothing
                    Case AnalyzeFileType.ExtraLogFile, AnalyzeFileType.ExtraReportFile
                        For Each selectedFile In selectedFiles
                            OutputFiles.Add(selectedFile.Key)
                            Log("[Crash] 输出报告：" & selectedFile.Key & "，不用作分析")
                        Next
                End Select
            Catch ex As Exception
                Log(ex, "分类处理日志文件时出错")
            End Try
        Next

        Prepare = LogMc IsNot Nothing OrElse LogHs IsNot Nothing OrElse LogCrash IsNot Nothing
        If Prepare Then
            Log(("[Crash] 步骤 2：准备日志文本完成，找到" & If(LogMc Is Nothing, "", "游戏日志、") & If(LogMcDebug Is Nothing, "", "游戏 Debug 日志、") & If(LogHs Is Nothing, "", "虚拟机日志、") & If(LogCrash Is Nothing, "", "崩溃日志、")).TrimEnd("、") & "用作分析")
        Else
            Log("[Crash] 步骤 2：准备日志文本完成，没有任何可供分析的日志")
        End If
    End Function

    Private Function GetHeadTailLines(raw As String(), headLines As Integer, tailLines As Integer) As String
        If raw.Length <= headLines + tailLines Then Return Join(raw.Distinct, vbLf)
        Dim lines As New List(Of String)
        Dim realHeadLines As Integer = 0, viewedLines As Integer
        For viewedLines = 0 To raw.Length - 1
            If lines.Contains(raw(viewedLines)) Then Continue For
            realHeadLines += 1
            lines.Add(raw(viewedLines))
            If realHeadLines >= headLines Then Exit For
        Next
        Dim realTailLines = 0
        For i = raw.Length - 1 To viewedLines Step -1
            If lines.Contains(raw(i)) Then Continue For
            realTailLines += 1
            lines.Insert(realHeadLines, raw(i))
            If realTailLines >= tailLines Then Exit For
        Next
        Dim result As New StringBuilder
        For Each line In lines
            If line = "" Then Continue For
            result.Append(line)
            result.Append(vbLf)
        Next
        Return result.ToString
    End Function

End Class
