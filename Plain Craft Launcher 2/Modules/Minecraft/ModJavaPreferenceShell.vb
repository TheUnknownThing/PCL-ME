Imports System.Text.Json
Imports PCL.Core.App
Imports PCL.Core.IO
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Java.UserPreference

Public Module ModJavaPreferenceShell

    Public Function JavaSelectShell(javaManager As JavaManager,
                                    cancelException As String,
                                    Optional minVersion As Version = Nothing,
                                    Optional maxVersion As Version = Nothing,
                                    Optional relatedInstance As McInstance = Nothing) As JavaEntry
        Log($"[Java] 要求选择合适 Java，要求最低版本 {If(minVersion IsNot Nothing, minVersion.ToString(), "未指定")}，要求选择的最高版本 {If(maxVersion IsNot Nothing, maxVersion.ToString(), "未指定")}，关联实例 {If(relatedInstance IsNot Nothing, relatedInstance.Name, "未指定")}")

        Dim isVersionSuitable = Function(ver As Version) As Boolean
                                    Return (minVersion Is Nothing OrElse ver >= minVersion) AndAlso
                                           (maxVersion Is Nothing OrElse ver <= maxVersion)
                                End Function

        If relatedInstance IsNot Nothing AndAlso relatedInstance.PathInstance IsNot Nothing Then
            Dim rawPreference = Config.Instance.SelectedJava(relatedInstance.PathInstance)
            If Not String.IsNullOrWhiteSpace(rawPreference) Then
                Dim preference As JavaPreference = GetInstanceJavaPreferenceShell(relatedInstance)
                If preference IsNot Nothing Then
                    Select Case True
                        Case TypeOf preference Is ExistingJava
                            Dim existPref = DirectCast(preference, ExistingJava)
                            Dim candidate = javaManager.AddOrGet(existPref.JavaExePath)
                            If candidate IsNot Nothing AndAlso candidate.IsEnabled Then
                                If Not isVersionSuitable(candidate.Installation.Version) Then
                                    Hint($"实例指定的 Java ({candidate.Installation.Version}) 超出版本要求范围 [{If(minVersion?.ToString(), "无下限")}, {If(maxVersion?.ToString(), "无上限")}]，可能导致游戏崩溃")
                                End If
                                Log($"[Java] 返回实例 '{relatedInstance.Name}' 指定的 Java: {candidate}")
                                Return candidate
                            Else
                                Log($"[Java] 警告：实例指定的 Java 路径无效或不可用: {existPref.JavaExePath}")
                            End If
                        Case TypeOf preference Is UseRelativePath
                            Dim relPref = DirectCast(preference, UseRelativePath)
                            Dim absPath = IO.Path.GetFullPath(IO.Path.Combine(Basics.ExecutableDirectory, relPref.RelativePath))
                            If Files.IsPathWithinDirectory(absPath, Basics.ExecutableDirectory) Then
                                Dim candidate = javaManager.Get(absPath)
                                If candidate IsNot Nothing AndAlso candidate.IsEnabled Then
                                    If Not isVersionSuitable(candidate.Installation.Version) Then
                                        Hint($"实例相对路径指定的 Java (v{candidate.Installation.Version}) 超出版本要求范围，可能导致游戏崩溃", HintType.Critical)
                                    End If
                                    Log($"[Java] 返回实例 '{relatedInstance.Name}' 相对路径指定的 Java ({relPref.RelativePath}): {candidate}")
                                    Return candidate
                                End If
                            Else
                                Log($"[Java] 警告：实例相对路径指定的 Java 无效: {absPath}")
                            End If
                        Case TypeOf preference Is UseGlobalPreference
                            Log($"[Java] 实例 '{relatedInstance.Name}' 配置为使用全局 Java 设置，继续检查全局配置")
                        Case Else
                            Log($"[Java] 警告：未知的 Java 偏好类型 '{preference}'，跳过处理")
                    End Select
                Else
                    Log($"[Java] 实例 '{relatedInstance.Name}' 未指定 Java 偏好（空值），使用自动选择策略")
                End If
            Else
                Log($"[Java] 实例 '{relatedInstance.Name}' 无 Java 偏好配置，使用自动选择策略")
            End If
        End If

        Dim globalJavaPath = Config.Launch.SelectedJava
        If Not String.IsNullOrWhiteSpace(globalJavaPath) Then
            globalJavaPath = globalJavaPath.Trim()
            Dim candidate = javaManager.AddOrGet(globalJavaPath)
            If candidate IsNot Nothing AndAlso candidate.IsEnabled Then
                If Not isVersionSuitable(candidate.Installation.Version) Then
                    Hint($"全局指定的 Java (v{candidate.Installation.Version}) 超出版本要求范围，可能导致游戏崩溃")
                End If
                Log($"[Java] 返回全局指定的 Java: {candidate}")
                Return candidate
            Else
                Log($"[Java] 警告：全局指定的 Java 路径无效或不可用: {globalJavaPath}")
            End If
        Else
            Log("[Java] 无全局 Java 配置，使用自动选择策略")
        End If

        Log("[Java] 开始自动搜索符合版本要求的 Java 运行时")
        javaManager.CheckAllAvailability()

        Dim reqMin = If(minVersion, New Version(1, 0, 0))
        Dim reqMax = If(maxVersion, New Version(999, 999, 999))
        Dim candidates = javaManager.SelectSuitableJavaAsync(reqMin, reqMax).GetAwaiter().GetResult()
        Dim ret = candidates.FirstOrDefault()
        If ret Is Nothing AndAlso candidates.Count = 0 Then
            Log("[Java] 未找到符合版本要求的 Java，触发全盘重新扫描")
            javaManager.ScanJavaAsync().GetAwaiter().GetResult()
            candidates = javaManager.SelectSuitableJavaAsync(reqMin, reqMax).GetAwaiter().GetResult()
            ret = candidates.FirstOrDefault()
        End If

        If ret IsNot Nothing Then
            Log($"[Java] 返回自动选择的 Java: {ret}")
        Else
            Log("[Java] 最终未能确定可用的 Java 运行时")
        End If

        Return ret
    End Function

    Public Function GetInstanceJavaPreferenceShell(instance As McInstance) As JavaPreference
        Dim rawPreference = Config.Instance.SelectedJava(instance.PathInstance)
        Dim preference As JavaPreference = Nothing

        If Not String.IsNullOrEmpty(rawPreference) Then
            Try
                preference = JsonSerializer.Deserialize(Of JavaPreference)(rawPreference)
            Catch ex As JsonException
            End Try
        End If

        If preference Is Nothing Then
            Dim trimmed = rawPreference?.Trim()
            If String.IsNullOrEmpty(trimmed) Then
                preference = New AutoSelect()
            ElseIf trimmed = "使用全局设置" Then
                preference = New UseGlobalPreference()
            Else
                preference = New ExistingJava(trimmed)
            End If
        End If

        Select Case True
            Case TypeOf preference Is ExistingJava
                Dim m = DirectCast(preference, ExistingJava)
                If Not IO.Path.IsPathRooted(m.JavaExePath) Then preference = New UseGlobalPreference()
            Case TypeOf preference Is UseRelativePath
                Dim m = DirectCast(preference, UseRelativePath)
                If Not Files.IsPathWithinDirectory(m.RelativePath, Basics.ExecutableDirectory) Then preference = New UseGlobalPreference()
        End Select

        Return preference
    End Function

    Public Function IsGameSet64BitJavaShell(javaManager As JavaManager,
                                            Optional relatedVersion As McInstance = Nothing) As Boolean
        Try
            Dim userSetup As String = Setup.Get("LaunchArgumentJavaSelect")
            If userSetup.StartsWith("{") Then
                Dim js = JToken.Parse(userSetup)
                userSetup = $"{js("Path")}java.exe"
                Setup.Set("LaunchArgumentJavaSelect", userSetup)
            End If
            If relatedVersion IsNot Nothing Then
                Dim instancePreference = GetInstanceJavaPreferenceShell(relatedVersion)
                Select Case True
                    Case TypeOf instancePreference Is AutoSelect
                        Return javaManager.Existing64BitJava()
                    Case TypeOf instancePreference Is ExistingJava
                        Dim m = DirectCast(instancePreference, ExistingJava)
                        Dim java = javaManager.AddOrGet(m.JavaExePath)
                        Return java IsNot Nothing AndAlso java.Installation.Is64Bit
                    Case TypeOf instancePreference Is UseRelativePath
                        Dim m = DirectCast(instancePreference, UseRelativePath)
                        Dim javaExePath = IO.Path.GetFullPath(m.RelativePath)
                        If Files.IsPathWithinDirectory(javaExePath, Basics.ExecutableDirectory) Then
                            Dim java = javaManager.Get(javaExePath)
                            Return java IsNot Nothing AndAlso java.Installation.Is64Bit
                        End If
                End Select
            End If
            If Not String.IsNullOrEmpty(userSetup) AndAlso Not File.Exists(userSetup) Then
                Setup.Set("LaunchArgumentJavaSelect", "")
                userSetup = String.Empty
            End If
            If String.IsNullOrEmpty(userSetup) Then Return javaManager.Existing64BitJava()
            Dim j = javaManager.AddOrGet(userSetup)
            Return j IsNot Nothing AndAlso j.Installation.Is64Bit
        Catch ex As Exception
            Log(ex, "检查 Java 类别时出错", LogLevel.Feedback)
            If relatedVersion IsNot Nothing Then Setup.Reset("VersionArgumentJavaSelect", instance:=relatedVersion)
            Setup.Set("LaunchArgumentJavaSelect", "")
        End Try
        Return True
    End Function

End Module
