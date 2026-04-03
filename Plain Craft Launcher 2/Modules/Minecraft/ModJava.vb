Imports PCL.Core.Minecraft
Imports PCL.Core.App
Imports PCL.Core.Minecraft.Launch
Imports System.Text.Json
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Minecraft.Java.UserPreference
Imports PCL.Core.IO

Public Module ModJava
    Public JavaListCacheVersion As Integer = 7

    ''' <summary>
    ''' 目前所有可用的 Java。
    ''' </summary>
    Public ReadOnly Property Javas As JavaManager
        Get
            Return JavaService.JavaManager
        End Get
    End Property

    ''' <summary>
    ''' 防止多个需要 Java 的部分同时要求下载 Java（#3797）。
    ''' </summary>
    Public JavaLock As New Object
    ''' <summary>
    ''' 根据要求返回最适合的 Java，若找不到则返回 Nothing。
    ''' 最小与最大版本在与输入相同时也会通过。
    ''' 必须在工作线程调用，且必须包括 SyncLock JavaLock。
    ''' </summary>
    Public Function JavaSelect(CancelException As String,
                               Optional MinVersion As Version = Nothing,
                               Optional MaxVersion As Version = Nothing,
                               Optional RelatedInstance As McInstance = Nothing) As JavaEntry
        Log($"[Java] 要求选择合适 Java，要求最低版本 {If(MinVersion IsNot Nothing, MinVersion.ToString(), "未指定")}，要求选择的最高版本 {If(MaxVersion IsNot Nothing, MaxVersion.ToString(), "未指定")}，关联实例 {If(RelatedInstance IsNot Nothing, RelatedInstance.Name, "未指定")}")

        ' 版本范围验证函数（安全处理 null 边界）
        Dim IsVersionSuitable = Function(ver As Version) As Boolean
                                    Return (MinVersion Is Nothing OrElse ver >= MinVersion) AndAlso
                                   (MaxVersion Is Nothing OrElse ver <= MaxVersion)
                                End Function

        ' ===== 优先级 1：实例专属 Java 偏好 =====
        If RelatedInstance IsNot Nothing AndAlso RelatedInstance.PathInstance IsNot Nothing Then
            Dim rawPreference = Config.Instance.SelectedJava(RelatedInstance.PathInstance)

            If Not String.IsNullOrWhiteSpace(rawPreference) Then
                Dim preference As JavaPreference = GetInstanceJavaPreference(RelatedInstance)

                ' 处理解析成功的偏好
                If preference IsNot Nothing Then
                    Select Case True
                        Case TypeOf preference Is ExistingJava ' "exist"
                            Dim existPref = DirectCast(preference, ExistingJava)
                            Dim candidate = Javas.AddOrGet(existPref.JavaExePath)

                            If candidate IsNot Nothing AndAlso candidate.IsEnabled Then
                                If Not IsVersionSuitable(candidate.Installation.Version) Then
                                    Hint($"实例指定的 Java ({candidate.Installation.Version}) 超出版本要求范围 [{If(MinVersion?.ToString(), "无下限")}, {If(MaxVersion?.ToString(), "无上限")}]，可能导致游戏崩溃")
                                End If
                                Log($"[Java] 返回实例 '{RelatedInstance.Name}' 指定的 Java: {candidate}")
                                Return candidate
                            Else
                                Log($"[Java] 警告：实例指定的 Java 路径无效或不可用: {existPref.JavaExePath}")
                            End If

                        Case TypeOf preference Is UseRelativePath ' "relative"
                            Dim relPref = DirectCast(preference, UseRelativePath)
                            Dim absPath = IO.Path.GetFullPath(IO.Path.Combine(Basics.ExecutableDirectory, relPref.RelativePath))

                            If Files.IsPathWithinDirectory(absPath, Basics.ExecutableDirectory) Then
                                Dim candidate = Javas.Get(absPath)
                                If candidate IsNot Nothing AndAlso candidate.IsEnabled Then
                                    If Not IsVersionSuitable(candidate.Installation.Version) Then
                                        Hint($"实例相对路径指定的 Java (v{candidate.Installation.Version}) 超出版本要求范围，可能导致游戏崩溃", HintType.Critical)
                                    End If
                                    Log($"[Java] 返回实例 '{RelatedInstance.Name}' 相对路径指定的 Java ({relPref.RelativePath}): {candidate}")
                                    Return candidate
                                End If
                            Else
                                Log($"[Java] 警告：实例相对路径指定的 Java 无效: {absPath}")
                            End If

                        Case TypeOf preference Is UseGlobalPreference ' "global"
                            Log($"[Java] 实例 '{RelatedInstance.Name}' 配置为使用全局 Java 设置，继续检查全局配置")
                            ' 不返回，继续到全局设置检查

                        Case Else
                            Log($"[Java] 警告：未知的 Java 偏好类型 '{preference}'，跳过处理")
                    End Select
                Else
                    Log($"[Java] 实例 '{RelatedInstance.Name}' 未指定 Java 偏好（空值），使用自动选择策略")
                End If
            Else
                Log($"[Java] 实例 '{RelatedInstance.Name}' 无 Java 偏好配置，使用自动选择策略")
            End If
        End If

        ' ===== 优先级 2：全局指定的 Java =====
        Dim globalJavaPath = Config.Launch.SelectedJava
        If Not String.IsNullOrWhiteSpace(globalJavaPath) Then
            globalJavaPath = globalJavaPath.Trim()
            Dim candidate = Javas.AddOrGet(globalJavaPath)

            If candidate IsNot Nothing AndAlso candidate.IsEnabled Then
                If Not IsVersionSuitable(candidate.Installation.Version) Then
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

        ' ===== 优先级 3：自动搜索合适版本 =====
        Log("[Java] 开始自动搜索符合版本要求的 Java 运行时")
        Javas.CheckAllAvailability()

        Dim reqMin = If(MinVersion, New Version(1, 0, 0))
        Dim reqMax = If(MaxVersion, New Version(999, 999, 999))

        Dim candidates = Javas.SelectSuitableJavaAsync(reqMin, reqMax).GetAwaiter().GetResult()
        Dim ret = candidates.FirstOrDefault()

        If ret Is Nothing AndAlso candidates.Count = 0 Then
            Log("[Java] 未找到符合版本要求的 Java，触发全盘重新扫描")
            Javas.ScanJavaAsync().GetAwaiter().GetResult()
            candidates = Javas.SelectSuitableJavaAsync(reqMin, reqMax).GetAwaiter().GetResult()
            ret = candidates.FirstOrDefault()
        End If

        If ret IsNot Nothing Then
            Log($"[Java] 返回自动选择的 Java: {ret}")
        Else
            Log("[Java] 最终未能确定可用的 Java 运行时")
        End If

        Return ret
    End Function

    Public Function GetInstanceJavaPreference(instance As McInstance) As JavaPreference
        Dim rawPreference = Config.Instance.SelectedJava(instance.PathInstance)

        Dim preference As JavaPreference = Nothing

        '尝试读取 JSON 配置
        If Not rawPreference.IsNullOrEmpty() Then
            Try
                preference = JsonSerializer.Deserialize(Of JavaPreference)(rawPreference)
            Catch ex As JsonException
                'ignored
            End Try
        End If
        '以旧方式读取配置
        If preference Is Nothing Then
            Dim trimmed = rawPreference?.Trim()
            If trimmed.IsNullOrEmpty() Then
                preference = New AutoSelect()
            ElseIf trimmed = "使用全局设置" Then '全局设置
                preference = New UseGlobalPreference()
            Else
                preference = New ExistingJava(trimmed)
            End If
        End If

        Select Case True
            Case TypeOf preference Is ExistingJava
                Dim m = DirectCast(preference, ExistingJava)
                If Not IO.Path.IsPathRooted(m.JavaExePath) Then
                    preference = New UseGlobalPreference()
                End If
            Case TypeOf preference Is UseRelativePath
                Dim m = DirectCast(preference, UseRelativePath)
                If Not Files.IsPathWithinDirectory(m.RelativePath, Basics.ExecutableDirectory) Then
                    preference = New UseGlobalPreference()
                End If
        End Select

        Return preference
    End Function

    ''' <summary>
    ''' 是否强制指定了 64 位 Java。如果没有强制指定，返回是否安装了 64 位 Java。
    ''' </summary>
    Public Function IsGameSet64BitJava(Optional RelatedVersion As McInstance = Nothing) As Boolean
        Try
            '检查强制指定
            Dim UserSetup As String = Setup.Get("LaunchArgumentJavaSelect")
            If UserSetup.StartsWith("{") Then '旧版本 Json 格式
                Dim js = JToken.Parse(UserSetup)
                UserSetup = $"{js("Path")}java.exe"
                Setup.Set("LaunchArgumentJavaSelect", UserSetup)
            End If
            If RelatedVersion IsNot Nothing Then
                Dim instancePreference = GetInstanceJavaPreference(RelatedVersion)
                Select Case True
                    Case TypeOf instancePreference Is AutoSelect
                        Return Javas.Existing64BitJava()
                    Case TypeOf instancePreference Is ExistingJava
                        Dim m = DirectCast(instancePreference, ExistingJava)
                        Dim java = Javas.AddOrGet(m.JavaExePath)
                        Return java IsNot Nothing AndAlso java.Installation.Is64Bit
                    Case TypeOf instancePreference Is UseRelativePath
                        Dim m = DirectCast(instancePreference, UseRelativePath)
                        Dim javaExePath = IO.Path.GetFullPath(m.RelativePath)
                        If Files.IsPathWithinDirectory(javaExePath, Basics.ExecutableDirectory) Then
                            Dim java = Javas.Get(javaExePath)
                            Return java IsNot Nothing AndAlso java.Installation.Is64Bit
                        End If
                End Select
            End If
            If Not String.IsNullOrEmpty(UserSetup) AndAlso Not File.Exists(UserSetup) Then
                Setup.Set("LaunchArgumentJavaSelect", "")
                UserSetup = String.Empty
            End If
            If String.IsNullOrEmpty(UserSetup) Then
                Return Javas.Existing64BitJava()
            End If
            Dim j = Javas.AddOrGet(UserSetup)
            Return j IsNot Nothing AndAlso j.Installation.Is64Bit
        Catch ex As Exception
            Log(ex, "检查 Java 类别时出错", LogLevel.Feedback)
            If RelatedVersion IsNot Nothing Then Setup.Reset("VersionArgumentJavaSelect", instance:=RelatedVersion)
            Setup.Set("LaunchArgumentJavaSelect", "")
        End Try
        Return True
    End Function

#Region "下载"

    ''' <summary>
    ''' 提示 Java 缺失，并弹窗确认是否自动下载。返回玩家选择是否下载。
    ''' </summary>
    Public Function JavaDownloadConfirm(VersionDescription As String, Optional ForcedManualDownload As Boolean = False) As Boolean
        Return ModJavaPromptShell.ConfirmJavaDownload(VersionDescription, ForcedManualDownload)
    End Function

    ''' <summary>
    ''' 获取下载 Java 的加载器。需要开启 IsForceRestart 以正常刷新 Java 列表。
    ''' </summary>
    Public Function GetJavaDownloadLoader() As LoaderCombo(Of String)
        Return ModJavaLoaderShell.CreateDownloadLoader()
    End Function

#End Region

    Public Function ResolveLaunchJavaSelection(task As LoaderTask(Of Integer, Integer),
                                               javaWorkflow As MinecraftLaunchJavaWorkflowPlan,
                                               relatedInstance As McInstance) As JavaEntry
        Return ModJavaSelectionShell.ResolveLaunchJavaSelectionShell(task, javaWorkflow, relatedInstance)
    End Function

End Module
